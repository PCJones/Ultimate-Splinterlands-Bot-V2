﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace HiveAPI.CS
{
	class CSerializer
	{
		private void WriteVarint32(MemoryStream oStream, UInt32 n)
		{
			while( n >= 0x80)
			{
				oStream.WriteByte(Convert.ToByte(0x80 | (n & 0x7F)));
				n = n >> 7;
			}
			oStream.WriteByte(Convert.ToByte(n));
		}
		private void AddToStream(MemoryStream oStream, Object obj)
		{
			byte[] buf;

			if (obj == null) return;
			switch(obj)
			{
				case bool value:
					oStream.WriteByte((byte)(value ? 1 : 0));
					break;
				case Byte value:
					oStream.WriteByte(value);
					break;
				case Int16 value:
					buf = BitConverter.GetBytes(value);
					oStream.Write(buf, 0, buf.Length);
					break;
				case UInt16 value:
					buf = BitConverter.GetBytes(value);
					oStream.Write(buf, 0, buf.Length);
					break;
				case Int32 value:
					buf = BitConverter.GetBytes(value);
					oStream.Write(buf, 0, buf.Length);
					break;
				case UInt32 value:
					buf = BitConverter.GetBytes(value);
					oStream.Write(buf, 0, buf.Length);
					break;
				case Int64 value:
					buf = BitConverter.GetBytes(value);
					oStream.Write(buf, 0, buf.Length);
					break;
				case UInt64 value:
					buf = BitConverter.GetBytes(value);
					oStream.Write(buf, 0, buf.Length);
					break;
				case float value:
					buf = BitConverter.GetBytes(value);
					oStream.Write(buf, 0, buf.Length);
					break;
				case double value:
					buf = BitConverter.GetBytes(value);
					oStream.Write(buf, 0, buf.Length);
					break;

				case byte[] value:
					oStream.Write(value, 0, value.Length);
					break;
				case string value:
					if(string.IsNullOrEmpty(value)) {
						oStream.WriteByte(0);
						break;
					}
					buf = System.Text.Encoding.UTF8.GetBytes(value);
					UInt32 len = Convert.ToUInt32(buf.Length);
					WriteVarint32(oStream, len);
					oStream.Write(buf, 0, (int)len);
					break;
				case DateTime value:
					buf = BitConverter.GetBytes(Convert.ToUInt32(value.Ticks / 10000000 - 62135596800)); //01.01.1970
					oStream.Write(buf, 0, buf.Length);
					break;
				case Asset value:
					int precision = value.GetPrecision();
					buf = BitConverter.GetBytes(Convert.ToUInt64(Math.Round((double)value.amount * Math.Pow(10, precision))));
					oStream.Write(buf, 0, buf.Length);
					oStream.WriteByte(Convert.ToByte(precision));
					// Convert HIVE & HBD to STEEM & SBD because the blockchain serialization is still based on STEEM & SBD

					string strSymbol = value.symbol == "HIVE" ? "STEEM" : value.symbol == "HBD" ? "SBD" : value.symbol;
					for (int i = 0; i <= 6; i++)
					{
						oStream.WriteByte(i < strSymbol.Length ? (byte)strSymbol[i] : (byte)0);
					}
					break;
				case PublicKey value:
					if (value==null || value.key=="STM1111111111111111111111111111111114T1Anm")
					{
						buf = new Byte[33];
						oStream.Write(buf, 0, 33);
					}
					else
					{
						buf = value.Decode();
						oStream.Write(buf, 0, buf.Length);
					}
					break;
				case object[] value:
					WriteVarint32(oStream, (UInt32)value.Length);
					if (value.Length == 0) break;
					foreach (object item in value)
					{
						Serialize(oStream, item);
					}
					break;
				case Dictionary<PublicKey, UInt16> value:
					WriteVarint32(oStream, (UInt32)value.Count);
					if (value.Count==0) break;
					foreach (KeyValuePair<PublicKey, UInt16> item in value) {
						Serialize(oStream, item);
					}
					break;

				case Dictionary<string, UInt16> value:
					WriteVarint32(oStream, (UInt32)value.Count);
					if (value.Count == 0) break;
					foreach (KeyValuePair<string, UInt16> item in value)
					{
						Serialize(oStream, item);
					}
					break;

				case Object value:
					Serialize(oStream, obj);
					break;

				default:
					throw new NotImplementedException();
			}
		}

		public Byte[] Serialize(Object obj)
		{
			using (MemoryStream oStream = new MemoryStream())
			{
				Serialize(oStream, obj);
				return oStream.ToArray();
			}
		}
		public void Serialize(MemoryStream oStream, Object obj)
		{
			Type oType = obj.GetType();
			if (obj is COperations.IOperationID)
			{
				WriteVarint32(oStream, (UInt32)((COperations.IOperationID)obj).opid);
			}
			if (oType.Namespace == "System")
				AddToStream(oStream, obj);
			else
			{
				FieldInfo[] oFields = oType.Name== "KeyValuePair`2" ? (FieldInfo[])oType.GetTypeInfo().DeclaredFields : oType.GetFields();
				foreach(FieldInfo oField in oFields)
				{
					if (!(oType.Name == "CTransaction" && (oField.Name == "signatures" || oField.Name == "txid"))) {
						if (oField.FieldType.Namespace == "System.Collections.Generic" && oField.GetValue(obj) == null)
							WriteVarint32(oStream, 0);
						else
						{
							if (Attribute.IsDefined(oField, typeof(OptionalField)))
							{
								if (oField.GetValue(obj) == null)
								{
									oStream.WriteByte(0);
									continue;
								}
								else
									oStream.WriteByte(1);
							}
							if (oField.GetValue(obj) == null)
								throw new ArgumentNullException(oField.Name);
							AddToStream(oStream, oField.GetValue(obj));
						}
					}
				}
			}
		}
	}
}
