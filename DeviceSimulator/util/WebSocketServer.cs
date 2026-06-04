using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Util
{
	internal class WebSocketServer
	{
        private SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private ConcurrentDictionary<string, List<WebSocket>> wsClientsDict = new ConcurrentDictionary<string, List<WebSocket>>();
        private HashSet<string> CloseURL = new HashSet<string>();
        private HashSet<string> AcceptURL = new HashSet<string>();
        private HttpListener httpListener;

        private bool running = false;

        public event Action<string> LogEvent;
        public event Action<string, string> DataReceive;


		public void RunServer(int port)
        {
            running = true;
            if (httpListener != null)
                return;

            httpListener = new HttpListener();
			httpListener.Prefixes.Clear();
			httpListener.Prefixes.Add($"http://*:{port}/");
			httpListener.Start();
			Task.Run(()=>StartAsync());
		}

		public async Task DownServer()
		{
            try
            {
                running = false;

                await _semaphore.WaitAsync();
                httpListener?.Close();
                foreach (var key in wsClientsDict.Keys)
                {
                    wsClientsDict[key].Clear();
                }
                httpListener = null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task AddTargetEndPoint(string path)
        {
            try
            {
                await _semaphore.WaitAsync();

                if (!AcceptURL.Contains(path))
                    AcceptURL.Add(path);
            }
            catch (Exception ex)
            {
                LogEvent?.Invoke($"WebsocketServer error : {ex.Message} in AddPath");
            }
            finally
            {
                _semaphore.Release();
            }
        }

		private async Task StartAsync()
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
            catch (Exception ex)
            {
                if (ex is HttpListenerException || ex is ObjectDisposedException)
                {
                    // 서버가 중지되었거나 HttpListener가 닫혔을 때 발생하는 예외를 무시
                    Console.WriteLine("WebSocketServer stopped: " + ex.Message);
                }
                else
                {
                    LogEvent?.Invoke($"WebsocketServer error : {ex.Message} in StartAsync");
                }
            }
		}

		private async Task HandleHttpRequest(HttpListenerContext context)
		{
			var requestUrl = context.Request.Url;
			var path = requestUrl.AbsolutePath;

            if (CloseURL.Contains(path) || (! await IsAllowedPath(path)))
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
                return;
            }

            var webSocketContext = await context.AcceptWebSocketAsync(subProtocol: null);
			var socket = webSocketContext.WebSocket;

            var clients = wsClientsDict.GetOrAdd(path, _ => new List<WebSocket>());
            clients.Add(webSocketContext.WebSocket);

            try
            {
                var r = Task.Run(() => RequestFromClient(webSocketContext.WebSocket, path));
            }
            catch (Exception ex)
            {
                LogEvent?.Invoke($"WebsocketServer error : {ex.Message} in HandleHttpRequest");
            }
        }

        private async Task<bool> IsAllowedPath(string path)
        {
            try
            {
                await _semaphore.WaitAsync();
                return AcceptURL.Contains(path);
            }
            catch (Exception ex)
            {
                LogEvent?.Invoke($"WebsocketServer error : {ex.Message} in IsAllowedPath");
                return false;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task RequestFromClient(System.Net.WebSockets.WebSocket sock, string path)
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
                        var data = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        DataReceive?.Invoke(data, path);
                    }
                }
				catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
				{
					// 비정상 종료가 감지된 경우 처리할 코드 추가
					// 예: 연결 종료 로그 남기기, 연결 재시도 등

					wsClientsDict[path].Remove(sock);
					sock.Dispose();
				}
			}
		}

		public async Task SendMessageToAll_Path(string message, string path)
		{
            try
            {
                await _semaphore.WaitAsync();
                if (wsClientsDict.ContainsKey(path))
                {
                    var buffer = Encoding.UTF8.GetBytes(message);
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
            }
            catch (Exception ex)
            {
                LogEvent?.Invoke($"WebsocketServer error : {ex.Message} in SendMessageToAll_Path (string)");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task SendMessageToAll_Path(byte[] buffer, string path)
        {
            try
            {
                await _semaphore.WaitAsync();
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
            }
            catch (Exception ex)
            {
                LogEvent?.Invoke($"WebsocketServer error : {ex.Message} in SendMessageToAll_Path (byte[])");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task CloseStreamTargetEndPoint(string path)
        {
            List<WebSocket> socketsToClose = null;

            try
            {
                try
                {
                    await _semaphore.WaitAsync();
                    CloseURL.Add(path);
                    if (wsClientsDict.TryRemove(path, out var list))
                    {
                        socketsToClose = list.ToList();
                        list.Clear();
                    }
                }
                finally
                {
                    _semaphore.Release();
                }

                if (socketsToClose == null)
                    return;

                foreach (var socket in socketsToClose)
                {
                    if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed by server", CancellationToken.None);
                    }

                    socket.Dispose();
                }
            }
            catch (Exception ex)
            {
                LogEvent?.Invoke($"WebsocketServer error : {ex.Message} in CloseStreamTargetEndPoint");
            }
        }

		public void OpenStreamTargetEndPoint(string path)
		{
            try
            {
                if (CloseURL.Contains(path))
                    CloseURL.Remove(path);
            }
            catch (Exception ex)
            {
                LogEvent?.Invoke($"WebsocketServer error : {ex.Message} in OpenStreamTargetEndPoint");
            }
        }

        public async Task RemoveSocket(WebSocket socket, string path)
        {
            try
            {
                await _semaphore.WaitAsync();

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
            catch (Exception ex)
            {
                LogEvent?.Invoke($"WebsocketServer error : {ex.Message} in RemoveSocket");
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
