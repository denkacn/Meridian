using MeridianServerLib.LogsLayer.Interfaces;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

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
		private bool _isDisposed;

		public HeaderSocketMessageComponatorV3(ILogger logger = null, int initialBufferSize = 64 * 1024)
		{
			_logger = logger;
			_receiveBuffer = ArrayPool<byte>.Shared.Rent(initialBufferSize);
			_bufferCount = 0;
		}

		public byte[] CreateMessageWithHeader(int messageId, byte[] message)
		{
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(HeaderSocketMessageComponatorV3));
			}

			var totalSize = HeaderSize + message.Length;
			var buffer = new byte[totalSize];

			WriteHeader(buffer.AsSpan(0, HeaderSize), messageId, totalSize);

			Buffer.BlockCopy(message, 0, buffer, HeaderSize, message.Length);

			//_logger?.Log($"[Componator] CreateMessageWithHeader {totalSize}");

			return buffer;
		}

		public ReadOnlyMemory<byte> CreateMessageWithHeader(int messageId, Action<IBufferWriter<byte>> writePayload)
		{
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(HeaderSocketMessageComponatorV3));
			}

			var writer = new ArrayBufferWriter<byte>();
			writer.Advance(HeaderSize);

			writePayload(writer);

			if (!MemoryMarshal.TryGetArray(writer.WrittenMemory, out var segment) || segment.Array == null)
			{
				throw new InvalidOperationException("Unable to access packet buffer.");
			}

			WriteHeader(segment.Array.AsSpan(segment.Offset, HeaderSize), messageId, writer.WrittenCount);

			return writer.WrittenMemory;
		}


		public void Received(byte[] data, int offset, int size)
		{
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(HeaderSocketMessageComponatorV3));
			}

			EnsureCapacity(_bufferCount + size);

			Buffer.BlockCopy(data, offset, _receiveBuffer, _bufferCount, size);
			_bufferCount += size;

			var readPos = 0;

			while (true)
			{
				if (_bufferCount - readPos < HeaderSize) break;

				if (_receiveBuffer[readPos] != StartSymbol)
				{
					//_logger?.Log("[Componator] Invalid start symbol, skip byte");
					readPos += 1;
					continue;
				}

				var messageId = BinaryPrimitives.ReadInt32LittleEndian(_receiveBuffer.AsSpan(readPos + 2, 4));
				var totalSize = BinaryPrimitives.ReadInt32LittleEndian(_receiveBuffer.AsSpan(readPos + 6, 4));

				if (totalSize < HeaderSize)
				{
					//_logger?.Log("[Componator] Invalid message size");
					readPos += 1;
					continue;
				}

				if (_bufferCount - readPos < totalSize) break;

				HandleFullMessage(messageId, readPos, totalSize);

				readPos += totalSize;
			}

			if (readPos > 0)
			{
				Buffer.BlockCopy(_receiveBuffer, readPos, _receiveBuffer, 0, _bufferCount - readPos);
				_bufferCount -= readPos;
			}
		}

		private void HandleFullMessage(int messageId, int messageOffset, int size)
		{
			var payloadLength = size - HeaderSize;
			var payload = new byte[payloadLength];
			Buffer.BlockCopy(_receiveBuffer, messageOffset + HeaderSize, payload, 0, payloadLength);

			//_logger?.Log($"[Componator] Received messageId={messageId}, size={payloadLength}");
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

		private static void WriteHeader(Span<byte> header, int messageId, int totalSize)
		{
			header[0] = StartSymbol;
			header[1] = 0;

			BinaryPrimitives.WriteInt32LittleEndian(header.Slice(2, 4), messageId);
			BinaryPrimitives.WriteInt32LittleEndian(header.Slice(6, 4), totalSize);
		}

		public void Dispose()
		{
			if (_isDisposed)
			{
				return;
			}

			OnReceivedMessage = null;

			if (_receiveBuffer != null)
			{
				ArrayPool<byte>.Shared.Return(_receiveBuffer);
				_receiveBuffer = null;
			}

			_bufferCount = 0;
			_isDisposed = true;
		}
	}
}
