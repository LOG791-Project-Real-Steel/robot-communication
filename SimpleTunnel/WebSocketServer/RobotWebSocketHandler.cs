using System.Net;
using System.Net.WebSockets;

namespace WebSocketServer
{
    internal class RobotWebSocketHandler
    {
        private const int BufferMaxSize = 4096;

        private WebSocket? robot = null;
        private HttpContext? robotContext = null;
        private WebSocket? controller = null;
        private HttpContext? controllerContext = null;

        private Task robotTask = Task.CompletedTask;
        private Task controllerTask = Task.CompletedTask;

        public IEnumerable<Task> Processes
        {
            get
            {
                yield return robotTask;
                yield return controllerTask;
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
                case "/send":
                    controller = await context.WebSockets.AcceptWebSocketAsync();
                    controllerContext = context;
                    controllerTask = StartControllerProcess();
                    await controllerTask;
                    break;

                case "/receive":
                    robot = await context.WebSockets.AcceptWebSocketAsync();
                    robotContext = context;
                    robotTask = StartRobotProcess();
                    await robotTask;
                    break;

                default:
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    break;
            }
        }

        private async Task StartRobotProcess()
        {
            try
            {
                if (robot is null)
                    return;

                byte[] buffer = new byte[BufferMaxSize];
                WebSocketReceiveResult result = await robot.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                buffer = TrimEnd(buffer);

                if (result is not null)
                {
                    while (!result.CloseStatus.HasValue)
                    {
                        if (controller is not null && !controller.CloseStatus.HasValue)
                            await controller.SendAsync(new ArraySegment<byte>(buffer), result.MessageType, result.EndOfMessage, CancellationToken.None);

                        buffer = new byte[BufferMaxSize];
                        result = await robot.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        buffer = TrimEnd(buffer);
                    }
                }
            }
            catch (Exception)
            {
                // TODO: Add logger to log error.
            }
            finally
            {
                robot!.Dispose();
                robot = null;
            }
        }

        private async Task StartControllerProcess()
        {
            try
            {
                if (controller is null)
                    return;

                byte[] buffer = new byte[BufferMaxSize];
                WebSocketReceiveResult result = await controller.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                buffer = TrimEnd(buffer);

                if (result is not null)
                {
                    while (!result.CloseStatus.HasValue)
                    {
                        if (robot is not null && !robot.CloseStatus.HasValue)
                            await robot.SendAsync(new ArraySegment<byte>(buffer), result.MessageType, result.EndOfMessage, CancellationToken.None);

                        buffer = new byte[BufferMaxSize];
                        result = await controller.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        buffer = TrimEnd(buffer);
                    }
                    await controller.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                    controller.Dispose();
                    controller = null;
                }
            }
            catch (Exception)
            {
                // TODO: Add logger to log error.
            }
            finally
            {
                controller!.Dispose();
                controller = null;
            }
        }

        private static byte[] TrimEnd(byte[] array)
        {
            int lastIndex = Array.FindLastIndex(array, b => b != 0);

            Array.Resize(ref array, lastIndex + 1);

            return array;
        }
    }
}
