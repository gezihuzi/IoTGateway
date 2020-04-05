﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Waher.Persistence.Serialization.NullableTypes
{
	/// <summary>
	/// Serializes a nullable <see cref="Char"/> value.
	/// </summary>
	public class NullableCharSerializer : NullableValueTypeSerializer
	{
		/// <summary>
		/// Serializes a nullable <see cref="Char"/> value.
		/// </summary>
		public NullableCharSerializer()
		{
		}

		/// <summary>
		/// What type of object is being serialized.
		/// </summary>
		public override Type ValueType
		{
			get
			{
				return typeof(char?);
			}
		}

		/// <summary>
		/// Deserializes an object from a binary source.
		/// </summary>
		/// <param name="Reader">Deserializer.</param>
		/// <param name="DataType">Optional datatype. If not provided, will be read from the binary source.</param>
		/// <param name="Embedded">If the object is embedded into another.</param>
		/// <returns>Deserialized object.</returns>
		public override object Deserialize(IDeserializer Reader, uint? DataType, bool Embedded)
		{
			if (!DataType.HasValue)
				DataType = Reader.ReadBits(6);

			switch (DataType.Value)
			{
				case ObjectSerializer.TYPE_CHAR: return (char?)Reader.ReadChar();
				case ObjectSerializer.TYPE_BYTE: return (char?)Reader.ReadByte();
				case ObjectSerializer.TYPE_INT16: return (char?)Reader.ReadInt16();
				case ObjectSerializer.TYPE_INT32: return (char?)Reader.ReadInt32();
				case ObjectSerializer.TYPE_INT64: return (char?)Reader.ReadInt64();
				case ObjectSerializer.TYPE_SBYTE: return (char?)Reader.ReadSByte();
				case ObjectSerializer.TYPE_UINT16: return (char?)Reader.ReadUInt16();
				case ObjectSerializer.TYPE_UINT32: return (char?)Reader.ReadUInt32();
				case ObjectSerializer.TYPE_UINT64: return (char?)Reader.ReadUInt64();
				case ObjectSerializer.TYPE_DECIMAL: return (char?)Reader.ReadDecimal();
				case ObjectSerializer.TYPE_DOUBLE: return (char?)Reader.ReadDouble();
				case ObjectSerializer.TYPE_SINGLE: return (char?)Reader.ReadSingle();
				case ObjectSerializer.TYPE_MIN: return char.MinValue;
				case ObjectSerializer.TYPE_MAX: return char.MaxValue;
				case ObjectSerializer.TYPE_NULL: return null;

				case ObjectSerializer.TYPE_STRING:
				case ObjectSerializer.TYPE_CI_STRING:
					string s = Reader.ReadString();
					return string.IsNullOrEmpty(s) ? (char?)0 : s[0];

				default: throw new Exception("Expected a nullable char value.");
			}
		}

		/// <summary>
		/// Serializes an object to a binary destination.
		/// </summary>
		/// <param name="Writer">Serializer.</param>
		/// <param name="WriteTypeCode">If a type code is to be output.</param>
		/// <param name="Embedded">If the object is embedded into another.</param>
		/// <param name="Value">The actual object to serialize.</param>
		public override void Serialize(ISerializer Writer, bool WriteTypeCode, bool Embedded, object Value)
		{
			char? Value2 = (char?)Value;

			if (WriteTypeCode)
			{
				if (!Value2.HasValue)
				{
					Writer.WriteBits(ObjectSerializer.TYPE_NULL, 6);
					return;
				}
				else
					Writer.WriteBits(ObjectSerializer.TYPE_CHAR, 6);
			}
			else if (!Value2.HasValue)
				throw new NullReferenceException("Value cannot be null.");

			Writer.Write(Value2.Value);
		}

	}
}
