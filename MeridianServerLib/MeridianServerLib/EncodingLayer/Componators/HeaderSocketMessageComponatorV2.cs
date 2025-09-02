using MeridianServerLib.EncodingLayer.Tools;
using MeridianServerLib.LogsLayer.Interfaces;
using System;
using System.Buffers.Binary;

namespace MeridianServerLib.EncodingLayer.Componators
{
	[Obsolete("HeaderSocketMessageComponatorV3", true)]
	public class HeaderSocketMessageComponatorV2 : ISocketMessageComponator
	{
		public event Action<byte[]> OnReceivedMessage;

		private const int HeaderSize = 10;
		private const byte StartSymbol = (byte)'@';

		private ReceivedMessage _receivedMessage;
		private readonly ILogger _logger;

		public HeaderSocketMessageComponatorV2(ILogger logger = null)
		{
			_logger = logger;
		}

		public byte[] CreateMessageWithHeader(int messageId, byte[] message)
		{
			var totalSize = HeaderSize + message.Length;
			var result = new byte[totalSize];

			// Стартовый байт (1) + паддинг (1)
			result[0] = StartSymbol;
			result[1] = 0; // резерв

			// ID (4 байта)
			BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(2, 4), messageId);

			// Длина (4 байта)
			BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(6, 4), totalSize);

			// Payload
			Buffer.BlockCopy(message, 0, result, HeaderSize, message.Length);

			_logger?.Log($"[HeaderSocketMessageComponator] CreateMessageWithHeader {result.Length}");

			return result;
		}

		public void Received(byte[] buffer, int offset, int size)
		{
			var span = new ReadOnlySpan<byte>(buffer, offset,size);

			while (span.Length > 0)
			{
				if (_receivedMessage == null)
				{
					if (span.Length < HeaderSize)
					{
						// Недостаточно байт для заголовка
						_receivedMessage = new ReceivedMessage(span.ToArray(), HeaderSize);
						return;
					}

					// Парсим заголовок
					var startSymbol = span[0];
					if (startSymbol != StartSymbol)
					{
						_logger?.Log("Invalid start symbol");
						return;
					}

					var id = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(2, 4));
					var totalSize = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(6, 4));

					if (totalSize < HeaderSize)
					{
						_logger?.Log("Invalid message size");
						return;
					}

					if (span.Length >= totalSize)
					{
						// Целое сообщение в буфере
						var fullMessage = span.Slice(0, totalSize).ToArray();
						HandleFullMessage(fullMessage);
						span = span.Slice(totalSize);
					}
					else
					{
						// Часть сообщения
						var partial = span.ToArray();
						_receivedMessage = new ReceivedMessage(partial, totalSize);
						return;
					}
				}
				else
				{
					var needed = _receivedMessage.NeedBytes;
					var take = Math.Min(needed, span.Length);

					_receivedMessage.Add(span.Slice(0, take));
					span = span.Slice(take);

					if (_receivedMessage.IsReady)
					{
						HandleFullMessage(_receivedMessage.Buffer);
						_receivedMessage = null;
					}
				}
			}
		}

		private void HandleFullMessage(byte[] buffer)
		{
			var messageLength = buffer.Length - HeaderSize;
			var message = new byte[messageLength];
			Buffer.BlockCopy(buffer, HeaderSize, message, 0, messageLength);

			_logger?.Log($"[HeaderSocketMessageComponator] Received message {message.Length} bytes");
			OnReceivedMessage?.Invoke(message);
		}

		public class ReceivedMessage
		{
			public byte[] Buffer { get; private set; }
			private int _position;
			public int Size { get; }

			public bool IsReady => _position >= Size;
			public int NeedBytes => Size - _position;

			public ReceivedMessage(byte[] initial, int totalSize)
			{
				Buffer = new byte[totalSize];
				_position = initial.Length;
				Size = totalSize;

				System.Buffer.BlockCopy(initial, 0, Buffer, 0, initial.Length);
			}

			public void Add(ReadOnlySpan<byte> data)
			{
				data.CopyTo(Buffer.AsSpan(_position));
				_position += data.Length;
			}
		}
	}
}
