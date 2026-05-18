using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text;
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
            ReturnTCPProperty();
            server?.Stop();
        }

        public bool IsConnectClient()
        {
            return client != null && client.Connected && stream.CanWrite;
        }

        private async Task RunServer()
        {
            try
            {
                if (isConnect)
                {
                    if (client == null || !client.Connected || !stream.CanRead)
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
                if (ex is OperationCanceledException || ex is ObjectDisposedException)
                {
                    MessageSendEvent?.Invoke("서버 종료");
                    return;
                }

                MessageSendEvent?.Invoke(ex.Message);
                ReturnTCPProperty();
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
                    var i = await ReadAsyncWithTimeout(stream, bytes, 0, bytes.Length, 0, _cts.Token);

                    if (i == -1)
                    {
                        continue;
                    }
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
                    ReturnTCPProperty();
                    _ = RunServer();
                }
            }
        }

        private void ReturnTCPProperty()
        {
            stream?.Close();
            client?.Close();
            stream?.Dispose();
            client?.Dispose();
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
                    ReturnTCPProperty();
                    _ = RunServer();
                }
                return false;
            }
        }

        private async Task<int> ReadAsyncWithTimeout(NetworkStream stream, byte[] buffer, int offset, int count,
            int timeoutMilliseconds, CancellationToken cancellationToken)
        {
            var readTask = stream.ReadAsync(buffer, offset, count, cancellationToken);

            // Timeout을 위한 Task.Delay 설정
            var timeoutTask = Task.Delay(Timeout.Infinite, cancellationToken);

            // 먼저 완료된 Task를 기다림
            var completedTask = await Task.WhenAny(readTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                // 타임아웃이 발생했을 때 처리
                return -1;
            }

            // readTask가 완료되었을 때 결과 반환
            return await readTask;
        }
    }
}
