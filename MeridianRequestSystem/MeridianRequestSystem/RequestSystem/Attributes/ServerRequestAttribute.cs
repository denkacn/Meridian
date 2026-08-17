using System;
using MeridianRequestSystem.RequestSystem.Interfaces;

namespace MeridianRequestSystem.RequestSystem.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ServerRequestAttribute : Attribute
    {
        public Type RequestType { get; }

        public ServerRequestAttribute(Type requestType)
        {
            if (requestType == null)
            {
                throw new ArgumentNullException(nameof(requestType));
            }

            if (!typeof(IDataNetworkRequest).IsAssignableFrom(requestType))
            {
                throw new ArgumentException("Server request attribute target must implement IDataNetworkRequest.", nameof(requestType));
            }

            RequestType = requestType;
        }
    }
}
