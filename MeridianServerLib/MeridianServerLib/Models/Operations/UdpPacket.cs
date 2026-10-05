using System;
using System.Buffers.Binary;

namespace MeridianServerLib.Models.Operations
{
    public readonly ref struct UdpPacket
    {
        public const int HeaderSize = 24;
        public const int MaxDatagramSize = 1200;
        public const byte Hello = 1;
        public const byte Data = 2;
        public const byte ReliableData = 3;
        public const byte Ack = 4;
        public const byte HelloAck = 5;

        public byte Kind { get; }
        public ReadOnlySpan<byte> Token { get; }
        public uint Sequence { get; }
        public ReadOnlyMemory<byte> Payload { get; }

        private UdpPacket(byte kind, ReadOnlySpan<byte> token, uint sequence, ReadOnlyMemory<byte> payload)
        {
            Kind = kind;
            Token = token;
            Sequence = sequence;
            Payload = payload;
        }

        public static void WriteHeader(Span<byte> destination, byte kind, ReadOnlySpan<byte> token, uint sequence)
        {
            if (destination.Length < HeaderSize) throw new ArgumentException("UDP header buffer is too small.", nameof(destination));
            if (token.Length != 16) throw new ArgumentException("A UDP token must contain 16 bytes.", nameof(token));
            destination[0] = (byte)'M';
            destination[1] = (byte)'U';
            destination[2] = 1;
            destination[3] = kind;
            token.CopyTo(destination.Slice(4, 16));
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(20, 4), sequence);
        }

        public static byte[] Create(byte kind, byte[] token, uint sequence, byte[] payload = null)
        {
            var payloadLength = payload?.Length ?? 0;
            if (HeaderSize + payloadLength > MaxDatagramSize)
                throw new ArgumentException("UDP datagram exceeds the size limit.", nameof(payload));
            var bytes = new byte[HeaderSize + payloadLength];
            WriteHeader(bytes.AsSpan(0, HeaderSize), kind, token, sequence);
            if (payloadLength != 0) Buffer.BlockCopy(payload, 0, bytes, HeaderSize, payloadLength);
            return bytes;
        }

        public static bool TryParse(ReadOnlyMemory<byte> datagram, out UdpPacket packet)
        {
            packet = default;
            var bytes = datagram.Span;
            if (bytes.Length < HeaderSize || bytes.Length > MaxDatagramSize) return false;
            if (bytes[0] != (byte)'M' || bytes[1] != (byte)'U' || bytes[2] != 1) return false;
            var kind = bytes[3];
            if (kind < Hello || kind > HelloAck) return false;
            if (kind != Data && kind != ReliableData && bytes.Length != HeaderSize) return false;
            if ((kind == Data || kind == ReliableData) && bytes.Length == HeaderSize) return false;
            packet = new UdpPacket(
                kind,
                bytes.Slice(4, 16),
                BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(20, 4)),
                datagram.Slice(HeaderSize));
            return true;
        }
    }
}
