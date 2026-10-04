using System;
using System.Buffers.Binary;

namespace MeridianServerLib.Models.Operations
{
    public sealed class UdpPacket
    {
        public const int HeaderSize = 24;
        public const int MaxDatagramSize = 1200;
        public const byte Hello = 1;
        public const byte Data = 2;
        public const byte ReliableData = 3;
        public const byte Ack = 4;
        public const byte HelloAck = 5;

        public byte Kind { get; }
        public string Token { get; }
        public uint Sequence { get; }
        public byte[] Payload { get; }

        private UdpPacket(byte kind, string token, uint sequence, byte[] payload)
        {
            Kind = kind;
            Token = token;
            Sequence = sequence;
            Payload = payload;
        }

        public static byte[] Create(byte kind, byte[] token, uint sequence, byte[] payload = null)
        {
            if (token == null || token.Length != 16) throw new ArgumentException("A UDP token must contain 16 bytes.", nameof(token));
            var length = payload?.Length ?? 0;
            if (HeaderSize + length > MaxDatagramSize) throw new ArgumentException("UDP datagram exceeds the size limit.", nameof(payload));
            var bytes = new byte[HeaderSize + length];
            bytes[0] = (byte)'M';
            bytes[1] = (byte)'U';
            bytes[2] = 1;
            bytes[3] = kind;
            Buffer.BlockCopy(token, 0, bytes, 4, 16);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20, 4), sequence);
            if (length > 0) Buffer.BlockCopy(payload, 0, bytes, HeaderSize, length);
            return bytes;
        }

        public static bool TryParse(byte[] bytes, int offset, int size, out UdpPacket packet)
        {
            packet = null;
            if (bytes == null || offset < 0 || size < HeaderSize || size > MaxDatagramSize || offset > bytes.Length - size) return false;
            if (bytes[offset] != (byte)'M' || bytes[offset + 1] != (byte)'U' || bytes[offset + 2] != 1) return false;
            var kind = bytes[offset + 3];
            if (kind < Hello || kind > HelloAck) return false;
            if (kind != Data && kind != ReliableData && size != HeaderSize) return false;
            if ((kind == Data || kind == ReliableData) && size == HeaderSize) return false;
            var token = new byte[16];
            Buffer.BlockCopy(bytes, offset + 4, token, 0, 16);
            var payload = new byte[size - HeaderSize];
            if (payload.Length != 0) Buffer.BlockCopy(bytes, offset + HeaderSize, payload, 0, payload.Length);
            packet = new UdpPacket(kind, Convert.ToBase64String(token),
                BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 20, 4)), payload);
            return true;
        }
    }
}
