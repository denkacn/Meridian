using MeridianServerLib.Models.Operations;

namespace MeridianServerLib.Interfaces.Client
{
    public interface IOperationsReceiver
    {
        void OnOperationReceived(OperationData operation);
    }
}