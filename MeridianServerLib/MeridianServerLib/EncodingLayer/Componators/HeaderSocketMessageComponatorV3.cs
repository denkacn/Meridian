using MeridianServerLib.LogsLayer.Interfaces;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace MeridianServerLib.EncodingLayer.Componators
{
	public class HeaderSocketMessageComponatorV3 : ISocketMessageComponator
	{
		public event Action<ReadOnlyMemory<byte>> OnReceivedMessage;

		private const int HeaderSize = 12;
		private const byte MagicByte0 = (byte)'M';
		private const byte MagicByte1 = (byte)'R';
		private const byte ProtocolVersion = 1;
		private const byte Flags = 0;

		private readonly ILogger _logger;
		private readonly object _sendLock = new object();

		private byte[] _receiveBuffer;
		private int _bufferCount;
		private bool _isDisposed;
		private ArrayBufferWriter<byte> _sendWriter;

		public HeaderSocketMessageComponatorV3(ILogger logger = null, int initialBufferSize = 64 * 1024)
		{
			_logger = logger;
			_receiveBuffer = ArrayPool<byte>.Shared.Rent(initialBufferSize);
			_bufferCount = 0;
			_sendWriter = new ArrayBufferWriter<byte>(initialBufferSize);
		}

		public byte[] CreateMessageWithHeader(int messageId, byte[] message)
		{
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(HeaderSocketMessageComponatorV3));
			}

			var totalSize = HeaderSize + message.Length;
			var buffer = new byte[totalSize];

			WriteHeader(buffer.AsSpan(0, HeaderSize), messageId, message.Length);

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

			lock (_sendLock)
			{
				_sendWriter.Clear();
				_sendWriter.Advance(HeaderSize);

				writePayload(_sendWriter);

				if (!MemoryMarshal.TryGetArray(_sendWriter.WrittenMemory, out var segment) || segment.Array == null)
				{
					throw new InvalidOperationException("Unable to access packet buffer.");
				}

				WriteHeader(segment.Array.AsSpan(segment.Offset, HeaderSize), messageId, _sendWriter.WrittenCount - HeaderSize);

				return _sendWriter.WrittenMemory;
			}
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

				if (!IsValidHeaderStart(readPos))
				{
					//_logger?.Log("[Componator] Invalid packet magic, skip byte");
					readPos += 1;
					continue;
				}

				var version = _receiveBuffer[readPos + 2];
				if (version != ProtocolVersion)
				{
					//_logger?.Log("[Componator] Invalid protocol version");
					readPos += 1;
					continue;
				}

				var messageId = BinaryPrimitives.ReadInt32LittleEndian(_receiveBuffer.AsSpan(readPos + 4, 4));
				var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(_receiveBuffer.AsSpan(readPos + 8, 4));

				if (payloadLength < 0)
				{
					//_logger?.Log("[Componator] Invalid payload size");
					readPos += 1;
					continue;
				}

				var totalSize = HeaderSize + payloadLength;
				if (_bufferCount - readPos < totalSize) break;

				HandleFullMessage(messageId, readPos, payloadLength);

				readPos += totalSize;
			}

			if (readPos > 0)
			{
				Buffer.BlockCopy(_receiveBuffer, readPos, _receiveBuffer, 0, _bufferCount - readPos);
				_bufferCount -= readPos;
			}
		}

		private void HandleFullMessage(int messageId, int messageOffset, int payloadLength)
		{
			//_logger?.Log($"[Componator] Received messageId={messageId}, size={payloadLength}");
			OnReceivedMessage?.Invoke(_receiveBuffer.AsMemory(messageOffset + HeaderSize, payloadLength));
		}

		private void EnsureCapacity(int neededSize)
		{
			if (_receiveBuffer.Length >= neededSize) return;

			var newBuffer = ArrayPool<byte>.Shared.Rent(Math.Max(_receiveBuffer.Length * 2, neededSize));
			Buffer.BlockCopy(_receiveBuffer, 0, newBuffer, 0, _bufferCount);

			ArrayPool<byte>.Shared.Return(_receiveBuffer);
			_receiveBuffer = newBuffer;
		}

		private bool IsValidHeaderStart(int offset)
		{
			return _receiveBuffer[offset] == MagicByte0 && _receiveBuffer[offset + 1] == MagicByte1;
		}

		private static void WriteHeader(Span<byte> header, int messageId, int payloadLength)
		{
			header[0] = MagicByte0;
			header[1] = MagicByte1;
			header[2] = ProtocolVersion;
			header[3] = Flags;

			BinaryPrimitives.WriteInt32LittleEndian(header.Slice(4, 4), messageId);
			BinaryPrimitives.WriteInt32LittleEndian(header.Slice(8, 4), payloadLength);
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
			_sendWriter = null;
			}

			_bufferCount = 0;
			_isDisposed = true;
		}
	}
}
