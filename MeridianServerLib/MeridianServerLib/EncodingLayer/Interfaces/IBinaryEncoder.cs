using MeridianServerLib.LogsLayer.Interfaces;

namespace MeridianServerLib.EncodingLayer.Interfaces
{
    public interface IBinaryEncoder<T>
    {
        public byte[] Decode(T data, ILogger logger = null);
        public T Encode(byte[] data, ILogger logger = null);
        public T[] EncodeAll(byte[] data, ILogger logger = null);
    }
}