using MeridianServerLib.EncodingLayer.Tools;
using MeridianServerLib.LogsLayer.Interfaces;
using System;

namespace MeridianServerLib.EncodingLayer.Componators
{
	public class HeaderReSocketMessageComponator : ISocketMessageComponator
	{
		public event Action<byte[]> OnReceivedMessage;

		private const int HeaderSize = 10;
		private static readonly char StartSymbol = '@';
		private ReceivedMessage _receivedMessage;
		private readonly ILogger _logger;

		public HeaderReSocketMessageComponator(ILogger logger = null)
		{
			_logger = logger;
		}

		/// <summary>
		/// Создает сообщение с заголовком.
		/// </summary>
		public byte[] CreateMessageWithHeader(int messageId, byte[] message)
		{
			var header = CreateHeader(messageId, message.Length);
			var result = ByteArrayHelper.Combine(header, message);

			_logger?.Log($"[HeaderSocketMessageComponator] CreateMessageWithHeader {result.Length}");

			return result;
		}

		/// <summary>
		/// Обрабатывает входящий буфер.
		/// </summary>
		public void Received(byte[] buffer, long offset, long size)
		{
			if (buffer == null || buffer.Length < HeaderSize || size < HeaderSize)
			{
				_logger?.LogError("[HeaderSocketMessageComponator] Buffer too small or null", null);
				return;
			}

			var span = new ReadOnlySpan<byte>(buffer, (int)offset, (int)size);
			var (startSymbol, messageId, correctMessageSize) = ParseHeader(span);

			if (startSymbol != StartSymbol)
			{
				_logger?.LogError("[HeaderSocketMessageComponator] Invalid start symbol", null);
				return;
			}

			var messageBytes = span.Slice(0, (int)size).ToArray();

			_logger?.Log($"[HeaderSocketMessageComponator] Received startSymbol: {startSymbol} size: {size} messageSize: {correctMessageSize} messageBytes Length: {messageBytes.Length}");

			if (_receivedMessage == null)
			{
				if (correctMessageSize == size)
				{
					TrySendReceivedMessage(messageBytes);
				}
				else if (correctMessageSize < size)
				{
					var firstMessageBytes = span.Slice(0, correctMessageSize).ToArray();
					TrySendReceivedMessage(firstMessageBytes);

					ProcessRemainderMessage(span, correctMessageSize);
				}
				else // correctMessageSize > size
				{
					_receivedMessage = new ReceivedMessage(messageBytes, correctMessageSize);
				}
			}
			else
			{
				var needBytes = _receivedMessage.NeedBytes;
				if (size > needBytes)
				{
					var missingMessageBytes = span.Slice(0, needBytes).ToArray();
					_receivedMessage.Add(missingMessageBytes);
					TrySendReceivedMessage(_receivedMessage.Buffer);

					ProcessRemainderMessage(span, needBytes);
				}
				else
				{
					_receivedMessage.Add(messageBytes);
					if (_receivedMessage.IsReady)
						TrySendReceivedMessage(_receivedMessage.Buffer);
				}
			}

			if (_receivedMessage != null && _receivedMessage.IsReady)
				_receivedMessage = null;
		}

		/// <summary>
		/// Парсит заголовок сообщения.
		/// </summary>
		private (char startSymbol, int messageId, int messageSize) ParseHeader(ReadOnlySpan<byte> buffer)
		{
			char startSymbol = BitConverter.ToChar(buffer.Slice(0, 2));
			int messageId = BitConverter.ToInt32(buffer.Slice(2, 4));
			int messageSize = BitConverter.ToInt32(buffer.Slice(6, 4));
			return (startSymbol, messageId, messageSize);
		}

		/// <summary>
		/// Создает заголовок сообщения.
		/// </summary>
		private byte[] CreateHeader(int messageId, int messageLength)
		{
			byte[] packStart = BitConverter.GetBytes(StartSymbol);    //2
			byte[] packId = BitConverter.GetBytes(messageId);         //4
			byte[] packLength = BitConverter.GetBytes(HeaderSize + messageLength); //4
			return ByteArrayHelper.Combine(packStart, packId, packLength);
		}

		/// <summary>
		/// Обрабатывает остаток буфера после первого сообщения.
		/// </summary>
		private void ProcessRemainderMessage(ReadOnlySpan<byte> buffer, int processedSize)
		{
			var nextMessageSize = buffer.Length - processedSize;
			if (nextMessageSize <= 0)
				return;

			var nextMessageBytes = buffer.Slice(processedSize, nextMessageSize).ToArray();
			Received(nextMessageBytes, 0, nextMessageSize);
		}

		/// <summary>
		/// Отправляет полученное сообщение без заголовка.
		/// </summary>
		private void TrySendReceivedMessage(byte[] buffer)
		{
			if (buffer.Length < HeaderSize)
				return;

			var messageLength = buffer.Length - HeaderSize;
			var message = new byte[messageLength];
			Buffer.BlockCopy(buffer, HeaderSize, message, 0, messageLength);

			OnReceivedMessage?.Invoke(message);
		}

		private class ReceivedMessage
		{
			public byte[] Buffer { get; private set; }
			public int Size { get; }
			public bool IsReady => Buffer.Length == Size;
			public int NeedBytes => Size - Buffer.Length;

			public ReceivedMessage(byte[] buffer, int size)
			{
				Buffer = buffer;
				Size = size;
			}

			public void Add(byte[] message)
			{
				Buffer = ByteArrayHelper.Combine(Buffer, message);
			}
		}
	}
}