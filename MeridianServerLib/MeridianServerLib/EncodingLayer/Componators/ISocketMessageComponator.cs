using System;
using MeridianServerLib.EncodingLayer.Interfaces;
using MeridianServerLib.LogsLayer.Interfaces;

namespace MeridianServerLib.EncodingLayer.Componators
{
    public interface ISocketMessageComponator : IDisposable
    {
        event Action<byte[]> OnReceivedMessage;
        byte[] CreateMessageWithHeader(int messageId, byte[] message);
        ReadOnlyMemory<byte> CreateMessageWithHeader<T>(int messageId, T message, IBinaryEncoder<T> encoder, ILogger logger = null);
        void Received(byte[] buffer, int offset, int size);
    }
}
