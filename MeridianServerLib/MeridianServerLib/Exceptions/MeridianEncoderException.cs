using System;

namespace MeridianServerLib.Exceptions
{
    public class MeridianEncoderException : Exception
    {
        public MeridianEncoderException() { }

        public MeridianEncoderException(string message) : base(message) { }

        public MeridianEncoderException(string message, Exception inner) : base(message, inner) { }
    }
}
