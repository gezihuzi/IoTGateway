﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Text;
using Waher.Content;
using Waher.Events;
using Waher.Networking.Sniffers;
using Waher.Networking.HTTP.HeaderFields;
using Waher.Networking.HTTP.TransferEncodings;
using Waher.Networking.HTTP.WebSockets;
using Waher.Security;

namespace Waher.Networking.HTTP
{
	internal enum ConnectionMode
	{
		Http,
		WebSocket
	}

	/// <summary>
	/// Class managing a remote client connection to a local <see cref="HttpServer"/>.
	/// </summary>
	internal class HttpClientConnection : Sniffable, IDisposable
	{
		internal const byte CR = 13;
		internal const byte LF = 10;
		internal const int MaxHeaderSize = 65536;
		internal const int MaxInmemoryMessageSize = 1024 * 1024;    // 1 MB
		internal const long MaxEntitySize = 1024 * 1024 * 1024;     // 1 GB

		private MemoryStream headerStream = null;
		private Stream dataStream = null;
		private TransferEncoding transferEncoding = null;
		private readonly HttpServer server;
		private BinaryTcpClient client;
		private HttpRequestHeader header = null;
		private ConnectionMode mode = ConnectionMode.Http;
		private WebSocket webSocket = null;
		private byte b1 = 0;
		private byte b2 = 0;
		private byte b3 = 0;
		private readonly bool encrypted;
		private bool disposed = false;

		internal HttpClientConnection(HttpServer Server, BinaryTcpClient Client, bool Encrypted, params ISniffer[] Sniffers)
			: base(Sniffers)
		{
			this.server = Server;
			this.client = Client;
			this.encrypted = Encrypted;

			this.client.OnDisconnected += Client_OnDisconnected;
			this.client.OnError += Client_OnError;
			this.client.OnReceived += Client_OnReceived;
		}

		private Task<bool> Client_OnReceived(object Sender, byte[] Buffer, int Offset, int Count)
		{
			bool Continue;

			this.server.DataReceived(Count);

			if (this.mode == ConnectionMode.Http)
			{
				if (this.header is null)
					return this.BinaryHeaderReceived(Buffer, Offset, Count);
				else
					return this.BinaryDataReceived(Buffer, Offset, Count);
			}
			else
				Continue = this.webSocket?.WebSocketDataReceived(Buffer, Offset, Count) ?? false;

			return Task.FromResult<bool>(Continue);
		}

		private void Client_OnError(object Sender, Exception Exception)
		{
			this.Dispose();
		}

		private void Client_OnDisconnected(object sender, EventArgs e)
		{
			this.Dispose();
		}

		public void Dispose()
		{
			if (!this.disposed)
			{
				this.disposed = true;

				this.webSocket?.Dispose();
				this.webSocket = null;

				this.headerStream?.Dispose();
				this.headerStream = null;

				this.dataStream?.Dispose();
				this.dataStream = null;

				this.client?.DisposeWhenDone();
				this.client = null;
			}
		}

		internal HttpServer Server
		{
			get { return this.server; }
		}

		internal bool Disposed
		{
			get { return this.disposed; }
		}

		internal BinaryTcpClient Client
		{
			get { return this.client; }
		}

#if !WINDOWS_UWP
		internal bool Encrypted
		{
			get { return this.encrypted; }
		}
#endif

