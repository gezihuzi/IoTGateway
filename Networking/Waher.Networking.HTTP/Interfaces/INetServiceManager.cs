using System;
using Windows.Networking.Sockets;

namespace Waher.Networking.HTTP
{
	/// <summary>
	/// NetService for server
	/// </summary>
	public interface INetServiceManager
	{
		/// <summary>
		///  register service
		/// </summary>
		bool RegisterService(StreamSocketListener listener);

		/// <summary>
		/// unregister service
		/// </summary>
		bool UnregisterService(StreamSocketListener listener);
	}
}