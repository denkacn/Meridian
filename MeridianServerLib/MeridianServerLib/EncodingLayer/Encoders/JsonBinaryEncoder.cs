using MeridianServerLib.EncodingLayer.Encoders.JsonConverters;
using MeridianServerLib.EncodingLayer.Interfaces;
using MeridianServerLib.EncodingLayer.Tools;
using MeridianServerLib.Exceptions;
using MeridianServerLib.LogsLayer.Interfaces;
using Newtonsoft.Json;
using System;

namespace MeridianServerLib.EncodingLayer.Encoders
{
    public class JsonBinaryEncoder<T> : IBinaryEncoder<T> where T : class
    {
        public byte[] Decode(T data, ILogger logger = null)
        {
            try
            {
                var json = JsonConvert.SerializeObject(data);
                return StringCompressor.CompressString(json);
            }
            catch (Exception ex) 
            {
                throw new MeridianEncoderException("[JsonBinaryEncoder] Decode Error ", ex);
            }         
        } 

        public T Encode(byte[] data, ILogger logger = null)
        {
            try
            {
                var json = StringCompressor.DecompressString(data);

                logger?.Log("Encode: " + json);

                return JsonConvert.DeserializeObject<T>(json, new DictionaryConversionRules());
            }
            catch (Exception ex)
            {
                throw new MeridianEncoderException("[JsonBinaryEncoder] Encode Error ", ex);
            }            
        }

        public T[] EncodeAll(byte[] data, ILogger logger = null)
        {
            throw new NotImplementedException();
        }
    }
}