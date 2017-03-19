﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Waher.Content;
using Waher.Networking.HTTP;
using Waher.Networking.XMPP.InBandBytestreams;

namespace Waher.Networking.XMPP.HTTPX
{
	internal class HttpxResponse : TransferEncoding
	{
		private static Dictionary<string, HttpxResponse> activeStreams = new Dictionary<string, HttpxResponse>();

		private StringBuilder response = new StringBuilder();
		private XmppClient client;
		private IEndToEndEncryption e2e;
		private IbbClient ibbClient;
		private string id;
		private string to;
		private string from;
		private int maxChunkSize;
		private bool? chunked = null;
		private int nr = 0;
		private string streamId = null;
		private byte[] chunk = null;
		private OutgoingStream ibbOutput = null;
		private int pos;
		private bool cancelled = false;

		public HttpxResponse(XmppClient Client, IEndToEndEncryption E2e, string Id, string To, string From, int MaxChunkSize, 
			IbbClient IbbClient) : base()
		{
			this.client = Client;
			this.e2e = E2e;
			this.ibbClient = IbbClient;
			this.id = Id;
			this.to = To;
			this.from = From;
			this.maxChunkSize = MaxChunkSize;
		}

		private void AssertNotCancelled()
		{
			if (this.cancelled)
				throw new IOException("Stream cancelled.");
		}

		public override void BeforeContent(HttpResponse Response, bool ExpectContent)
		{
			this.response.Append("<resp xmlns='");
			this.response.Append(HttpxClient.Namespace);
			this.response.Append("' version='1.1' statusCode='");
			this.response.Append(Response.StatusCode.ToString());
			this.response.Append("' statusMessage='");
			this.response.Append(XML.Encode(Response.StatusMessage));
			this.response.Append("'><headers xmlns='");
			this.response.Append(HttpxClient.NamespaceHeaders);
			this.response.Append("'>");

			if (Response.ContentLength.HasValue)
				this.chunked = (Response.ContentLength.Value > this.maxChunkSize);
			else if (ExpectContent)
				this.chunked = true;
			else
			{
				if ((Response.StatusCode < 100 || Response.StatusCode > 199) && Response.StatusCode != 204 && Response.StatusCode != 304)
					this.response.Append("<header name='Content-Length'>0</header>");
			}

			foreach (KeyValuePair<string, string> P in Response.GetHeaders())
			{
				this.response.Append("<header name='");
				this.response.Append(XML.Encode(P.Key));
				this.response.Append("'>");
				this.response.Append(XML.Encode(P.Value));
				this.response.Append("</header>");
			}

			this.response.Append("</headers>");

			if (this.chunked.HasValue)
			{
				if (this.chunked.Value)
				{
					this.streamId = Guid.NewGuid().ToString().Replace("-", string.Empty);

					if (this.ibbClient != null)
					{
						this.response.Append("<data><ibb sid='");
						this.response.Append(this.streamId);
						this.response.Append("'/></data>");
						this.ReturnResponse();

						this.ibbOutput = this.ibbClient.OpenStream(this.to, this.maxChunkSize, this.streamId);
						this.ibbOutput.OnAbort += this.IbbOutput_OnAbort;
					}
					else
					{
						this.chunk = new byte[this.maxChunkSize];

						this.response.Append("<data><chunkedBase64 streamId='");
						this.response.Append(this.streamId);
						this.response.Append("'/></data>");
						this.ReturnResponse();
					}

					lock (activeStreams)
					{
						activeStreams[this.from + " " + this.streamId] = this;
					}
				}
				else
					this.response.Append("<data><base64>");
			}
			else
				this.ReturnResponse();
		}

		private void IbbOutput_OnAbort(object sender, EventArgs e)
		{
			this.Cancel();
		}

		public override void ContentSent()
		{
			this.ReturnResponse();

			if (this.chunked.HasValue && this.chunked.Value)
			{
				lock (activeStreams)
				{
					activeStreams.Remove(this.from + " " + this.streamId);
				}

				if (this.ibbOutput != null)
					this.ibbOutput.Close();
				else
					this.SendChunk(true);
			}
		}