		private Task<bool> BinaryHeaderReceived(byte[] Data, int Offset, int NrRead)
		{
			string Header;
			int i, c;
			byte b;

			c = Offset + NrRead;

			for (i = Offset; i < c; i++)
			{
				b = Data[i];

				if (this.b1 == CR && this.b2 == LF && this.b3 == CR && b == LF) // RFC 2616, §2.2
				{
					if (this.headerStream is null)
						Header = InternetContent.ISO_8859_1.GetString(Data, Offset, i - Offset - 3);
					else
					{
						this.headerStream.Write(Data, Offset, i - Offset - 3);

						Header = InternetContent.ISO_8859_1.GetString(this.headerStream.ToArray(), 0, (int)this.headerStream.Position);
						this.headerStream = null;
					}
				}
				else if (this.b3 == LF && b == LF)  // RFC 2616, §19.3
				{
					if (this.headerStream is null)
						Header = InternetContent.ISO_8859_1.GetString(Data, Offset, i - Offset - 1);
					else
					{
						this.headerStream.Write(Data, Offset, i - Offset - 1);
						Header = InternetContent.ISO_8859_1.GetString(this.headerStream.ToArray(), 0, (int)this.headerStream.Position);
						this.headerStream = null;
					}
				}
				else
				{
					this.b1 = this.b2;
					this.b2 = this.b3;
					this.b3 = b;
					continue;
				}

				this.ReceiveText(Header);
				this.header = new HttpRequestHeader(Header, this.encrypted ? "https" : "http");

				if (this.header.HttpVersion < 1)
				{
					this.SendResponse(null, new HttpException(505, "HTTP Version Not Supported", "At least HTTP Version 1.0 is required."), true);
					return Task.FromResult<bool>(false);
				}
				else if (this.header.ContentLength != null && (this.header.ContentLength.ContentLength > MaxEntitySize))
				{
					this.SendResponse(null, new HttpException(413, "Request Entity Too Large", "Maximum Entity Size: " + MaxEntitySize.ToString()), true);
					return Task.FromResult<bool>(false);
				}
				else if (i + 1 < NrRead)
					return this.BinaryDataReceived(Data, i + 1, NrRead - i - 1);
				else if (!this.header.HasMessageBody)
					return this.RequestReceived();
				else
					return Task.FromResult<bool>(true);
			}

			if (this.headerStream is null)
				this.headerStream = new MemoryStream();

			this.headerStream.Write(Data, Offset, NrRead);

			if (this.headerStream.Position < MaxHeaderSize)
				return Task.FromResult<bool>(true);
			else
			{
				if (this.HasSniffers)
				{
					int d = (int)this.headerStream.Position;
					byte[] Data2 = new byte[d];
					this.headerStream.Position = 0;
					this.headerStream.Read(Data2, 0, d);
					this.ReceiveBinary(Data2);
				}

				this.SendResponse(null, new HttpException(431, "Request Header Fields Too Large", "Max Header Size: " + MaxHeaderSize.ToString()), true);
				return Task.FromResult<bool>(false);
			}
		}

		private async Task<bool> BinaryDataReceived(byte[] Data, int Offset, int NrRead)
		{
			if (this.dataStream is null)
			{
				HttpFieldTransferEncoding TransferEncoding = this.header.TransferEncoding;
				if (TransferEncoding != null)
				{
					if (TransferEncoding.Value == "chunked")
					{
						this.dataStream = new TemporaryFile();
						this.transferEncoding = new ChunkedTransferEncoding(new BinaryOutputStream(this.dataStream), null);
					}
					else
					{
						this.SendResponse(null, new HttpException(501, "Not Implemented", "Transfer encoding not implemented."), false);
						return true;
					}
				}
				else
				{
					HttpFieldContentLength ContentLength = this.header.ContentLength;
					if (ContentLength != null)
					{
						long l = ContentLength.ContentLength;
						if (l < 0)
						{
							this.SendResponse(null, new HttpException(400, "Bad Request", "Negative content lengths invalid."), false);
							return true;
						}

						if (l <= MaxInmemoryMessageSize)
							this.dataStream = new MemoryStream((int)l);
						else
							this.dataStream = new TemporaryFile();

						this.transferEncoding = new ContentLengthEncoding(new BinaryOutputStream(this.dataStream), l, null);
					}
					else
					{
						this.SendResponse(null, new HttpException(411, "Length Required", "Content Length required."), true);
						return false;
					}
				}
			}

			ulong DecodingResponse = await this.transferEncoding.DecodeAsync(Data, Offset, NrRead);
			int NrAccepted = (int)DecodingResponse;
			bool Complete = (DecodingResponse & 0x100000000) != 0;

			if (this.HasSniffers)
			{
				if (Offset == 0 && NrAccepted == Data.Length)
					this.ReceiveBinary(Data);
				else
				{
					byte[] Data2 = new byte[NrAccepted];
					Array.Copy(Data, Offset, Data2, 0, NrAccepted);
					this.ReceiveBinary(Data2);
				}
			}

			if (Complete)
			{
				if (this.transferEncoding.InvalidEncoding)
				{
					this.SendResponse(null, new HttpException(400, "Bad Request", "Invalid transfer encoding."), false);
					return true;
				}
				else if (this.transferEncoding.TransferError)
				{
					this.SendResponse(null, new HttpException(500, "Internal Server Error", "Unable to transfer content to resource."), false);
					return true;
				}
				else
				{
					Offset += NrAccepted;
					NrRead -= NrAccepted;

					if (!await this.RequestReceived())
						return false;

					if (NrRead > 0)
						return await this.BinaryHeaderReceived(Data, Offset, NrRead);
					else
						return true;
				}
			}
			else if (this.dataStream.Position > MaxEntitySize)
			{
				this.dataStream.Dispose();
				this.dataStream = null;

				this.SendResponse(null, new HttpException(413, "Request Entity Too Large", "Maximum Entity Size: " + MaxEntitySize.ToString()), true);
				return false;
			}
			else
				return true;
		}

