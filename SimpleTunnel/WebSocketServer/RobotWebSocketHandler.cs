using System.Net;
using System.Net.WebSockets;

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
                    _controllerTask = StartControllerProcess();
                    await _controllerTask;
                    break;
                
                case "/oculus/ping":
                    _controllerPing = await ctx.WebSockets.AcceptWebSocketAsync();
                    _controllerPingTask = StartControllerProcess("oculus ping");
                    await _controllerPingTask;
                    break;
                
                case "/robot":
                    _robot = await ctx.WebSockets.AcceptWebSocketAsync();
                    _robotTask = StartRobotProcess();
                    await _robotTask;
                    break;
                
                case "/robot/ping":
                    _robotPing = await ctx.WebSockets.AcceptWebSocketAsync();
                    _robotPingTask = StartRobotProcess("robot ping");
                    await _robotPingTask;
                    break;
                
                default:
                    ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    logger.LogWarning("Could not find viable path: {Path}", ctx.Request.Path);
                    break;
            }
        }

        private async Task StartRobotProcess(string type = "robot")
        {
            logger.LogInformation("Starting {Type} process", type);
            var stopWatch = new System.Diagnostics.Stopwatch();

            try
            {
                byte[] buffer = new byte[BufferMaxSize];
                if (_robot is not null)
                {
                    WebSocketReceiveResult result =
                        await _robot.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    buffer = TrimEnd(buffer);

                    while (!result.CloseStatus.HasValue)
                    {
                        if (_controller is not null && !_controller.CloseStatus.HasValue)
                            await _controller.SendAsync(new ArraySegment<byte>(buffer), result.MessageType,
                                result.EndOfMessage, CancellationToken.None);

                        buffer = new byte[BufferMaxSize];
                        result = await _robot.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        buffer = TrimEnd(buffer);
                    }
                }
                else if (_robotPing is not null)
                {
                    WebSocketReceiveResult result =
                        await _robotPing.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    buffer = TrimEnd(buffer);

                    while (!result.CloseStatus.HasValue)
                    {
                        string txt = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                        if (txt.Equals("ping", StringComparison.OrdinalIgnoreCase))
                        {
                            stopWatch.Restart();
                            logger.LogDebug("Robot → Oculus ping");
                        }
                        else if (txt.Equals("pong", StringComparison.OrdinalIgnoreCase))
                        {
                            stopWatch.Stop();
                            logger.LogInformation("RTT Robot↔Oculus {Elapsed} ms", stopWatch.ElapsedMilliseconds);
                        }
                        
                        if (_controllerPing is not null && !_controllerPing.CloseStatus.HasValue)
                            await _controllerPing.SendAsync(
                                new ArraySegment<byte>(buffer), result.MessageType,
                                result.EndOfMessage, CancellationToken.None);

                        buffer = new byte[BufferMaxSize];
                        result = await _robotPing.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        buffer = TrimEnd(buffer);
                    }
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
                _robotPing?.Dispose();
                _robotPing = null;
            }
        }

        private async Task StartControllerProcess(string type = "oculus")
        {
            logger.LogInformation("Starting {Type} process", type);
            var stopWatch = new System.Diagnostics.Stopwatch();
            
            try
            {
                byte[] buffer = new byte[BufferMaxSize];

                if (_controller is not null)
                {
                    WebSocketReceiveResult result =
                        await _controller.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    buffer = TrimEnd(buffer);

                    while (!result.CloseStatus.HasValue)
                    {
                        if (_robot is not null && !_robot.CloseStatus.HasValue)
                            await _robot.SendAsync(new ArraySegment<byte>(buffer), result.MessageType,
                                result.EndOfMessage, CancellationToken.None);

                        buffer = new byte[BufferMaxSize];
                        result = await _controller.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        buffer = TrimEnd(buffer);
                    }

                    await _controller.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                    _controller.Dispose();
                    _controller = null;
                }
                else if (_controllerPing is not null)
                {
                    WebSocketReceiveResult result = await _controllerPing.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    buffer = TrimEnd(buffer);

                    while (!result.CloseStatus.HasValue)
                    {
                        string txt = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);

                        if (txt.Equals("ping", StringComparison.OrdinalIgnoreCase))
                        {
                            stopWatch.Restart();
                            logger.LogDebug("Oculus → Robot ping");
                        }
                        else if (txt.Equals("pong", StringComparison.OrdinalIgnoreCase))
                        {
                            stopWatch.Stop();
                            logger.LogInformation("RTT Oculus↔Robot {Elapsed} ms", stopWatch.ElapsedMilliseconds);
                        }

                        if (_robotPing is not null && !_robotPing.CloseStatus.HasValue)
                            await _robotPing.SendAsync(new ArraySegment<byte>(buffer), result.MessageType,
                                result.EndOfMessage, CancellationToken.None);


                        buffer = new byte[BufferMaxSize];
                        result = await _controllerPing.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        buffer = TrimEnd(buffer);
                    }
                    await _controllerPing.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                    _controllerPing.Dispose();
                    _controllerPing = null;
                }
            }
            catch (Exception e)
            {
                logger.LogError(e.Message);
            }
            finally
            {
                _controller?.Dispose();
                _controller = null;
                _controllerPing.Dispose();
                _controllerPing = null;
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