		private void ReturnResponse()
		{
			this.AssertNotCancelled();

			if (this.response != null)
			{
				if (this.chunked.HasValue && !this.chunked.Value)
					this.response.Append("</base64></data>");

				this.response.Append("</resp>");

				if (this.e2e != null)
					this.e2e.SendIqResult(this.client, E2ETransmission.IgnoreIfNotE2E, this.id, this.to, this.response.ToString());
				else
					this.client.SendIqResult(this.id, this.to, this.response.ToString());

				this.response = null;
			}
		}

		public override bool Decode(byte[] Data, int Offset, int NrRead, out int NrAccepted)
		{
			throw new InternalServerErrorException();   // Will not be called.
		}

		public override void Encode(byte[] Data, int Offset, int NrBytes)
		{
			this.AssertNotCancelled();

			if (this.chunked.Value)
			{
				if (this.ibbOutput != null)
					this.ibbOutput.Write(Data, Offset, NrBytes);
				else
				{
					int NrLeft = this.maxChunkSize - this.pos;

					while (NrBytes > 0)
					{
						if (NrBytes <= NrLeft)
						{
							Array.Copy(Data, Offset, this.chunk, this.pos, NrBytes);
							this.pos += NrBytes;
							NrBytes = 0;

							if (this.pos >= this.maxChunkSize)
								this.SendChunk(false);
						}
						else
						{
							Array.Copy(Data, Offset, this.chunk, this.pos, NrLeft);
							this.pos += NrLeft;
							Offset += NrLeft;
							NrBytes -= NrLeft;
							this.SendChunk(false);
							NrLeft = this.maxChunkSize;
						}
					}
				}
			}
			else
				this.response.Append(Convert.ToBase64String(Data, Offset, NrBytes, Base64FormattingOptions.None));
		}

		private void SendChunk(bool Last)
		{
			if (this.client.State != XmppState.Connected)
				this.Cancel();

			this.AssertNotCancelled();

			StringBuilder Xml = new StringBuilder();

			Xml.Append("<chunk xmlns='");
			Xml.Append(HttpxClient.Namespace);
			Xml.Append("' streamId='");
			Xml.Append(this.streamId);
			Xml.Append("' nr='");
			Xml.Append(this.nr.ToString());

			if (Last)
			{
				Xml.Append("' last='true");

				lock (activeStreams)
				{
					activeStreams.Remove(this.from + " " + this.streamId);
				}
			}

			Xml.Append("'>");

			if (this.pos > 0)
				Xml.Append(Convert.ToBase64String(this.chunk, 0, this.pos, Base64FormattingOptions.None));

			Xml.Append("</chunk>");

			ManualResetEvent ChunkSent = new ManualResetEvent(false);

			this.client.SendMessage(QoSLevel.Unacknowledged, MessageType.Normal, this.to, Xml.ToString(), string.Empty, string.Empty, string.Empty,
				string.Empty, string.Empty, (sender, e) =>
				{
					try
					{
						ChunkSent.Set();
					}
					catch (Exception)
					{
						// Ignore.
					}
				}, null);


			ChunkSent.WaitOne(1000);    // Limit read speed to rate at which messages can be sent to the network.
			ChunkSent.Close();

			this.nr++;
			this.pos = 0;
		}

		public override void Flush()
		{
			this.ReturnResponse();

			if (this.pos > 0)
				this.SendChunk(false);
		}

		public void Cancel()
		{
			this.cancelled = true;

			if (this.chunked.HasValue && this.chunked.Value)
			{
				lock (activeStreams)
				{
					activeStreams.Remove(this.from + " " + this.streamId);
				}
			}
		}

		public static void CancelChunkedTransfer(string To, string From, string StreamId)
		{
			HttpxResponse Response;
			string Key = From + " " + StreamId;

			lock (activeStreams)
			{
				if (activeStreams.TryGetValue(Key, out Response))
				{
					if (Response.to == To)
					{
						activeStreams.Remove(Key);
						Response.cancelled = true;
					}
				}
			}
		}
	}
}
