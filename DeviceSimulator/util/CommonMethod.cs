using System.Net.Sockets;
using System.IO.Ports;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace Util
{
    public class CommonMethod
    {
        /// <summary>
        /// TCP NetworkStream의 Timeout ReadAsync
        /// </summary>
        public static async Task<int> TcpReadAsyncWithTimeout(NetworkStream stream, byte[] buffer, int offset, int count, int timeoutMilliseconds, CancellationToken globalToken)
        {
            // 1. 글로벌 토큰 토대로 LTS 생성 후 타임아웃 속성 연결 (Linked Token Source)
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(globalToken))
            {
                cts.CancelAfter(timeoutMilliseconds); // timeoutMilliseconds/1000 초 뒤 자동 취소 설정

                try
                {
                    // 2. ReadAsync에 토큰을 직접 전달. 타임아웃 시 여기서 바로 Task가 취소 상태로 종료됨!
                    return await stream.ReadAsync(buffer, offset, count, cts.Token);
                }
                catch (OperationCanceledException) when (!globalToken.IsCancellationRequested)
                {
                    // 타임아웃 발생 시 '진행 중'인 작업 없이 깔끔하게 예외만 던짐
                    throw new TimeoutException("Read operation timed out.");
                }
            }
        }

        public static async Task<byte[]> UdpReadAsyncWithTimeout(UdpClient udpClient, int timeoutMilliseconds, CancellationToken globalToken)
        {
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(globalToken))
            {
                cts.CancelAfter(timeoutMilliseconds);
                var token = cts.Token;

                try
                {
                    var receiveTask = udpClient.ReceiveAsync();
                    var cancelTask = Task.Delay(timeoutMilliseconds, token);

                    var completedTask = await Task.WhenAny(receiveTask, cancelTask);

                    if (completedTask == cancelTask)
                    {
                        udpClient.Dispose(); // ReceiveAsync 깨우기
                        token.ThrowIfCancellationRequested();
                    }

                    var result = await receiveTask;
                    return result.Buffer;
                }
                catch (OperationCanceledException) when (!globalToken.IsCancellationRequested)
                {
                    // 타임아웃으로 간주
                    // 호출단에서는 udpClient가 닫혀있는걸 인지하고 적절히 처리해야 함 (예: 재시도, 로그 기록 등)
                    throw new TimeoutException("UDP receive operation timed out.");
                }
            }
        }

        /// <summary>
        /// Ping을 이용하여 IP 주소의 네트워크 연결 상태를 확인하는 메서드
        /// </summary>
        public static async Task<bool> CheckPing(string ip, CancellationToken token)
        {
            try
            {
                using (Ping ping = new Ping())
                {
                    PingReply reply = await ping.SendPingAsync(ip, 1000); // 1초 타임아웃
                    return reply.Status == IPStatus.Success;
                }
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                    return true;

                throw;
            }
        }


        /// <summary>
        /// 시리얼 포트 존재 여부 확인
        /// </summary>
        public static bool IsPortExist(string comName)
        {
            string[] coms = SerialPort.GetPortNames();

            foreach (string com in coms)
            {
                if (com.Equals(comName))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
