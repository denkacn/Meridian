using System;

namespace MeridianServerLib.Exceptions
{
    public class MeridianExternalLogicException : Exception
    {
        public MeridianExternalLogicException() { }

        public MeridianExternalLogicException(string message) : base(message) { }

        public MeridianExternalLogicException(string message, Exception inner) : base(message, inner) { }
    }
}
