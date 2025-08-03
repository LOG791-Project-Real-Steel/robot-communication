using System.Net;
using System.Net.WebSockets;

namespace WebSocketServer
{
    internal class RobotWebSocketHandler(ILogger<RobotWebSocketHandler> logger)
    {
        private const int BufferMaxSize = 65_536;
        
        private WebSocket? _robot;
        private WebSocket? _oculus;
        private WebSocket? _robotPing;
        private WebSocket? _oculusPing;
        
        private Task _robotTask = Task.CompletedTask;
        private Task _oculusTask = Task.CompletedTask;
        private Task _robotPingTask = Task.CompletedTask;
        private Task _oculusPingTask = Task.CompletedTask;

        public IEnumerable<Task> Processes
        {
            get
            {
                yield return _robotTask;
                yield return _oculusTask;
                yield return _robotPingTask;
                yield return _oculusPingTask;
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

            var port = ctx.Connection.LocalPort;
            var ws = await ctx.WebSockets.AcceptWebSocketAsync();
            var pre = $"[{port}] ";

            switch (ctx.Request.Path)
            {
                case "/robot":
                    _robot     = ws;
                    _robotTask = RelayLoop(_robot, () => _oculus, pre + "robot");
                    await _robotTask;
                    break;

                case "/oculus":
                    _oculus = ws;
                    _oculusTask = RelayLoop(_oculus, () => _robot, pre + "oculus");
                    await _oculusTask;
                    break;

                case "/robot/ping":
                    _robotPing  = ws;
                    _robotPingTask = RelayLoop(_robotPing, () => _oculusPing, pre + "robot ping");
                    await _robotPingTask;
                    break;

                case "/oculus/ping":
                    _oculusPing = ws;
                    _oculusPingTask = RelayLoop(_oculusPing, () => _robotPing, pre + "oculus ping");
                    await _oculusPingTask;
                    break;

                default:
                    ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    logger.LogWarning($"{pre}unknown path {{Path}}", ctx.Request.Path);
                    break;
            }
        }

        private async Task RelayLoop(WebSocket source,
            Func<WebSocket?> target,
            string logName)
        {
            logger.LogInformation($"{logName} connected");

            try
            {
                var buffer = new byte[BufferMaxSize];

                while (source.State == WebSocketState.Open)
                {
                    var res = await source.ReceiveAsync(buffer, CancellationToken.None);

                    if (res.MessageType == WebSocketMessageType.Close)
                        break;

                    WebSocket? dst = target();
                    if (dst is not null && dst.State == WebSocketState.Open)
                    {
                        await dst.SendAsync(buffer.AsMemory(..res.Count),
                            res.MessageType,
                            res.EndOfMessage,
                            CancellationToken.None);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"{logName} relay crashed");
            }
            finally
            {
                try
                {
                    await source.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                }
                catch
                {
                    /* ignore */
                }

                source.Dispose();
                logger.LogInformation($"{logName} disconnected");

                switch (logName)
                {
                    case not null when logName.Contains("robot ping"): _robotPing = null; break;
                    case not null when logName.Contains("oculus ping"): _oculusPing = null; break;
                    case not null when logName.Contains("robot"): _robot = null; break;
                    case not null when logName.Contains("oculus"): _oculus = null; break;
                }
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
                        if (_oculus is not null && !_oculus.CloseStatus.HasValue)
                            await _oculus.SendAsync(new ArraySegment<byte>(buffer), result.MessageType,
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
                        }
                        else if (txt.Equals("pong", StringComparison.OrdinalIgnoreCase))
                        {
                            stopWatch.Stop();
                            logger.LogInformation("RTT Robot ↔ Oculus {Elapsed} ms", stopWatch.ElapsedMilliseconds);
                        }
                        
                        if (_oculusPing is not null && !_oculusPing.CloseStatus.HasValue)
                            await _oculusPing.SendAsync(
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
                logger.LogError(e, "Robot process error");
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

                if (_oculus is not null)
                {
                    WebSocketReceiveResult result =
                        await _oculus.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    buffer = TrimEnd(buffer);

                    while (!result.CloseStatus.HasValue)
                    {
                        if (_robot is not null && !_robot.CloseStatus.HasValue)
                            await _robot.SendAsync(new ArraySegment<byte>(buffer), result.MessageType,
                                result.EndOfMessage, CancellationToken.None);

                        buffer = new byte[BufferMaxSize];
                        result = await _oculus.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        buffer = TrimEnd(buffer);
                    }

                    await _oculus.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                    _oculus.Dispose();
                    _oculus = null;
                }
                else if (_oculusPing is not null)
                {
                    WebSocketReceiveResult result = await _oculusPing.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    buffer = TrimEnd(buffer);

                    while (!result.CloseStatus.HasValue)
                    {
                        string txt = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);

                        if (txt.Equals("ping", StringComparison.OrdinalIgnoreCase))
                        {
                            stopWatch.Restart();
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
                        result = await _oculusPing.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        buffer = TrimEnd(buffer);
                    }
                    await _oculusPing.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                    _oculusPing.Dispose();
                    _oculusPing = null;
                }
            }
            catch (Exception e)
            {
                logger.LogError(e.Message);
            }
            finally
            {
                _oculus?.Dispose();
                _oculus = null;
                _oculusPing?.Dispose();
                _oculusPing = null;
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
