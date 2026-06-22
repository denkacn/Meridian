using System;
using System.Buffers;
using MeridianServerLib.EncodingLayer.Encoders.JsonConverters;
using MeridianServerLib.EncodingLayer.Interfaces;
using MeridianServerLib.EncodingLayer.Tools;
using MeridianServerLib.Exceptions;
using MeridianServerLib.LogsLayer.Interfaces;
using Newtonsoft.Json;

namespace MeridianServerLib.EncodingLayer.Encoders
{
	public class JsonBinaryListEncoder<T> : IBinaryEncoder<T> where T : struct
	{
		private const string MESSAGE_SEPARATOR = "[:end:]";

		public byte[] Serialize(T data, ILogger logger = null)
		{
			try
			{
				var json = JsonConvert.SerializeObject(data);
				logger?.Log($"Serialize: {json}");

				return StringCompressor.CompressString(json + MESSAGE_SEPARATOR);
			}
			catch (Exception ex)
			{
				logger?.LogError("[JsonBinaryListEncoder] Serialize Error", ex);
				throw new MeridianEncoderException("[JsonBinaryListEncoder] Serialize Error", ex);
			}
		}

		public void Serialize(IBufferWriter<byte> writer, T data, ILogger logger = null)
		{
			var bytes = Serialize(data, logger);
			var span = writer.GetSpan(bytes.Length);
			bytes.CopyTo(span);
			writer.Advance(bytes.Length);
		}

		public T Deserialize(ReadOnlyMemory<byte> data, ILogger logger = null)
		{
			throw new NotSupportedException("Use DeserializeAll for batch decoding.");
		}

		public T[] DeserializeAll(ReadOnlyMemory<byte> data, ILogger logger = null)
		{
			string json;

			try
			{
				json = StringCompressor.DecompressString(data.ToArray());
			}
			catch (Exception ex)
			{
				logger?.LogError("[JsonBinaryListEncoder] DeserializeAll Decompress Error", ex);
				throw new MeridianDecompressStringException(
					"[JsonBinaryListEncoder] DeserializeAll Decompress Error", ex);
			}

			try
			{
				logger?.Log($"DeserializeAll: {json}");

				if (json.EndsWith(MESSAGE_SEPARATOR))
					json = json.Remove(json.Length - MESSAGE_SEPARATOR.Length);

				var jsonObjects = json.Split(new[] { MESSAGE_SEPARATOR }, StringSplitOptions.RemoveEmptyEntries);

				logger?.Log($"jsonObjects count: {jsonObjects.Length}");

				var result = new T[jsonObjects.Length];
				for (var i = 0; i < jsonObjects.Length; i++)
				{
					result[i] = JsonConvert.DeserializeObject<T>(jsonObjects[i], new DictionaryConversionRules());
				}

				return result;
			}
			catch (Exception ex)
			{
				logger?.LogError("[JsonBinaryListEncoder] DeserializeAll Error", ex);
				throw new MeridianEncoderException("[JsonBinaryListEncoder] DeserializeAll Error", ex);
			}
		}
	}
}
