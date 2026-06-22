using System;
using System.Buffers;
using MeridianServerLib.EncodingLayer.Interfaces;
using MeridianServerLib.LogsLayer.Interfaces;
using MessagePack;

namespace MeridianServerLib.EncodingLayer.Encoders
{
	public class MessagePackEncoder<T> : IBinaryEncoder<T> where T : struct
	{
		public byte[] Serialize(T data, ILogger logger = null)
		{
			return MessagePackSerializer.Serialize(data);
		}

		public void Serialize(IBufferWriter<byte> writer, T data, ILogger logger = null)
		{
			MessagePackSerializer.Serialize(writer, data);
		}

		public T Deserialize(ReadOnlyMemory<byte> data, ILogger logger = null)
		{
			return MessagePackSerializer.Deserialize<T>(data);
		}

		public T[] DeserializeAll(ReadOnlyMemory<byte> data, ILogger logger = null)
		{
			return new[] { Deserialize(data, logger) };
		}
	}
}
