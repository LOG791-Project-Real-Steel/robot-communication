using RemoteKeyboardController.Models;
using System.Net.WebSockets;
using System.Text;

namespace RemoteKeyboardController
{
    public class ConsoleHandler
    {
        private readonly string _serverAddress;
        private readonly HttpClient _client;

        private Message? Msg;
        private State state = State.Main;
        private Movement movement = Movement.None;

        private const float ThrottleRate = 0.1f;
        private const float ThrottleAutoRate = 0.30f;
        private const float SteeringRate = 0.25f;
        private const float SteeringAutoRate = 0.10f;
        private RaceCar car = new();

        public ConsoleHandler(string serverAddress)
        {
            _serverAddress = serverAddress;
            _client = new()
            {
                BaseAddress = new Uri($"http://{_serverAddress}"),
                Timeout = TimeSpan.FromSeconds(2)
            };

            Task.Run(async () =>
            {
                while (state != State.Quit)
                {
                    while (!(await IsServerOnline()))
                        await Task.Delay(1000);

                    using ClientWebSocket client = new();
                    Uri uri = new($"ws://{serverAddress}/send");
                    CancellationTokenSource cts = new();
                    cts.CancelAfter(TimeSpan.FromSeconds(120));
                    try
                    {
                        await client.ConnectAsync(uri, cts.Token);
                        while (client.State == WebSocketState.Open)
                        {
                            await Task.Delay(50);

                            if (Msg is null)
                                continue;

                            ArraySegment<byte> buffer = new(Encoding.UTF8.GetBytes(Msg.Json()));
                            await client.SendAsync(buffer, WebSocketMessageType.Text, true, cts.Token);

                            if (Msg is QuitMessage)
                            {
                                CancellationTokenSource cts2 = new();
                                cts2.CancelAfter(TimeSpan.FromSeconds(120));
                                await client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Connection closed due to user being done with application.", cts2.Token);
                                await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed due to user being done with application.", cts2.Token);
                                state = State.Quit;
                            }
                        }
                    }
                    catch (WebSocketException e)
                    {
                        Console.WriteLine(e.Message);
                    }
                    finally
                    {
                        cts.Cancel();
                        client.Dispose();
                    }
                }
            });
        }

        private enum State
        {
            Main,
            Quitting,
            Quit,
        }

        private enum Movement
        {
            None,
            Up,
            Back,
            Left,
            Right
        }

        public void Run()
        {
            RefreshCMD();
            Process();
        }

        private void Process()
        {
            ConsoleKeyInfo key;
            while (state != State.Quit)
            {
                RefreshCMD();
                try
                {
                    if (state == State.Quitting)
                        continue;

                    key = Console.ReadKey();
                    switch (key.Key)
                    {
                        case ConsoleKey.Q:
                        case ConsoleKey.Escape:
                            state = State.Quitting;
                            Msg = new QuitMessage();
                            break;

                        case ConsoleKey.W:
                            car.Throttle -= ThrottleRate;
                            Msg = new MoveMessage(car);
                            break;

                        case ConsoleKey.A:
                            car.Steering += SteeringRate;
                            Msg = new MoveMessage(car);
                            break;

                        case ConsoleKey.S:
                            car.Throttle += ThrottleRate;
                            Msg = new MoveMessage(car);
                            break;

                        case ConsoleKey.D:
                            car.Steering -= SteeringRate;
                            Msg = new MoveMessage(car);
                            break;

                        default:
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    break;
                }
            }
        }

        private void RefreshCMD()
        {
            Console.Clear();
            switch (state)
            {
                case State.Main:
                    Console.WriteLine(Displays.Header);
                    Console.WriteLine(Displays.MainMenu);
                    break;

                case State.Quit:
                case State.Quitting:
                    Console.WriteLine(Displays.Goodbye);
                    break;

                default:
                    Console.WriteLine("App needs a restart. You are out of scope.");
                    break;
            }
        }

        private async Task<bool> IsServerOnline()
        {
            CancellationTokenSource cts = new();
            cts.CancelAfter(TimeSpan.FromSeconds(2));

            HttpRequestMessage req = new(HttpMethod.Get, $"{_client.BaseAddress}health");
            HttpResponseMessage res = await _client.SendAsync(req, cts.Token);

            return res.IsSuccessStatusCode;
        }
    }
}
