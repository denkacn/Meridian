using System;
using System.Text;

namespace MeridianServerLib.EncodingLayer.Tools
{
    public static class StringCompressor
    {
        public static byte[] CompressString(string value)
        {
            return value != "" ? CLZF.Compress(Encoding.UTF8.GetBytes(value)) : new byte[] { };
        }

        public static string DecompressString(Byte[] value)
        {
            return value.Length > 0 ? Encoding.UTF8.GetString(CLZF.Decompress(value)) : "";
        }
    }
}