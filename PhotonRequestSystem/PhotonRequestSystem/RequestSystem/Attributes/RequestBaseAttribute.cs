using System;

namespace PhotonRequestSystem.RequestSystem.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class RequestBaseAttribute : Attribute
    {
        public byte CommandId;
        public bool IsNecessarily;

        public RequestBaseAttribute(byte commandId, bool isNecessarily)
        {
            CommandId = commandId;
            IsNecessarily = isNecessarily;
        }
    }
}
