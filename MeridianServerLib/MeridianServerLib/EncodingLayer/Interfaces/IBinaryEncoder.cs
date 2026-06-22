using System;
using System.Buffers;
using MeridianServerLib.LogsLayer.Interfaces;

namespace MeridianServerLib.EncodingLayer.Interfaces
{
    public interface IBinaryEncoder<T>
    {
        byte[] Serialize(T data, ILogger logger = null);
        void Serialize(IBufferWriter<byte> writer, T data, ILogger logger = null);
        T Deserialize(ReadOnlyMemory<byte> data, ILogger logger = null);
        T[] DeserializeAll(ReadOnlyMemory<byte> data, ILogger logger = null);
    }
}
