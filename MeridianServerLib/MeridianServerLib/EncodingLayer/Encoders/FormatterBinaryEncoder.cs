using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using MeridianServerLib.EncodingLayer.Interfaces;
using MeridianServerLib.LogsLayer.Interfaces;

namespace MeridianServerLib.EncodingLayer.Encoders
{
    public class FormatterBinaryEncoder<T> : IBinaryEncoder<T> where T : class
    {
        public byte[] Decode(T data, ILogger logger)
        {
            var bf = new BinaryFormatter();
            using var ms = new MemoryStream();
            bf.Serialize(ms, data);
            return ms.ToArray();
        }

        public T Encode(byte[] data, ILogger logger)
        {
            using var memStream = new MemoryStream();
            
            var binForm = new BinaryFormatter();
            memStream.Write(data, 0, data.Length);
            memStream.Seek(0, SeekOrigin.Begin);
            var obj = binForm.Deserialize(memStream);
                
            return (T)obj;
        }

        public T[] EncodeAll(byte[] data, ILogger logger = null)
        {
            throw new System.NotImplementedException();
        }
    }
}