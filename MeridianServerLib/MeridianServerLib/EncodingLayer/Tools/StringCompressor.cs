using K4os.Compression.LZ4;
using System.Text;

namespace MeridianServerLib.EncodingLayer.Tools
{
    public static class StringCompressor
    {
        public static byte[] CompressString(string value)
        {
	        return !string.IsNullOrEmpty(value) ? LZ4Pickler.Pickle(Encoding.UTF8.GetBytes(value)) : new byte[] { };
        }

        public static string DecompressString(byte[] value)
        {
	        return value.Length > 0 ? Encoding.UTF8.GetString(LZ4Pickler.Unpickle(value)) : string.Empty;
        }
    }
}