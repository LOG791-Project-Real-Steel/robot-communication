using RemoteKeyboardController.Models;
using System.Net.WebSockets;
using System.Text;

namespace RemoteKeyboardController
{
    public class ConsoleHandler
    {
        public ConsoleHandler()
        {
            Task.Run(async () =>
            {
                while (state != State.Quit)
                {
                    // TODO: Change this to wait on Server UP (Check health)
                    await Task.Delay(1000);

                    using ClientWebSocket client = new();
                    Uri uri = new("ws://localhost:5000/send");
                    CancellationTokenSource cts = new();
                    cts.CancelAfter(TimeSpan.FromSeconds(120));
                    try
                    {
                        await client.ConnectAsync(uri, cts.Token);
                        while (client.State == WebSocketState.Open)
                        {
                            await Task.Delay(200);

                            if (Msg is null)
                                continue;

                            ArraySegment<byte> buffer = new(Encoding.UTF8.GetBytes(Msg.Json()));
                            await client.SendAsync(buffer, WebSocketMessageType.Text, true, cts.Token);

                            if (Msg is QuitMessage)
                                state = State.Quit;
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

        private Message? Msg;

        private State state = State.Main;

        public async Task RunAsync()
        {
            RefreshCMD();
            await ProcessAsync();
        }

        private async Task ProcessAsync()
        {
            ConsoleKeyInfo key;
            while (state != State.Quit)
            {
                RefreshCMD();
                try
                {
                    while (!Console.KeyAvailable || state == State.Quitting)
                        Thread.Sleep(50);

                    key = Console.ReadKey();
                    switch (key.Key)
                    {
                        case ConsoleKey.Q:
                        case ConsoleKey.Escape:
                            state = State.Quitting;
                            Msg = new QuitMessage();
                            break;

                        case ConsoleKey.W:
                            break;

                        case ConsoleKey.A:
                            break;

                        case ConsoleKey.S:
                            break;

                        case ConsoleKey.D:
                            break;

                        default:
                            Console.WriteLine($"Command [{key.KeyChar}] not implemented.");
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
    }
}
