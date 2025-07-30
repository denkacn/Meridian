using MeridianServerLib.EncodingLayer.Tools;
using MeridianServerLib.LogsLayer.Interfaces;
using System;

namespace MeridianServerLib.EncodingLayer.Componators
{
	public class HeaderReSocketMessageComponator : ISocketMessageComponator
	{
		public event Action<byte[]> OnReceivedMessage;

		private const int HeaderSize = 10;
		private const char StartSymbol = '@';

		private readonly ILogger _logger;
		private ReReceivedMessage _receivedMessage;

		public HeaderReSocketMessageComponator(ILogger logger = null)
		{
			_logger = logger;
		}

		public byte[] CreateMessageWitchHeader(int messageId, byte[] message)
		{
			var header = ByteArrayHelper.Combine(
				BitConverter.GetBytes(StartSymbol),			//2
				BitConverter.GetBytes(messageId),						//4
				BitConverter.GetBytes(HeaderSize + message.Length)		//4
			);

			var result = ByteArrayHelper.Combine(header, message);

			_logger?.Log($"[HeaderSocketMessageComponator] Created message with length {result.Length}");

			return result;
		}

		public void Received(byte[] buffer, long offset, long size)
		{
			var startSymbol = BitConverter.ToChar(buffer, 0);
			var messageId = BitConverter.ToInt32(buffer, 2);
			var expectedMessageSize = BitConverter.ToInt32(buffer, 6);

			var messageBytes = new byte[size];
			Buffer.BlockCopy(buffer, 0, messageBytes, 0, (int)size);

			_logger?.Log($"[HeaderSocketMessageComponator] Received startSymbol: {startSymbol}, size: {size}, expected size: {expectedMessageSize}, buffer length: {messageBytes.Length}");

			if (_receivedMessage == null)
			{
				ProcessInitialMessage(buffer, size, expectedMessageSize, startSymbol);
			}
			else
			{
				ProcessContinuedMessage(buffer, size);
			}

			if (_receivedMessage?.IsReady == true)
			{
				_receivedMessage = null;
			}
		}

		private void ProcessInitialMessage(byte[] buffer, long size, int expectedMessageSize, char startSymbol)
		{
			if (expectedMessageSize == size)
			{
				TrySendReceivedMessage(buffer);
			}
			else if (expectedMessageSize < size)
			{
				SplitAndProcessMessages(buffer, (int)size, expectedMessageSize);
			}
			else if (startSymbol == StartSymbol)
			{
				_receivedMessage = new ReReceivedMessage(buffer, 0, expectedMessageSize);
			}
		}

		private void ProcessContinuedMessage(byte[] buffer, long size)
		{
			if (size > _receivedMessage.NeedBytes)
			{
				var missingBytes = new byte[_receivedMessage.NeedBytes];
				Buffer.BlockCopy(buffer, 0, missingBytes, 0, _receivedMessage.NeedBytes);

				_receivedMessage.Add(missingBytes);
				TrySendReceivedMessage(_receivedMessage.Buffer);

				var remainingBytes = new byte[size - _receivedMessage.NeedBytes];
				Buffer.BlockCopy(buffer, _receivedMessage.NeedBytes, remainingBytes, 0, remainingBytes.Length);
				Received(remainingBytes, 0, remainingBytes.Length);
			}
			else
			{
				_receivedMessage.Add(buffer);

				if (_receivedMessage.IsReady)
				{
					TrySendReceivedMessage(_receivedMessage.Buffer);
				}
			}
		}

		private void SplitAndProcessMessages(byte[] buffer, int size, int correctMessageSize)
		{
			var firstMessage = new byte[correctMessageSize];
			Buffer.BlockCopy(buffer, 0, firstMessage, 0, correctMessageSize);

			TrySendReceivedMessage(firstMessage);

			var remainingSize = size - correctMessageSize;
			var remainingMessage = new byte[remainingSize];
			Buffer.BlockCopy(buffer, correctMessageSize, remainingMessage, 0, remainingSize);

			Received(remainingMessage, 0, remainingSize);
		}

		private void TrySendReceivedMessage(byte[] buffer)
		{
			var messageLength = buffer.Length - HeaderSize;
			var message = new byte[messageLength];
			Buffer.BlockCopy(buffer, HeaderSize, message, 0, messageLength);

			OnReceivedMessage?.Invoke(message);
		}
	}

	public class ReReceivedMessage
	{
		public byte[] Buffer { get; private set; }
		public int Offset { get; }
		public int Size { get; }

		public bool IsReady => Buffer.Length == Size;
		public int NeedBytes => Size - Buffer.Length;

		public ReReceivedMessage(byte[] buffer, int offset, int size)
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
