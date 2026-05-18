using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Util
{
	internal class WebSocketServer
	{
        public Action<string, string> MessageReceive;

        private HttpListener httpListener;
		private System.Net.WebSockets.WebSocket socket;
		private Dictionary<string, List<System.Net.WebSockets.WebSocket>> wsClientsDict = new Dictionary<string, List<WebSocket>>();
		private bool running = false;

        private List<string> DeleteURL = new List<string>();
        private SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public static Action<string> DataSendEvent;
        public Action LoopClientInput;


		public void RunServer(int port)
        {
            running = true;
            if (httpListener != null)
                return;

            httpListener = new HttpListener();
			httpListener.Prefixes.Clear();
			httpListener.Prefixes.Add($"http://*:{port}/");
			httpListener.Start();
			StartAsync();
		}

		public async void DownServer()
		{
			running = false;

            await _semaphore.WaitAsync();
            httpListener?.Close();
			foreach(var key in  wsClientsDict.Keys)
			{
                wsClientsDict[key].Clear();
            }
            httpListener = null;
            _semaphore.Release();
        }

		private async void StartAsync()
		{
			try
			{
				while (running)
				{
					var context = await httpListener.GetContextAsync();
					if (context.Request.IsWebSocketRequest)
					{
						await HandleHttpRequest(context);
					}
					else
					{
						context.Response.StatusCode = 400;
						context.Response.Close();
					}
				}
			}
			catch (HttpListenerException) { }
			catch (ObjectDisposedException) { }
		}

		private async Task HandleHttpRequest(HttpListenerContext context)
		{
			var requestUrl = context.Request.Url;
			var path = requestUrl.AbsolutePath;

			/*if (DeleteURL.Contains(path))
				return;*/

            var webSocketContext = await context.AcceptWebSocketAsync(subProtocol: null);
			socket = webSocketContext.WebSocket;

            // await SendMessageAsync(socket, "Websocket Connect!"); // 클라이언트에게 메세지 보내기

            if (DeleteURL.Contains(path))
                path = "";

            switch (path) //TODO 외부에서 주입하는 방식으로 변경 필요. 완전 모듈화
			{
				case "/sources":
				case "/sources/1/trajectories":
                case "/vms":
                case "/":
                    if (!wsClientsDict.ContainsKey(path))
						wsClientsDict[path] = new List<System.Net.WebSockets.WebSocket>();
					wsClientsDict[path].Add(webSocketContext.WebSocket);

					if (path.Equals("/sources"))
						LoopClientInput?.Invoke();

					break;
				default:
                    context.Response.StatusCode = 400;
					context.Response.Close();
					break;
			}
			try
			{
				var r = Task.Run(() => RequestFromClient(webSocketContext.WebSocket, path));
			}
			catch (Exception e)
			{

			}
		}

		public async Task RequestFromClient(System.Net.WebSockets.WebSocket sock, string path)
		{
			byte[] buffer = new byte[1024];
			while (running)
			{
				try
				{
					var result = await sock.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
					if (result.MessageType == WebSocketMessageType.Close) // 클라이언트가 연결을 종료한 경우
					{
						wsClientsDict[path].Remove(sock);
						sock.Dispose();
						break;
					}
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        MessageReceive?.Invoke(message, path);
                    }
                }
				catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
				{
					// 비정상 종료가 감지된 경우 처리할 코드 추가
					// 예: 연결 종료 로그 남기기, 연결 재시도 등

					wsClientsDict[path].Remove(sock);
					sock.Dispose();
					Console.WriteLine("???");
				}
			}
		}

		public async Task SendMessageToAll_Path(string message, string path)
		{
			var buffer = Encoding.UTF8.GetBytes(message);

			try
			{
                //await _semaphore.WaitAsync();
                if (wsClientsDict.ContainsKey(path))
				{
					var clients = wsClientsDict[path].ToList();
					foreach (var s in clients)
					{
                        if (s.State == WebSocketState.Aborted)
                        {
                            wsClientsDict[path].Remove(s); // 요소 직접 제거
                            continue;
                        }
                        await s.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
					}
				}

                //_semaphore.Release();
            }
			catch (Exception ex)
            {
                if (_semaphore.CurrentCount == 0)
                    _semaphore.Release();
            }
		}

        public async Task SendMessageToAll_Path(byte[] buffer, string path)
        {
            try
            {
                //await _semaphore.WaitAsync();
                if (wsClientsDict.ContainsKey(path))
                {
                    var clients = wsClientsDict[path].ToList();
                    foreach (var s in clients)
                    {
                        if (s.State == WebSocketState.Aborted)
                        {
                            wsClientsDict[path].Remove(s); // 요소 직접 제거
                            continue;
                        }
                        await s.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Binary, true, CancellationToken.None);
                    }
                }

                //_semaphore.Release();
            }
            catch (Exception ex)
            {
                if (_semaphore.CurrentCount == 0)
                    _semaphore.Release();
            }
        }

        public async Task CloseStreamTargetEndPoint(string path)
        {
            try
            {
                await _semaphore.WaitAsync();
                DeleteURL.Add(path);
                if (wsClientsDict.TryGetValue(path, out var list))
                {
                    while (list.Count > 0)
                        await RemoveSocket(list[list.Count-1], path);

                    wsClientsDict.Remove(path);
                }
                _semaphore.Release();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message + "WebSocketServer_CloseStreamTargetEndPoint()");
                _semaphore.Release();
            }
        }

		public async Task OpenStreamTargetEndPoint(string path)
		{
            DeleteURL.Remove(path);
        }

        public async Task RemoveSocket(System.Net.WebSockets.WebSocket socket, string path)
        {
            if (wsClientsDict.TryGetValue(path, out var clients))
            {
                clients.Remove(socket);
            }

            // 안전하게 WebSocket 닫기
            if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed by server", CancellationToken.None);
            }

            socket.Dispose();
            Console.WriteLine($"Socket removed: {path}");
        }
    }
}
