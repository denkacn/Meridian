using System;

namespace PhotonRequestSystem.RequestSystem.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class RequestFieldAttribute : Attribute
    {
        public byte IndexId;
        public Type PackingProviderType;

        public RequestFieldAttribute(byte indexId, Type packingProviderType = null)
        {
            IndexId = indexId;
            PackingProviderType = packingProviderType;
        }
    }
}
