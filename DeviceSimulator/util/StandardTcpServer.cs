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
    public class StandardTcpServer
    {
        private TcpListener server;
        private NetworkStream stream;
        private TcpClient client;
        private CancellationTokenSource _cts;

        public event Action<string> MessageSendEvent;
        public event Action<byte[]> DataSendEvent;

        private bool isConnect = false;
        private bool isSendOnly = false;

        private int timer = 5000;


        /*
            가장 Standard한 TCP Server 구현
            요청에 대한 응답 형식으로 구현되어 있고,
            Blazor 특성 상 1초 주기로 질의 요청을 하기 때문에 최소 1초에 1개의 패킷 수신을 기준으로
                5초 이상 수신이 없으면 연결이 끊긴 것으로 간주, 현재 연결을 해제하고 다음 연결을 대기

            SendOnly 모드 시, 송신만 수행하고 송신중 이슈 터질 시 현재 연결을 해재하고 다음 연결을 대기
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
                    if (client == null || !client.Connected || !stream.CanRead)
                    {
                        MessageSendEvent?.Invoke("Connecting...");
                        client = await server.AcceptTcpClientAsync();
                        MessageSendEvent?.Invoke("Connected!");

                        stream = client.GetStream();

                        if(! isSendOnly)
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
                byte[] bytes = new byte[1024 * 5];
                while (isConnect)
                {
                    var i = await ReadAsyncWithTimeout(stream, bytes, 0, bytes.Length, timer, _cts.Token);

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
            var timeoutTask = Task.Delay(timeoutMilliseconds, cancellationToken);

            // 먼저 완료된 Task를 기다림
            var completedTask = await Task.WhenAny(readTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                // 타임아웃이 발생했을 때 처리
                throw new TimeoutException("Read operation timed out.");
            }

            // readTask가 완료되었을 때 결과 반환
            return await readTask;
        }
    }
}
