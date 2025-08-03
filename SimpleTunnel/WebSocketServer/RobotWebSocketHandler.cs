using System.Diagnostics;
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
        private WebSocket? _robotPing;
        private WebSocket? _controllerPing;
        
        private Task _robotTask = Task.CompletedTask;
        private Task _controllerTask = Task.CompletedTask;
        private Task _robotPingTask = Task.CompletedTask;
        private Task _controllerPingTask = Task.CompletedTask;

        public IEnumerable<Task> Processes
        {
            get
            {
                yield return _robotTask;
                yield return _controllerTask;
                yield return _robotPingTask;
                yield return _controllerPingTask;
            }
        }

        public async Task Handle(HttpContext ctx, Func<Task> next)
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                await next();
                return;
            }

            switch (ctx.Request.Path)
            {
                case "/oculus":
                    _controller = await ctx.WebSockets.AcceptWebSocketAsync();
                    _controllerTask = Bridge(_controller, () => _robot, "oculus → robot");
                    break;

                case "/robot":
                    _robot = await ctx.WebSockets.AcceptWebSocketAsync();
                    _robotTask = Bridge(_robot, () => _controller, "robot  → oculus");
                    break;

                case "/oculus/ping":
                    _controllerPing = await ctx.WebSockets.AcceptWebSocketAsync();
                    _controllerPingTask = BridgePing(_controllerPing, () => _robotPing, "oculus-ping → robot-ping");
                    break;

                case "/robot/ping":
                    _robotPing = await ctx.WebSockets.AcceptWebSocketAsync();
                    _robotPingTask = BridgePing(_robotPing, () => _controllerPing, "robot-ping → oculus-ping");
                    break;
                
                default:
                    ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    logger.LogWarning("Unknown webSocket path {Path}", ctx.Request.Path);
                    break;
            }
        }
        
        private Task Bridge(WebSocket source, Func<WebSocket?> destSelector, string label) =>
            Task.Run(async () =>
            {
                logger.LogInformation("[{Label}] bridge started", label);

                var buffer = new byte[BufferMaxSize];

                try
                {
                    while (source.State == WebSocketState.Open)
                    {
                        var result =
                            await source.ReceiveAsync(buffer, CancellationToken.None);

                        if (result.CloseStatus.HasValue)
                            break;

                        WebSocket? dest = destSelector();
                        if (dest is not null && dest.State == WebSocketState.Open)
                        {
                            await dest.SendAsync(
                                new ArraySegment<byte>(buffer, 0, result.Count),
                                result.MessageType,
                                result.EndOfMessage,
                                CancellationToken.None);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[{Label}] bridge error", label);
                }
                finally
                {
                    logger.LogInformation("[{Label}] bridge stopped", label);
                    await CloseQuietly(source);
                }
            });
        
        private Task BridgePing(WebSocket source,
                                Func<WebSocket?> destSelector,
                                string label) =>
            Task.Run(async () =>
            {
                logger.LogInformation("[{Label}] ping bridge started", label);
                var sw     = new Stopwatch();
                var buffer = new byte[BufferMaxSize];

                try
                {
                    while (source.State == WebSocketState.Open)
                    {
                        var result =
                            await source.ReceiveAsync(buffer, CancellationToken.None);

                        if (result.CloseStatus.HasValue)
                            break;

                        string text = Encoding.UTF8.GetString(buffer, 0, result.Count);

                        if (text.Equals("ping", StringComparison.OrdinalIgnoreCase))
                        {
                            // ping came FROM this socket; start RTT timer
                            sw.Restart();
                            logger.LogDebug("[{Label}] → ping", label);
                        }
                        else if (text.Equals("pong", StringComparison.OrdinalIgnoreCase))
                        {
                            // pong came back TO this socket; stop and log RTT
                            sw.Stop();
                            logger.LogInformation(
                                "[{Label}] ← pong (RTT {Elapsed} ms)",
                                label,
                                sw.ElapsedMilliseconds);
                        }

                        WebSocket? dest = destSelector();
                        if (dest is not null && dest.State == WebSocketState.Open)
                        {
                            await dest.SendAsync(
                                new ArraySegment<byte>(buffer, 0, result.Count),
                                result.MessageType,
                                result.EndOfMessage,
                                CancellationToken.None);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[{Label}] ping bridge error", label);
                }
                finally
                {
                    logger.LogInformation("[{Label}] ping bridge stopped", label);
                    await CloseQuietly(source);
                }
            });

        private static async Task CloseQuietly(WebSocket ws)
        {
            try
            {
                if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
                    await ws.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "closing",
                        CancellationToken.None);
            }
            catch { /* swallow */ }
            finally
            {
                ws.Dispose();
            }
        }

        private async Task StartRobotProcess(string type="robot")
        {
            logger.LogInformation($"Starting {type} process");
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

        private async Task StartControllerProcess(string type="oculus")
        {
            logger.LogInformation($"Starting controller process ({type})");
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
        
        private static byte[] TrimEnd(byte[] array)
        {
            var lastIndex = Array.FindLastIndex(array, b => b != 0);

            Array.Resize(ref array, lastIndex + 1);

            return array;
        }
    }
}
