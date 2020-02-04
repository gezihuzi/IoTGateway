using System;
using Windows.Networking.Sockets;

namespace Waher.Networking.HTTP
{
	/// <summary>
	/// NetService for server
	/// </summary>
	public interface IHttpNetServiceMethod
	{
		/// <summary>
		///  register service
		/// </summary>
		bool Register(StreamSocketListener listener);

		/// <summary>
		/// unregister service
		/// </summary>
		bool UnRegister(StreamSocketListener listener);
	}
}