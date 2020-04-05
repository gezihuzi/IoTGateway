﻿using System;
using Waher.Persistence.Attributes;
using Waher.Security;

namespace Waher.Networking.XMPP.Avatar
{
	/// <summary>
	/// Contains information about an avatar.
	/// </summary>
	[CollectionName("Avatars")]
	[Index("BareJid")]
	public class Avatar
	{
		private string objectId = null;
		private string bareJid = string.Empty;
		private string hash = string.Empty;
		private string contentType = string.Empty;
		private byte[] binary = null;
		private int width = 0;
		private int height = 0;

		/// <summary>
		/// Contains information about an avatar.
		/// </summary>
		public Avatar()
		{
		}

		/// <summary>
		/// Contains information about an avatar.
		/// </summary>
		/// <param name="BareJid">Bare JID related to the avatar.</param>
		/// <param name="ContentType">Content-Type of the avatar image.</param>
		/// <param name="Binary">Binary encoding of the image.</param>
		/// <param name="Width">Width of avatar, in pixels.</param>
		/// <param name="Height">Height of avatar, in pixels.</param>
		public Avatar(string BareJid, string ContentType, byte[] Binary, int Width, int Height)
		{
			this.bareJid = BareJid;
			this.contentType = ContentType;
			this.binary = Binary;
			this.hash = Hashes.ComputeSHA1HashString(Binary);
			this.width = Width;
			this.height = Height;
		}

		/// <summary>
		/// Contains information about an avatar.
		/// </summary>
		/// <param name="BareJid">Bare JID related to the avatar.</param>
		/// <param name="ContentType">Content-Type of the avatar image.</param>
		/// <param name="Hash">Hash of the avatar image.</param>
		/// <param name="Binary">Binary encoding of the image.</param>
		/// <param name="Width">Width of avatar, in pixels.</param>
		/// <param name="Height">Height of avatar, in pixels.</param>
		public Avatar(string BareJid, string ContentType, string Hash, byte[] Binary, int Width, int Height)
		{
			this.bareJid = BareJid;
			this.contentType = ContentType;
			this.binary = Binary;
			this.hash = Hash;
			this.width = Width;
			this.height = Height;
		}

		/// <summary>
		/// Object ID of avatar.
		/// </summary>
		[ObjectId]
		public string ObjectId
		{
			get { return this.objectId; }
			set { this.objectId = value; }
		}

		/// <summary>
		/// Bare JID of avatar.
		/// </summary>
		public string BareJid
		{
			get { return this.bareJid; }
			set { this.bareJid = value; }
		}

		/// <summary>
		/// Hash digest of avatar.
		/// </summary>
		[DefaultValueStringEmpty]
		public string Hash
		{
			get { return this.hash; }
			set { this.hash = value; }
		}

		/// <summary>
		/// Content-Type of binary encoding of avatar.
		/// </summary>
		[DefaultValueStringEmpty]
		public string ContentType
		{
			get { return this.contentType; }
			set { this.contentType = value; }
		}

		/// <summary>
		/// Binary encoding of avatar.
		/// </summary>
		[DefaultValueNull]
		public byte[] Binary
		{
			get { return this.binary; }
			set { this.binary = value; }
		}

		/// <summary>
		/// Width of avatar, in pixels. 0 = No width provided.
		/// </summary>
		[DefaultValue(0)]
		public int Width
		{
			get { return this.width; }
			set { this.width = value; }
		}

		/// <summary>
		/// Height of avatar, in pixels. 0 = No height provided.
		/// </summary>
		[DefaultValue(0)]
		public int Height
		{
			get { return this.height; }
			set { this.height = value; }
		}
	}
}
