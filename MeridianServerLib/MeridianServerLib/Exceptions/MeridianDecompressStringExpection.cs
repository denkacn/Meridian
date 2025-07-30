using System;


namespace MeridianServerLib.Exceptions
{
    public class MeridianDecompressStringExpection : Exception
    {
        public MeridianDecompressStringExpection() { }

        public MeridianDecompressStringExpection(string message) : base(message) { }

        public MeridianDecompressStringExpection(string message, Exception inner) : base(message, inner) { }
    }
}
