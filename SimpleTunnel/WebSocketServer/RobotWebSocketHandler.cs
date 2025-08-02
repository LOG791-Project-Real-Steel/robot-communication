using System.Net;
using System.Net.WebSockets;
using System.Text;

namespace WebSocketServer
{
    internal class RobotWebSocketHandler(ILogger<RobotWebSocketHandler> logger)
    {
        private const int BufferMaxSize = 65536;

        private WebSocket? _robot;
        private WebSocket? _controller;

        private Task _robotTask = Task.CompletedTask;
        private Task _controllerTask = Task.CompletedTask;

        public IEnumerable<Task> Processes
        {
            get
            {
                yield return _robotTask;
                yield return _controllerTask;
            }
        }

        public async Task Handle(HttpContext context, Func<Task> next)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                await next();
                return;
            }

            switch (context.Request.Path)
            {
                case "/oculus":
                    _controller = await context.WebSockets.AcceptWebSocketAsync();
                    _controllerTask = StartControllerProcess();
                    await _controllerTask;
                    break;

                case "/robot":
                    _robot = await context.WebSockets.AcceptWebSocketAsync();
                    _robotTask = StartRobotProcess();
                    await _robotTask;
                    break;
                
                case "/ping":                                   // stand-alone ping socket
                    var pingSock = await context.WebSockets.AcceptWebSocketAsync();
                    _ = HandlePingBridgeAsync(pingSock);    // fire-and-forget
                    break;

                default:
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    logger.LogWarning("Could not find viable path: " + context.Request.Path);
                    break;
            }
        }

        private async Task StartRobotProcess()
        {
            logger.LogInformation("Starting robot process");
            try
            {
                if (_robot is null)
                    return;

                byte[] buffer = new byte[BufferMaxSize];
                WebSocketReceiveResult result = await _robot.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                buffer = TrimEnd(buffer);

                while (!result.CloseStatus.HasValue)
                {
                    if (_controller is not null && !_controller.CloseStatus.HasValue)
                        await _controller.SendAsync(new ArraySegment<byte>(buffer), result.MessageType, result.EndOfMessage, CancellationToken.None);

                    buffer = new byte[BufferMaxSize];
                    result = await _robot.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    buffer = TrimEnd(buffer);
                }
            }
            catch (Exception e)
            {
                logger.LogError(e.Message);
            }
            finally
            {
                _robot?.Dispose();
                _robot = null;
            }
        }

        private async Task StartControllerProcess()
        {
            logger.LogInformation("Starting controller process (oculus)");
            try
            {
                if (_controller is null)
                    return;

                byte[] buffer = new byte[BufferMaxSize];
                WebSocketReceiveResult result = await _controller.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                buffer = TrimEnd(buffer);

                while (!result.CloseStatus.HasValue)
                {
                    if (_robot is not null && !_robot.CloseStatus.HasValue)
                        await _robot.SendAsync(new ArraySegment<byte>(buffer), result.MessageType, result.EndOfMessage, CancellationToken.None);

                    buffer = new byte[BufferMaxSize];
                    result = await _controller.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    buffer = TrimEnd(buffer);
                }
                await _controller.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                _controller.Dispose();
                _controller = null;
            }
            catch (Exception e)
            {
                logger.LogError(e.Message);
            }
            finally
            {
                _controller?.Dispose();
                _controller = null;
            }
        }

        private async Task HandlePingBridgeAsync(WebSocket socket)
        {
            logger.LogInformation("Ping bridge connected");
            try
            {
                var buffer = new byte[BufferMaxSize];

                while (socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(buffer, CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(result.CloseStatus!.Value, result.CloseStatusDescription,
                                                CancellationToken.None);
                        break;
                    }

                    var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    if (msg.Equals("ping", StringComparison.OrdinalIgnoreCase))
                    {
                        bool ok  = await PingRobotAsync();
                        string r = ok ? "pong" : "timeout";
                        await socket.SendAsync(Encoding.UTF8.GetBytes(r), WebSocketMessageType.Text, true,
                                               CancellationToken.None);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ping bridge crashed");
            }
            finally
            {
                try { await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None); }
                catch { /* ignore */ }
                socket.Dispose();
            }
        }

        private async Task<bool> PingRobotAsync()
        {
            try
            {
                if (_robot is null || _robot.CloseStatus.HasValue) return false;

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                await _robot.SendAsync(ArraySegment<byte>.Empty,
                                       (WebSocketMessageType)0x9,
                                       true,
                                       cts.Token);
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        private static byte[] TrimEnd(byte[] array)
        {
            var lastIndex = Array.FindLastIndex(array, b => b != 0);

            Array.Resize(ref array, lastIndex + 1);

            return array;
        }
    }
}
