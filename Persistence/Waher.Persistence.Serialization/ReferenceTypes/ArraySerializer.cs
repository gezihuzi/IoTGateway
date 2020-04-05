﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Waher.Persistence.Serialization.ReferenceTypes
{
	/// <summary>
	/// Generic serializer of array types.
	/// </summary>
	/// <typeparam name="T">Element type.</typeparam>
	public class ArraySerializer<T> : GeneratedObjectSerializerBase
	{
		private readonly ISerializerContext context;

		/// <summary>
		/// Generic serializer of array types.
		/// </summary>
		/// <param name="Context">Serialization context.</param>
		public ArraySerializer(ISerializerContext Context)
		{
			this.context = Context;
		}

		/// <summary>
		/// What type of object is being serialized.
		/// </summary>
		public override Type ValueType
		{
			get
			{
				return typeof(T[]);
			}
		}

		/// <summary>
		/// If the value being serialized, can be null.
		/// </summary>
		public override bool IsNullable
		{
			get { return true; }
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

			return ReadArray<T>(this.context, Reader, DataType.Value);
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
			if (Value is null)
			{
				if (!WriteTypeCode)
					throw new NullReferenceException("Value cannot be null.");

				Writer.WriteBits(ObjectSerializer.TYPE_NULL, 6);
			}
			else
			{
				T[] Array = (T[])Value;
				Type LastType = typeof(T);
				IObjectSerializer S = this.context.GetObjectSerializer(LastType);
				Type ItemType;
				bool Nullable;

				Writer.WriteBits(ObjectSerializer.TYPE_ARRAY, 6);
				Writer.WriteVariableLengthUInt64((ulong)Array.Length);

				if (Nullable = S.IsNullable)
					Writer.WriteBits(ObjectSerializer.TYPE_NULL, 6);
				else
					Writer.WriteBits(ObjectSerializer.GetFieldDataTypeCode(LastType), 6);

				foreach (T Item in Array)
				{
					if (Item == null)
					{
						if (Nullable)
							Writer.WriteBits(ObjectSerializer.TYPE_NULL, 6);
						else
							throw new Exception("Elements cannot be null.");
					}
					else
					{
						ItemType = Item.GetType();
						if (ItemType != LastType)
						{
							S = this.context.GetObjectSerializer(ItemType);
							LastType = ItemType;
						}

						S.Serialize(Writer, Nullable, true, Item);
					}
				}
			}
		}

		/// <summary>
		/// Gets the value of a field or property of an object, given its name.
		/// </summary>
		/// <param name="FieldName">Name of field or property.</param>
		/// <param name="Object">Object.</param>
		/// <param name="Value">Corresponding field or property value, if found, or null otherwise.</param>
		/// <returns>If the corresponding field or property was found.</returns>
		public override bool TryGetFieldValue(string FieldName, object Object, out object Value)
		{
			Value = null;
			return false;
		}

	}
}
