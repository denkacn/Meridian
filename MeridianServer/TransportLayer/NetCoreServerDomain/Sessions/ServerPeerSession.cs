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

        private bool _isBusy = false;
        private int _messageId = 0;

        private readonly Queue<OperationData> _operationDatas;

        private ISocketMessageComponator _socketMessageComponator;

        public ServerPeerSession(TcpServer server, ILogger logger) : base(server)
        {
            SetLogger(logger);

            _operationDatas = new Queue<OperationData>();
            _encoder = new JsonBinaryListEncoder<RpcData>();
            _convertor = new RpcDataConvertor();

            _socketMessageComponator = new HeaderSocketMessageComponator(logger);

            _socketMessageComponator.OnReceivedMessage += OnSocketMessageComponatorReceivedMessage;
        }

        public void Send(OperationData operationData)
        {
            _operationDatas.Enqueue(operationData);

            CheckOperationDataQueue();
        }

        public void Update()
        {
	        if (!IsSocketConnected()) Disconnect();
        }

        protected override void OnConnected(object userToken)
        {
            _logger?.Log($"[NetworkSession] Session with Id {Id} connected! UserToken: " + userToken);

            ConnectedEventHandler?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnDisconnected()
        {
            _logger?.Log($"[NetworkSession] Session with Id {Id} disconnected!");
            
            DisconnectedEventHandler?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            try
            {
                _logger?.Log("[ServerPeerSession MeridianEncoderException] OnReceived: " + buffer.Length + " size: " + size);

                _socketMessageComponator.Received(buffer, offset, size);    
                //var message = new byte[size];
                //System.Buffer.BlockCopy(buffer, (int)offset, message, 0, (int)size);
            
                ////var dataPack = _encoder.Encode(message);
                ////var operationData = _convertor.From(dataPack);

                ////ReceivedEventHandler?.Invoke(this, operationData);
                ////_ = ReceivedEventAsync(operationData);

                //var dataPacks = _encoder.EncodeAll(message, _logger);

                //foreach (var dataPack in dataPacks)
                //{
                //    var operationData = _convertor.From(dataPack);
                //    _ = ReceivedEventAsync(operationData);
                //}

            }
            catch (MeridianEncoderException ex)
            {
                _logger?.LogError("[ServerPeerSession MeridianEncoderException] Error Received", ex);
            }
            catch (Exception ex)
            {
                _logger?.LogError("[ServerPeerSession] Error On Received", ex);
            }
        }

        private void OnSocketMessageComponatorReceivedMessage(byte[] message)
        {        
            try
            {
                var dataPacks = _encoder.EncodeAll(message, _logger);
                ProcessDataPacks(dataPacks);

            }
            catch (MeridianEncoderException ex)
            {
                _logger?.LogError("[ServerPeerSession MeridianEncoderException] Error Received", ex);
            }
            catch (Exception ex)
            {
                _logger?.LogError("[ServerPeerSession] Error On Received", ex);
            }       
        }

        private void ProcessDataPacks(RpcData[] dataPacks)
        {
            //_logger.Log("[ClientPeer] ProcessDataPacks Length: " + dataPacks.Length);

            foreach (var dataPack in dataPacks)
            {
                var operationData = _convertor.From(dataPack);
                _ = ReceivedEventAsync(operationData);
            }
        }

        protected override void OnEmpty()
        {
            _isBusy = false;

            CheckOperationDataQueue();
        }

        protected override void OnError(SocketError error)
        {
            _logger?.Log($"[NetworkSession] OnError Session caught an error with code {error}");

            ErrorEventHandler?.Invoke(this, error.ToString());
        }

        private void CheckOperationDataQueue()
        {
            if (!_isBusy && _operationDatas.Count > 0)
            {
                DecodeAndSend(_operationDatas.Dequeue());
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
                var sendBytesWithHeader = _socketMessageComponator.CreateMessageWitchHeader(_messageId, sendBytes);

                //_logger.Log("[ServerPeerSession MeridianEncoder] Send " + operationData.OperationCode + " Length: " + sendBytesWithHeader.Length);

                SendAsync(sendBytesWithHeader);
            }
            catch (MeridianEncoderException ex)
            {
                _logger?.LogError("[ServerPeerSession MeridianEncoderException] Error Send", ex);
                _isBusy = false;
            }
            catch (Exception ex)
            {
                _logger?.LogError("[ServerPeerSession Exception] Error Send", ex);
                _isBusy = false;
            }
        }

        private async Task ReceivedEventAsync(OperationData operationData)
        {
            await Task.Run(() => { ReceivedEventHandler?.Invoke(this, operationData); });  
        }
    }
}