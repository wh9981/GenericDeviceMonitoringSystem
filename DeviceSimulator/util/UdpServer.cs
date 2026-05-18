using System;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;

namespace Util
{
    public class UdpServer
    {
        private IPEndPoint serverEndPoint { get; set; }
        private UdpClient udpClient { get; set; }
        private IPAddress serverIP { get; set; }

        public void OpenUDPServer(string _serverIP, int _port)
        {
            serverIP = IPAddress.Parse(_serverIP);
            serverEndPoint = new IPEndPoint(serverIP, _port);
            udpClient = new UdpClient();
        }

        public void CloseUDPServer()
        {
            udpClient.Close();
            udpClient = null;
        }

        public async Task<bool> SendData(byte[] data)
        {
            try
            {
                if (udpClient != null)
                {
                    await udpClient.SendAsync(data, data.Length, serverEndPoint);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
