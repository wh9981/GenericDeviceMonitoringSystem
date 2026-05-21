using System;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;

namespace Util
{
    public class UdpSender
    {
        /// <summary>
        /// 일방향 UDP Sender - DST IP와 포트를 설정하여 데이터를 송신하는 클래스
        /// </summary>

        private IPEndPoint ServerEndPoint { get; set; }
        private UdpClient UdpClient { get; set; }

        public Action<string> DataSendEvent;

        /// <summary>
        /// IP, Port를 전달 받아 udpClient를 초기화하는 메서드. 이후 SendData 메서드를 통해 데이터를 송신할 수 있음
        /// </summary>
        public void Open(string _serverIP, int _port)
        {
            var serverIP = IPAddress.Parse(_serverIP);
            ServerEndPoint = new IPEndPoint(serverIP, _port);
            UdpClient = new UdpClient();
        }

        /// <summary>
        /// udpClient를 Dispose하여 리소스를 해제하는 메서드
        /// </summary>
        public void Close()
        {
            UdpClient?.Dispose();
            UdpClient = null;
        }

        /// <summary>
        /// 바이트 배열 형태의 데이터를 udpClient를 통해 설정된 DST IP와 포트로 송신하는 메서드. 송신 성공 여부를 boolean 값으로 반환
        /// </summary>
        public async Task<bool> SendData(byte[] data)
        {
            try
            {
                if (UdpClient != null)
                {
                    await UdpClient.SendAsync(data, data.Length, ServerEndPoint);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                DataSendEvent?.Invoke($"{ex.Message} in SendData");
                return false;
            }
        }
    }
}
