using System;


namespace MeridianServerLib.Exceptions
{
    public class MeridianDecompressStringException : Exception
    {
        public MeridianDecompressStringException() { }

        public MeridianDecompressStringException(string message) : base(message) { }

        public MeridianDecompressStringException(string message, Exception inner) : base(message, inner) { }
    }
}
