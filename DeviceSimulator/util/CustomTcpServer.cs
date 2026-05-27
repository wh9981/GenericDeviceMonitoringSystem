using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;

namespace Util
{
    public class CustomTcpServer
    {
        private TcpListener server;
        private NetworkStream stream;
        private TcpClient client;
        private CancellationTokenSource _cts;

        public event Action<string> MessageSendEvent;
        public event Action<byte[]> DataSendEvent;

        private bool isConnect = false;
        private bool isSendOnly = false;

        private int timer = 60000;

        /*
            특정 흐름에 대한 상태 제어용 TCP Server 구현
            상태값 송신 - 요청 없이 자동으로 전송
            제어 명령 수신 - 비 주기적으로 수신
        */


        public void OpenTCPServer(string ip, int port, out string message, bool isSendOnly = false)
        {
            try
            {
                server = new TcpListener(IPAddress.Parse(ip), port);
                server.Start();
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

                        if (!isSendOnly)
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
                ReturnConnectionProperty();
                server?.Stop();
            }
        }

        private async Task ReceiveClientMessage()
        {
            try
            {
                byte[] bytes = new byte[512];
                while (isConnect)
                {
                    var i = 0;
                    try
                    {
                        i = await CommonMethod.TcpReadAsyncWithTimeout(stream, bytes, 0, bytes.Length, timer, _cts.Token);

                        if (i == 0)
                        {
                            throw new IOException();
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex is TimeoutException)
                        {
                            // MessageSendEvent?.Invoke("data 수신 없음.");
                            continue;
                        }
                        throw; // 다른 예외는 상위로 던짐
                    }

                    byte[] receiveData = new byte[i];
                    Buffer.BlockCopy(bytes, 0, receiveData, 0, i);
                    DataSendEvent?.Invoke(receiveData);
                }
            }
            catch (Exception ex)
            {
                if (ex is TimeoutException || ex is IOException)
                {
                    MessageSendEvent?.Invoke("data 수신 없음.");
                    ReturnConnectionProperty();
                    _ = RunServer();
                }
                MessageSendEvent?.Invoke($"{ex.Message}");
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
                if (isSendOnly)
                {
                    ReturnConnectionProperty();
                    _ = RunServer();
                }
                return false;
            }
        }
    }
}
