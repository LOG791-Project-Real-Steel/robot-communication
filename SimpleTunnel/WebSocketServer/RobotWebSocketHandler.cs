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

        public async Task Control(HttpContext context, Func<Task> next)
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
            if (robot is null)
                return;

            byte[] buffer = new byte[BufferMaxSize];
            WebSocketReceiveResult result = await robot.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            buffer = TrimEnd(buffer);

            if (result is not null)
            {
                while (!result.CloseStatus.HasValue)
                {
                    if (controller is null)
                        continue;

                    await controller.SendAsync(new ArraySegment<byte>(buffer), result.MessageType, result.EndOfMessage, CancellationToken.None);
                    buffer = new byte[BufferMaxSize];
                    result = await robot.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    buffer = TrimEnd(buffer);
                }
                await robot.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);

                if (controller is null)
                    return;

                await controller.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
            }
        }

        private async Task StartControllerProcess()
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
                    if (robot is null)
                        continue;

                    await robot.SendAsync(new ArraySegment<byte>(buffer), result.MessageType, result.EndOfMessage, CancellationToken.None);
                    buffer = new byte[BufferMaxSize];
                    result = await controller.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    buffer = TrimEnd(buffer);
                }
                await controller.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);

                if (robot is null)
                    return;

                await robot.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
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
