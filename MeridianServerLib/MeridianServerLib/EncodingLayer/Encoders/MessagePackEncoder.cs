using MeridianServerLib.EncodingLayer.Interfaces;
using MeridianServerLib.LogsLayer.Interfaces;
using MessagePack;

namespace MeridianServerLib.EncodingLayer.Encoders
{
	public class MessagePackEncoder<T> : IBinaryEncoder<T> where T : struct
	{
		public byte[] Decode(T data, ILogger logger = null)
		{
			return MessagePackSerializer.Serialize(data);
		}

		public T Encode(byte[] data, ILogger logger = null)
		{
			return MessagePackSerializer.Deserialize<T>(data);
		}

		public T[] EncodeAll(byte[] data, ILogger logger = null)
		{
			return new[] { Encode(data, logger) };
		}
	}
}
