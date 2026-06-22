using System;
using System.Buffers;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using MeridianServerLib.EncodingLayer.Interfaces;
using MeridianServerLib.LogsLayer.Interfaces;

namespace MeridianServerLib.EncodingLayer.Encoders
{
    [Obsolete]
    public class FormatterBinaryEncoder<T> : IBinaryEncoder<T> where T : class
    {
        public byte[] Serialize(T data, ILogger logger)
        {
            var binaryFormatter = new BinaryFormatter();
            using var memoryStream = new MemoryStream();
            binaryFormatter.Serialize(memoryStream, data);
            return memoryStream.ToArray();
        }

        public void Serialize(IBufferWriter<byte> writer, T data, ILogger logger = null)
        {
            var bytes = Serialize(data, logger);
            var span = writer.GetSpan(bytes.Length);
            bytes.CopyTo(span);
            writer.Advance(bytes.Length);
        }

        public T Deserialize(ReadOnlyMemory<byte> data, ILogger logger)
        {
            using var memoryStream = new MemoryStream(data.ToArray());
            var binaryFormatter = new BinaryFormatter();

            return (T)binaryFormatter.Deserialize(memoryStream);
        }

        public T[] DeserializeAll(ReadOnlyMemory<byte> data, ILogger logger = null)
        {
            throw new NotImplementedException();
        }
    }
}