		private Task<bool> RequestReceived()
		{
#if WINDOWS_UWP
			HttpRequest Request = new HttpRequest(this.header, this.dataStream, 
				this.client.Client.Information.RemoteAddress.ToString() + ":" + this.client.Client.Information.RemotePort);
#else
			HttpRequest Request = new HttpRequest(this.header, this.dataStream, this.client.Client.Client.RemoteEndPoint.ToString());
#endif
			Request.clientConnection = this;

			bool? Queued = this.QueueRequest(Request);

			if (Queued.HasValue)
			{
				if (!Queued.Value && this.dataStream != null)
					this.dataStream.Dispose();

				this.header = null;
				this.dataStream = null;
				this.transferEncoding = null;

				return Task.FromResult<bool>(Queued.Value);
			}
			else
				return Task.FromResult<bool>(true);
		}

		private bool? QueueRequest(HttpRequest Request)
		{
			HttpAuthenticationScheme[] AuthenticationSchemes;
			bool Result;

			try
			{
				if (this.server.TryGetResource(Request.Header.Resource, out HttpResource Resource, out string SubPath))
				{
					Request.Resource = Resource;
#if WINDOWS_UWP
					this.server.RequestReceived(Request, this.client.Client.Information.RemoteAddress.ToString() + ":" + 
						this.client.Client.Information.RemotePort, Resource, SubPath);
#else
					this.server.RequestReceived(Request, this.client.Client.Client.RemoteEndPoint.ToString(), Resource, SubPath);
#endif

					AuthenticationSchemes = Resource.GetAuthenticationSchemes(Request);
					if (AuthenticationSchemes != null && AuthenticationSchemes.Length > 0)
					{
						foreach (HttpAuthenticationScheme Scheme in AuthenticationSchemes)
						{
							if (Scheme.IsAuthenticated(Request, out IUser User))
							{
								Request.User = User;
								break;
							}
						}

						if (Request.User is null)
						{
							List<KeyValuePair<string, string>> Challenges = new List<KeyValuePair<string, string>>();

							foreach (HttpAuthenticationScheme Scheme in AuthenticationSchemes)
								Challenges.Add(new KeyValuePair<string, string>("WWW-Authenticate", Scheme.GetChallenge()));

							this.SendResponse(Request, new HttpException(401, "Unauthorized", "Unauthorized access prohibited."), false, Challenges.ToArray());
							Request.Dispose();
							return true;
						}
					}

					Request.SubPath = SubPath;
					Resource.Validate(Request);

					if (Request.Header.Expect != null)
					{
						if (Request.Header.Expect.Continue100)
						{
							if (!Request.HasData)
							{
								this.SendResponse(Request, new HttpException(100, "Continue", null), false);
								return null;
							}
						}
						else
						{
							this.SendResponse(Request, new HttpException(417, "Expectation Failed", "Unable to parse Expect header."), true);
							Request.Dispose();
							return false;
						}
					}

					Task.Run(() => this.ProcessRequest(Request, Resource));
					return true;
				}
				else
				{
					this.SendResponse(Request, new NotFoundException("Resource not found: " + this.server.CheckResourceOverride(Request.Header.Resource)), false);
					Result = true;
				}
			}
			catch (HttpException ex)
			{
				Result = (Request.Header.Expect is null || !Request.Header.Expect.Continue100 || Request.HasData);
				this.SendResponse(Request, ex, !Result, ex.HeaderFields);
			}
			catch (System.NotImplementedException ex)
			{
				Result = (Request.Header.Expect is null || !Request.Header.Expect.Continue100 || Request.HasData);

				Log.Critical(ex);

				this.SendResponse(Request, new NotImplementedException(ex.Message), !Result);
			}
			catch (IOException ex)
			{
				Log.Critical(ex);

				int Win32ErrorCode = ex.HResult & 0xFFFF;
				if (Win32ErrorCode == 0x27 || Win32ErrorCode == 0x70)   // ERROR_HANDLE_DISK_FULL, ERROR_DISK_FULL
					this.SendResponse(Request, new HttpException(507, "Insufficient Storage", "Insufficient space."), true);
				else
					this.SendResponse(Request, new InternalServerErrorException(ex.Message), true);

				Result = false;
			}
			catch (Exception ex)
			{
				Result = (Request.Header.Expect is null || !Request.Header.Expect.Continue100 || Request.HasData);

				Log.Critical(ex);

				this.SendResponse(Request, new InternalServerErrorException(ex.Message), !Result);
			}

			Request.Dispose();
			return Result;
		}

		private KeyValuePair<string, string>[] Merge(KeyValuePair<string, string>[] Headers, LinkedList<Cookie> Cookies)
		{
			if (Cookies is null || Cookies.First is null)
				return Headers;

			List<KeyValuePair<string, string>> Result = new List<KeyValuePair<string, string>>();
			Result.AddRange(Headers);

			foreach (Cookie Cookie in Cookies)
				Result.Add(new KeyValuePair<string, string>("Set-Cookie", Cookie.ToString()));

			return Result.ToArray();
		}

