using MeridianServerLib.EncodingLayer.Encoders.JsonConverters;
using MeridianServerLib.EncodingLayer.Interfaces;
using MeridianServerLib.EncodingLayer.Tools;
using MeridianServerLib.Exceptions;
using MeridianServerLib.LogsLayer.Interfaces;
using Newtonsoft.Json;
using System;

namespace MeridianServerLib.EncodingLayer.Encoders
{
    public class JsonBinaryListEncoder<T> : IBinaryEncoder<T> where T : class
    {
        private const string MESSAGE_SEPARATOR = "[:end:]";

        public byte[] Decode(T data, ILogger logger = null)
        {
            try
            {
                var json = JsonConvert.SerializeObject(data);
                
                logger?.Log("Decode: " + json);

                return StringCompressor.CompressString(json + MESSAGE_SEPARATOR);
            }
            catch (Exception ex)
            {
                throw new MeridianEncoderException("[JsonBinaryListEncoder] Decode Error ", ex);
            }
        }

        public T Encode(byte[] data, ILogger logger = null)
        {
            throw new NotImplementedException();
        }

        public T[] EncodeAll(byte[] data, ILogger logger = null)
        {
            string json = string.Empty;

            try
            {
                json = StringCompressor.DecompressString(data);
            }
            catch (Exception ex)
            {
                throw new MeridianDecompressStringExpection("[JsonBinaryListEncoder] EncodeAll Decompress String Error", ex);
            }

            try
            {

                logger?.Log("Encode: " + json);

                json = json.Substring(0, json.Length - 7);

                var jsonObjects = json.Split(MESSAGE_SEPARATOR);

                logger?.Log("jsonObjects: " + jsonObjects.Length);

                T[] encodeObjects = new T[jsonObjects.Length];

                for (int index = 0; index < jsonObjects.Length; index++)
                {
                    string jsonObject = jsonObjects[index];

                    var encodeObject = JsonConvert.DeserializeObject<T>(jsonObject, new DictionaryConversionRules());
                    encodeObjects[index] = encodeObject;
                }

                return encodeObjects;
            }
            catch (Exception ex)
            {
                throw new MeridianEncoderException("[JsonBinaryListEncoder] EncodeAll Error ", ex);
            }
        }
    }
}
