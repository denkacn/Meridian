using System;
using System.Threading;
using System.Threading.Tasks;
using MeridianServerLib.Models.Operations;
using MeridianRequestSystem.RequestSystem.Client;
using MeridianRequestSystem.RequestSystem.Helpers;
using MeridianRequestSystem.RequestSystem.Interfaces;

namespace MeridianRequestSystem.RequestSystem.Server
{
    public class MeridianRequestSystem : IMeridianRequestSystem
    {
        private readonly IServerRequestCreator _serverRequestCreator;
        private readonly INetworkSender _networkSender;

        public MeridianRequestSystem(
            IServerRequestCreator serverRequestCreator,
            INetworkSender networkSender)
        {
            _serverRequestCreator = serverRequestCreator ?? throw new ArgumentNullException(nameof(serverRequestCreator));
            _networkSender = networkSender ?? throw new ArgumentNullException(nameof(networkSender));
        }

        public Task HandleIncomingAsync(
            OperationData messageData,
            IUserClient client,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }

            var request = _serverRequestCreator.CreateServerDataNetworkRequest(messageData.Parameters);
            if (request == null)
            {
                return Task.CompletedTask;
            }

            if (request.IsAsync)
            {
                var completionSource = new TaskCompletionSource<IDataNetworkResponse>();

                request.Execute(client, response =>
                {
                    if (response == null)
                    {
                        completionSource.TrySetResult(null);
                        return;
                    }

                    completionSource.TrySetResult(response);
                });

                return SendAsyncResponseAsync(messageData.OperationCode, completionSource.Task, cancellationToken);
            }

            var dataResponse = request.Execute(client);
            SendResponse(messageData.OperationCode, dataResponse);
            return Task.CompletedTask;
        }

        private async Task SendAsyncResponseAsync(
            byte operationCode,
            Task<IDataNetworkResponse> responseTask,
            CancellationToken cancellationToken)
        {
            var response = await responseTask.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            SendResponse(operationCode, response);
        }

        private void SendResponse(byte operationCode, IDataNetworkResponse response)
        {
            if (response == null) return;

            var responsePackage = RequestMapper.GetFieldAttributeData(response);
            _networkSender.Send(operationCode, responsePackage, true);
        }
    }
}
