using System;
using System.Buffers;

namespace MeridianServerLib.EncodingLayer.Componators
{
    public interface ISocketMessageComponator : IDisposable
    {
        event Action<byte[]> OnReceivedMessage;
        byte[] CreateMessageWithHeader(int messageId, byte[] message);
        ReadOnlyMemory<byte> CreateMessageWithHeader(int messageId, Action<IBufferWriter<byte>> writePayload);
        void Received(byte[] buffer, int offset, int size);
    }
}
