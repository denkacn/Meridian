using MeridianServerLib.EncodingLayer.Tools;
using MeridianServerLib.LogsLayer.Interfaces;
using System;

namespace MeridianServerLib.EncodingLayer.Componators
{
    public class HeaderSocketMessageComponator : ISocketMessageComponator
    {
        public event Action<byte[]> OnReceivedMessage;

        private const int _headerSize = 10;

        private char _startSymbol = '@';
        private ReceivedMessage _receivedMessage;
        private ILogger _logger;

        public HeaderSocketMessageComponator(ILogger logger = null)
        {
            _logger = logger;
        }

        public byte[] CreateMessageWitchHeader(int messageId, byte[] message)
        {
            byte[] packStart = BitConverter.GetBytes(_startSymbol);    //2
            byte[] packId = BitConverter.GetBytes(messageId);    //4
            byte[] packLength = BitConverter.GetBytes(_headerSize + message.Length); //4

            var header = ByteArrayHelper.Combine(packStart, packId, packLength);
            var result = ByteArrayHelper.Combine(header, message);

            _logger?.Log("[HeaderSocketMessageComponator] CreateMessageWitchHeader " + result.Length);

            return result;
        }

        public void Received(byte[] buffer, long offset, long size)
        {
            var startSymbolBytes = new byte[2];
            var idBytes = new byte[4];
            var sizeBytes = new byte[4];

            Buffer.BlockCopy(buffer, 0, startSymbolBytes, 0, 2);                  
            Buffer.BlockCopy(buffer, 2, idBytes, 0, 4);     
            Buffer.BlockCopy(buffer, 6, sizeBytes, 0, 4);      

            var startSymbol = BitConverter.ToChar(startSymbolBytes);
            var id = BitConverter.ToInt32(idBytes);
            var correctMessageSize = BitConverter.ToInt32(sizeBytes);

            var messageBytes = new byte[size];
            Buffer.BlockCopy(buffer, 0, messageBytes, 0, (int)size);

            _logger?.Log("[HeaderSocketMessageComponator] Received startSymbol: " + startSymbol + " size: " + size + " messageSize" + correctMessageSize + " messageBytes Length: " + messageBytes.Length);

            if(_receivedMessage == null)
            {
                if (correctMessageSize == size)
                {
                    TrySendReceivedMessage(messageBytes);
                }
                else if (correctMessageSize < size)
                {
                    var firstMessageBytes = new byte[correctMessageSize];
                    Buffer.BlockCopy(buffer, 0, firstMessageBytes, 0, correctMessageSize);

                    TrySendReceivedMessage(firstMessageBytes);

                    ProcessRemainderMessage(buffer, (int)size, correctMessageSize);
                }
                else if (correctMessageSize > size)
                {
                    if (startSymbol == _startSymbol)
                    {
                        _receivedMessage = new ReceivedMessage(messageBytes, 0, correctMessageSize);
                    }
                }
            }
            else
            {
                if(size > _receivedMessage.NeedBytes)
                {
                    var missingMessageBytes = new byte[_receivedMessage.NeedBytes];
                    Buffer.BlockCopy(buffer, 0, missingMessageBytes, 0, _receivedMessage.NeedBytes);

                    _receivedMessage.Add(missingMessageBytes);
                    TrySendReceivedMessage(_receivedMessage.Buffer);

                    ProcessRemainderMessage(buffer, (int)size, _receivedMessage.NeedBytes);
                }
                else
                {
                    _receivedMessage.Add(messageBytes);

                    if (_receivedMessage.IsReady) TrySendReceivedMessage(_receivedMessage.Buffer);
                }
            }


            if (_receivedMessage != null && _receivedMessage.IsReady) _receivedMessage = null;
        }

        private void ProcessRemainderMessage(byte[] buffer, int size, int correctMessageSize)
        {
            var nextMessageSize = size - correctMessageSize;
            var nextMessageBytes = new byte[nextMessageSize];
            Buffer.BlockCopy(buffer, correctMessageSize, nextMessageBytes, 0, (int)nextMessageSize);

            Received(nextMessageBytes, 0, nextMessageSize);
        }

        private void TrySendReceivedMessage(byte[] buffer)
        {
            var messageLength = buffer.Length - _headerSize;
            var message = new byte[messageLength];

            Buffer.BlockCopy(buffer, _headerSize, message, 0, messageLength);

            OnReceivedMessage?.Invoke(message);
        }
    }

    public class ReceivedMessage
    {
        public byte[] Buffer;
        public int Offset;
        public int Size;

        public bool IsReady => Buffer.Length == Size;
        public int NeedBytes => Size - Buffer.Length;

        public ReceivedMessage(byte[] buffer, int offset, int size)
        {
            Buffer = buffer;
            Offset = offset;
            Size = size;
        }

        public void Add(byte[] message)
        {
            Buffer = ByteArrayHelper.Combine(Buffer, message);
        }
    }
}
