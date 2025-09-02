using MeridianServerLib.EncodingLayer.Encoders.JsonConverters;
using MeridianServerLib.EncodingLayer.Interfaces;
using MeridianServerLib.EncodingLayer.Tools;
using MeridianServerLib.Exceptions;
using MeridianServerLib.LogsLayer.Interfaces;
using Newtonsoft.Json;
using System;

namespace MeridianServerLib.EncodingLayer.Encoders
{
	
	public class JsonBinaryListEncoder<T> : IBinaryEncoder<T> where T : struct
	{
		private const string MESSAGE_SEPARATOR = "[:end:]";

		public byte[] Decode(T data, ILogger logger = null)
		{
			//if (data == null)
				//throw new ArgumentNullException(nameof(data));

			try
			{
				var json = JsonConvert.SerializeObject(data);
				logger?.Log($"EncodeToBinary: {json}");

				return StringCompressor.CompressString(json + MESSAGE_SEPARATOR);
			}
			catch (Exception ex)
			{
				logger?.LogError("[JsonBinaryListEncoder] EncodeToBinary Error", ex);
				throw new MeridianEncoderException("[JsonBinaryListEncoder] EncodeToBinary Error", ex);
			}
		}

		public T Encode(byte[] data, ILogger logger = null)
		{
			throw new NotSupportedException("Use DecodeAllFromBinary for batch decoding.");
		}

		public T[] EncodeAll(byte[] data, ILogger logger = null)
		{
			if (data == null)
				throw new ArgumentNullException(nameof(data));

			string json;

			try
			{
				json = StringCompressor.DecompressString(data);
			}
			catch (Exception ex)
			{
				logger?.LogError("[JsonBinaryListEncoder] DecodeAllFromBinary Decompress Error", ex);
				throw new MeridianDecompressStringException(
					"[JsonBinaryListEncoder] DecodeAllFromBinary Decompress Error", ex);
			}

			try
			{
				logger?.Log($"DecodeAllFromBinary: {json}");

				if (json.EndsWith(MESSAGE_SEPARATOR))
					json = json.Remove(json.Length - MESSAGE_SEPARATOR.Length);

				var jsonObjects = json.Split(new[] { MESSAGE_SEPARATOR }, StringSplitOptions.RemoveEmptyEntries);

				logger?.Log($"jsonObjects count: {jsonObjects.Length}");

				var result = new T[jsonObjects.Length];
				for (var i = 0; i < jsonObjects.Length; i++)
				{
					var jsonObject = jsonObjects[i];
					result[i] = JsonConvert.DeserializeObject<T>(jsonObject, new DictionaryConversionRules());
				}

				return result;
			}
			catch (Exception ex)
			{
				logger?.LogError("[JsonBinaryListEncoder] DecodeAllFromBinary Error", ex);
				throw new MeridianEncoderException("[JsonBinaryListEncoder] DecodeAllFromBinary Error", ex);
			}
		}
	}
}
