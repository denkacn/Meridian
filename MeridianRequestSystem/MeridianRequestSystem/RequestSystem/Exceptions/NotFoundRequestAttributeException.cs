using System;

namespace MeridianRequestSystem.RequestSystem.Exceptions
{
    public class NotFoundRequestAttributeException : Exception
    {
        public NotFoundRequestAttributeException() { }

        public NotFoundRequestAttributeException(string message)
            : base(message) { }

        public NotFoundRequestAttributeException(string message, Exception inner)
            : base(message, inner) { }
    }
}
