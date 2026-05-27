using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;

namespace Util
{
    public class StandardTcpServer
    {
        private TcpListener server;
        private NetworkStream stream;
        private TcpClient client;
        private CancellationTokenSource _cts;

        public event Action<string> MessageSendEvent;
        public event Action<byte[]> DataSendEvent;

        private bool isConnect = false;

        private int timer = 5000;


        /*
            가장 Standard한 TCP Server 구현
            요청에 대한 응답 형식으로 구현되어 있고,
            Blazor 특성 상 1초 주기로 질의 요청을 하기 때문에 최소 1초에 1개의 패킷 수신을 기준으로
                5초 이상 수신이 없으면 연결이 끊긴 것으로 간주, 현재 연결을 해제하고 다음 연결을 대기

            SendOnly 모드 시, 송신만 수행하고 송신중 이슈 터질 시 현재 연결을 해재하고 다음 연결을 대기
        */


        public void OpenTCPServer(string ip, int port, out string message)
        {
            try
            {
                server = new TcpListener(IPAddress.Parse(ip), port);
                server.Start();
                message = "Server Starting..";
                isConnect = true;
                _cts = new CancellationTokenSource();
                _ = RunServer();
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return;
            }
        }

        public void CloseTCPServer()
        {
            isConnect = false;
            _cts?.Cancel();
            ReturnConnectionProperty();
            server?.Stop();
        }

        public bool IsConnectClient()
        {
            return client != null && client.Connected && stream != null && stream.CanWrite;
        }

        public void ChangeTimer(int timeout)
        {
            timer = timeout;
        }

        private async Task RunServer()
        {
            try
            {
                if (isConnect)
                {
                    if (client == null || !client.Connected)
                    {
                        MessageSendEvent?.Invoke("Connecting...");
                        client = await server.AcceptTcpClientAsync();
                        MessageSendEvent?.Invoke("Connected!");

                        stream = client.GetStream();
                        _ = ReceiveClientMessage();
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException || ex is ObjectDisposedException || ex is SocketException)
                {
                    MessageSendEvent?.Invoke("서버 종료");
                    return;
                }

                MessageSendEvent?.Invoke(ex.Message);
                CloseTCPServer();
            }
        }

        private async Task ReceiveClientMessage()
        {
            try
            {
                byte[] bytes = new byte[1024 * 5];
                while (isConnect)
                {
                    var i = await CommonMethod.TcpReadAsyncWithTimeout(stream, bytes, 0, bytes.Length, timer, _cts.Token);

                    if (i == 0)
                    {
                        throw new IOException();
                    }

                    byte[] receiveData = new byte[i];
                    Buffer.BlockCopy(bytes, 0, receiveData, 0, i);
                    DataSendEvent?.Invoke(receiveData);
                }
            }
            catch (Exception e)
            {
                if (e is TimeoutException || e is IOException)
                {
                    MessageSendEvent?.Invoke("data 수신 없음.");
                    ReturnConnectionProperty();
                    _ = RunServer();
                }
            }
        }

        private void ReturnConnectionProperty()
        {
            stream?.Dispose();
            client?.Dispose();
            stream = null;
            client = null;
        }

        public async Task<bool> SendData(byte[] data)
        {
            try
            {
                if (IsConnectClient())
                {
                    await stream.WriteAsync(data, 0, data.Length);
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
