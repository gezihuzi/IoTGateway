﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Waher.Networking.CoAP.LWM2M
{
	/// <summary>
	/// LWM2M states.
	/// </summary>
	public enum Lwm2mState
	{
		/// <summary>
		/// In bootstrap handshake.
		/// </summary>
		Bootstrap,

		/// <summary>
		/// In registration handshake.
		/// </summary>
		Registration,

		/// <summary>
		/// In normal operation
		/// </summary>
		Operation,

		/// <summary>
		/// Deregistered
		/// </summary>
		Deregistered
	}

	/// <summary>
	/// Determines which UDP payload security mode is used.
	/// </summary>
	public enum SecurityMode
	{
		/// <summary>
		/// Pre-Shared Key mode 
		/// </summary>
		PSK = 0,

		/// <summary>
		/// Raw Public Key mode 
		/// </summary>
		RawPK = 1,

		/// <summary>
		/// Certificate mode 
		/// </summary>
		Certificate = 2,

		/// <summary>
		/// NoSec mode 
		/// </summary>
		NoSec = 3,

		/// <summary>
		/// Certificate mode with EST 
		/// </summary>
		CertificateEst = 4
	}

	/// <summary>
	/// Determines which SMS security mode is used.
	/// </summary>
	public enum SmsSecurityMode
	{
		/// <summary>
		/// DTLS mode (Device terminated) PSK mode assumed.
		/// </summary>
		Dtls = 1,

		/// <summary>
		/// Secure Packet Structure mode (Smartcard terminated).
		/// </summary>
		SecurePacketStructureMode = 2,

		/// <summary>
		/// NoSec mode.
		/// </summary>
		NoSec = 3
	}
}
