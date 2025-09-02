using System;
using System.Collections.Generic;
using System.Text;

namespace MeridianServerLib.EncodingLayer.Componators
{
    public interface ISocketMessageComponator
    {
        event Action<byte[]> OnReceivedMessage;
        byte[] CreateMessageWithHeader(int messageId, byte[] message);
        void Received(byte[] buffer, long offset, long size);
    }
}
