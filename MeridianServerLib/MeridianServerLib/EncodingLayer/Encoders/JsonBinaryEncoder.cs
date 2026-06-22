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
    public class JsonBinaryEncoder<T> : IBinaryEncoder<T> where T : class
    {
        public byte[] Serialize(T data, ILogger logger = null)
        {
            try
            {
                var json = JsonConvert.SerializeObject(data);
                return StringCompressor.CompressString(json);
            }
            catch (Exception ex)
            {
                throw new MeridianEncoderException("[JsonBinaryEncoder] Serialize Error ", ex);
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
            try
            {
                var json = StringCompressor.DecompressString(data.ToArray());

                logger?.Log("Deserialize: " + json);

                return JsonConvert.DeserializeObject<T>(json, new DictionaryConversionRules());
            }
            catch (Exception ex)
            {
                throw new MeridianEncoderException("[JsonBinaryEncoder] Deserialize Error ", ex);
            }
        }

        public T[] DeserializeAll(ReadOnlyMemory<byte> data, ILogger logger = null)
        {
            throw new NotImplementedException();
        }
    }
}