		private void ProcessRequest(HttpRequest Request, HttpResource Resource)
		{
			HttpResponse Response = null;

			try
			{
				Response = new HttpResponse(this.client, this, this.server, Request);
#if !WINDOWS_UWP
				HttpRequestHeader Header = Request.Header;
				int? UpgradePort = null;

				if (!this.encrypted &&
					(Header.UpgradeInsecureRequests?.Upgrade ?? false) &&
					Header.Host != null &&
					string.Compare(Header.Host.Value, "localhost", true) != 0 &&
					((UpgradePort = this.server.UpgradePort).HasValue))
				{
					StringBuilder Location = new StringBuilder();
					string s;
					int i;

					Location.Append("https://");

					s = Header.Host.Value;
					i = s.IndexOf(':');
					if (i > 0)
						s = s.Substring(0, i);

					Location.Append(s);

					if (!(UpgradePort is null) && UpgradePort.Value != HttpServer.DefaultHttpsPort)
					{
						Location.Append(':');
						Location.Append(UpgradePort.Value.ToString());
					}

					Location.Append(Header.Resource);

					if (!string.IsNullOrEmpty(s = Header.QueryString))
					{
						Location.Append('?');
						Location.Append(Header.QueryString);
					}

					if (!string.IsNullOrEmpty(s = Header.Fragment))
					{
						Location.Append('#');
						Location.Append(Header.Fragment);
					}

					this.SendResponse(Request, new HttpException(307, "Moved Temporarily",
						new KeyValuePair<string, string>("Location", Location.ToString()),
						new KeyValuePair<string, string>("Vary", "Upgrade-Insecure-Requests")), false);
				}
				else
#endif
					Resource.Execute(this.server, Request, Response);
			}
			catch (HttpException ex)
			{
				if (Response is null || !Response.HeaderSent)
				{
					try
					{
						this.SendResponse(Request, ex, false, this.Merge(ex.HeaderFields, Response.Cookies));
					}
					catch (Exception)
					{
						this.CloseStream();
					}
				}
				else
					this.CloseStream();
			}
			catch (Exception ex)
			{
				Log.Critical(ex);

				if (Response is null || !Response.HeaderSent)
				{
					try
					{
						this.SendResponse(Request, new InternalServerErrorException(ex.Message), true);
					}
					catch (Exception)
					{
						this.CloseStream();
					}
				}
				else
					this.CloseStream();
			}
			finally
			{
				Request.Dispose();
			}
		}

		private void CloseStream()
		{
			this.client?.DisposeWhenDone();
			this.client = null;
		}

		private void SendResponse(HttpRequest Request, HttpException ex, bool CloseAfterTransmission,
			params KeyValuePair<string, string>[] HeaderFields)
		{
			using (HttpResponse Response = new HttpResponse(this.client, this, this.server, Request)
			{
				StatusCode = ex.StatusCode,
				StatusMessage = ex.Message,
				ContentLength = null,
				ContentType = null,
				ContentLanguage = null
			})
			{
				foreach (KeyValuePair<string, string> P in HeaderFields)
					Response.SetHeader(P.Key, P.Value);

				if (CloseAfterTransmission)
				{
					Response.CloseAfterResponse = true;
					Response.SetHeader("Connection", "close");
				}

				if (ex is null)
					Response.SendResponse();
				else
					Response.SendResponse(ex);
			}
		}

		internal void Upgrade(WebSocket Socket)
		{
			this.mode = ConnectionMode.WebSocket;
			this.webSocket = Socket;
		}

		/// <summary>
		/// Checks if the connection is live.
		/// </summary>
		/// <returns>If the connection is still live.</returns>
		internal bool CheckLive()
		{
			try
			{
				if (this.disposed)
					return false;

				if (!this.client.Connected)
					return false;

#if WINDOWS_UWP
				return true;
#else

				// https://msdn.microsoft.com/en-us/library/system.net.sockets.socket.connected.aspx

				Socket Socket = this.client.Client.Client;
				bool BlockingBak = Socket.Blocking;
				try
				{
					byte[] Temp = new byte[1];

					Socket.Blocking = false;
					Socket.Send(Temp, 0, 0);

					return true;
				}
				catch (SocketException ex)
				{
					int Win32ErrorCode = ex.HResult & 0xFFFF;

					if (Win32ErrorCode == 10035)    // WSAEWOULDBLOCK
						return true;
					else
						return false;
				}
				finally
				{
					Socket.Blocking = BlockingBak;
				}
#endif
			}
			catch (Exception)
			{
				return false;
			}
		}

	}
}
