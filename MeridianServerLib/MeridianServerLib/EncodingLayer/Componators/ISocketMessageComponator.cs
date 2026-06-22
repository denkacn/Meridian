using System;

namespace MeridianServerLib.EncodingLayer.Componators
{
    public interface ISocketMessageComponator : IDisposable
    {
        event Action<byte[]> OnReceivedMessage;
        byte[] CreateMessageWithHeader(int messageId, byte[] message);
        void Received(byte[] buffer, int offset, int size);
    }
}
