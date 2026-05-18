using System;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System.Threading;

namespace Util
{
    internal class StandardUdpServer
    {
        private UdpClient _server;
        private CancellationTokenSource _cts;

        public event Action<string> MessageSendEvent;
        public event Action<byte[], IPEndPoint> DataSendEvent;
        public event Action<byte[], IPEndPoint, int> DataSendEventWithId;

        private bool isConnect = false;
        private bool isSendOnly = false;


        private int _id = -1;

        public int Id
        {
            get { return _id; }
            set { _id = value; }
        }

        /*
            가장 Standard한 UDP Server 구현
            요청에 대한 응답 형식으로 구현되어 있고,
            UDP 특성 상 다중 클라이언트가 접속할 수 있기 때문에 클라이언트별로 IPEndPoint를 관리
        */


        public void OpenUDPServer(string ip, int port, out string message, bool isSendOnly = false)
        {
            try
            {
                _server = new UdpClient(port);
                message = "Server Starting..";
                isConnect = true;
                _cts = new CancellationTokenSource();
                this.isSendOnly = isSendOnly;
                _ = RunServer();
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return;
            }
        }

        public void CloseUDPServer()
        {
            isConnect = false;
            _cts?.Cancel();
            _server.Close();
            MessageSendEvent?.Invoke("Server Closed.");
        }

        private async Task RunServer()
        {
            try
            {
                var token = _cts.Token;
                while (!token.IsCancellationRequested)
                {
                    var result = await _server.ReceiveAsync();
                    var clientEP = result.RemoteEndPoint;

                    if (Id < 0)
                        DataSendEvent?.Invoke(result.Buffer, clientEP);
                    else
                        DataSendEventWithId?.Invoke(result.Buffer, clientEP, Id);
                }
            }
            catch (Exception ex)
            {
                if (ex is ObjectDisposedException)
                {
                    return;
                }

                MessageSendEvent?.Invoke(ex.Message);
            }
        }

        public async Task<bool> SendData(byte[] data, IPEndPoint clientEP)
        {
            try
            {
                if (isConnect)
                {
                    await _server.SendAsync(data, data.Length, clientEP);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                MessageSendEvent?.Invoke(ex.Message);
                return false;
            }
        }
    }
}

