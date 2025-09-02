using MeridianServerLib.LogsLayer.Interfaces;
using System;
using System.Buffers;
using System.Buffers.Binary;

namespace MeridianServerLib.EncodingLayer.Componators
{
	public class HeaderSocketMessageComponatorV3 : ISocketMessageComponator
	{
		public event Action<byte[]> OnReceivedMessage;

		private const int HeaderSize = 10;
		private const byte StartSymbol = (byte)'@';

		private readonly ILogger _logger;

		private byte[] _receiveBuffer;
		private int _bufferCount;

		public HeaderSocketMessageComponatorV3(ILogger logger = null, int initialBufferSize = 64 * 1024)
		{
			_logger = logger;
			_receiveBuffer = ArrayPool<byte>.Shared.Rent(initialBufferSize);
			_bufferCount = 0;
		}

		public byte[] CreateMessageWithHeader(int messageId, byte[] message)
		{
			var totalSize = HeaderSize + message.Length;
			var buffer = new byte[totalSize];

			buffer[0] = StartSymbol;
			buffer[1] = 0;

			BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(2, 4), messageId);
			BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(6, 4), totalSize);

			Buffer.BlockCopy(message, 0, buffer, HeaderSize, message.Length);

			_logger?.Log($"[Componator] CreateMessageWithHeader {totalSize}");

			return buffer;
		}


		public void Received(byte[] data, int offset, int size)
		{
			EnsureCapacity(_bufferCount + size);

			Buffer.BlockCopy(data, offset, _receiveBuffer, _bufferCount, size);
			_bufferCount += size;

			var readPos = 0;

			while (true)
			{
				if (_bufferCount - readPos < HeaderSize) break;

				if (_receiveBuffer[readPos] != StartSymbol)
				{
					_logger?.Log("[Componator] Invalid start symbol, skip byte");
					readPos += 1;
					continue;
				}

				var messageId = BinaryPrimitives.ReadInt32LittleEndian(_receiveBuffer.AsSpan(readPos + 2, 4));
				var totalSize = BinaryPrimitives.ReadInt32LittleEndian(_receiveBuffer.AsSpan(readPos + 6, 4));

				if (totalSize < HeaderSize)
				{
					_logger?.Log("[Componator] Invalid message size");
					readPos += 1;
					continue;
				}

				if (_bufferCount - readPos < totalSize) break;

				var message = new byte[totalSize];
				Buffer.BlockCopy(_receiveBuffer, readPos, message, 0, totalSize);

				HandleFullMessage(messageId, message, totalSize);

				readPos += totalSize;
			}

			if (readPos > 0)
			{
				Buffer.BlockCopy(_receiveBuffer, readPos, _receiveBuffer, 0, _bufferCount - readPos);
				_bufferCount -= readPos;
			}
		}

		private void HandleFullMessage(int messageId, byte[] buffer, int size)
		{
			var payloadLength = size - HeaderSize;
			var payload = new byte[payloadLength];
			Buffer.BlockCopy(buffer, HeaderSize, payload, 0, payloadLength);

			_logger?.Log($"[Componator] Received messageId={messageId}, size={payloadLength}");
			OnReceivedMessage?.Invoke(payload);
		}

		private void EnsureCapacity(int neededSize)
		{
			if (_receiveBuffer.Length >= neededSize) return;

			var newBuffer = ArrayPool<byte>.Shared.Rent(Math.Max(_receiveBuffer.Length * 2, neededSize));
			Buffer.BlockCopy(_receiveBuffer, 0, newBuffer, 0, _bufferCount);

			ArrayPool<byte>.Shared.Return(_receiveBuffer);
			_receiveBuffer = newBuffer;
		}
	}
}
