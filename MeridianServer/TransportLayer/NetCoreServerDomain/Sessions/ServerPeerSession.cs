using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using MeridianServerLib.EncodingLayer.Componators;
using MeridianServerLib.EncodingLayer.Convertors;
using MeridianServerLib.EncodingLayer.DataObjects;
using MeridianServerLib.EncodingLayer.Encoders;
using MeridianServerLib.EncodingLayer.Interfaces;
using MeridianServerLib.Exceptions;
using MeridianServerLib.Interfaces.Server;
using MeridianServerLib.LogsLayer.Interfaces;
using MeridianServerLib.Models.Operations;
using NetCoreServer;

namespace MeridianServer.TransportLayer.NetCoreServerDomain.Sessions
{
    public class ServerPeerSession : TcpSession, IServerPeerSession
    {
        public event EventHandler ConnectedEventHandler;
        public event EventHandler DisconnectedEventHandler;
        public event EventHandler<OperationData> ReceivedEventHandler;
        public event EventHandler<string> ErrorEventHandler;

        public string SessionId => Id.ToString();
        
        private readonly IBinaryEncoder<RpcData> _encoder;
        private readonly RpcDataConvertor _convertor;

        private readonly string _id;
        private bool _isBusy = false;
        private int _messageId = 0;

        private readonly Queue<OperationData> _operations;

        private readonly ISocketMessageComponator _socketMessageComponator;

        public ServerPeerSession(string id, TcpServer server, ILogger logger) : base(server)
        {
            SetLogger(logger);

            _id = id;
			_operations = new Queue<OperationData>();
            _encoder = new MessagePackEncoder<RpcData>();
            _convertor = new RpcDataConvertor();

            _socketMessageComponator = new HeaderSocketMessageComponatorV3(logger);

            _socketMessageComponator.OnReceivedMessage += OnSocketMessageComponatorReceivedMessage;
        }

        public void Send(OperationData operationData)
        {
            _operations.Enqueue(operationData);

            CheckOperationDataQueue();
        }

        public void Update()
        {
	        if (!IsSocketConnected()) Disconnect();
        }

        protected override void OnConnected(object userToken)
        {
            _logger?.Log($"[NetworkSession] ({_id}) Session with Id {Id} connected! UserToken: " + userToken);

            ConnectedEventHandler?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnDisconnected()
        {
            _logger?.Log($"[NetworkSession] ({_id}) Session with Id {Id} disconnected!");
            
            DisconnectedEventHandler?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            try
            {
                _logger?.Log($"[ServerPeerSession MeridianEncoderException] ({_id}) OnReceived: {buffer.Length} size: {size}");

                _socketMessageComponator.Received(buffer, (int)offset, (int)size);
            }
            catch (MeridianEncoderException ex)
            {
                _logger?.LogError($"[ServerPeerSession MeridianEncoderException] ({_id}) Error Received", ex);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[ServerPeerSession] ({_id}) Error On Received", ex);
            }
        }

        private void OnSocketMessageComponatorReceivedMessage(byte[] message)
        {        
            try
            {
                var dataPack = _encoder.Encode(message, _logger);
                var operationData = _convertor.From(dataPack);
				_ = ReceivedEventAsync(operationData);
			}
            catch (MeridianEncoderException ex)
            {
                _logger?.LogError($"[ServerPeerSession MeridianEncoderException] ({_id}) Error Received", ex);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[ServerPeerSession] ({_id}) Error On Received", ex);
            }       
        }

        protected override void OnEmpty()
        {
            _isBusy = false;

            CheckOperationDataQueue();
        }

        protected override void OnError(SocketError error)
        {
            _logger?.Log($"[NetworkSession] ({_id}) OnError Session caught an error with code {error}");

            ErrorEventHandler?.Invoke(this, error.ToString());
        }

        private void CheckOperationDataQueue()
        {
            if (!_isBusy && _operations.Count > 0)
            {
                DecodeAndSend(_operations.Dequeue());
            }
        }

        private void DecodeAndSend(OperationData operationData)
        {
            try
            {
                _isBusy = true;
                _messageId++;

                var packData = _convertor.To(operationData);
                var sendBytes = _encoder.Decode(packData, _logger);
                var sendBytesWithHeader = _socketMessageComponator.CreateMessageWithHeader(_messageId, sendBytes);

                //_logger.Log("[ServerPeerSession MeridianEncoder] Send " + operationData.OperationCode + " Length: " + sendBytesWithHeader.Length);

                SendAsync(sendBytesWithHeader);
            }
            catch (MeridianEncoderException ex)
            {
                _logger?.LogError($"[ServerPeerSession MeridianEncoderException] ({_id}) Error Send", ex);
                _isBusy = false;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[ServerPeerSession Exception] ({_id}) Error Send", ex);
                _isBusy = false;
            }
        }

        private async Task ReceivedEventAsync(OperationData operationData)
        {
	        ReceivedEventHandler?.Invoke(this, operationData);
	        //await Task.Run(() => { ReceivedEventHandler?.Invoke(this, operationData); });  
        }
    }
}