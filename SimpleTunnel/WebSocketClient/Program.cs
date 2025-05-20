// See https://aka.ms/new-console-template for more information
using System.Net.WebSockets;
using System.Text;

await Task.Delay(1000);

using ClientWebSocket client = new();
Uri uri = new Uri("ws://localhost:5000/send");
CancellationTokenSource cts = new();
cts.CancelAfter(TimeSpan.FromSeconds(120));
try
{
    await client.ConnectAsync(uri, cts.Token);
    int n = 0;
    while (client.State == WebSocketState.Open)
    {
        Console.WriteLine("Enter message to send");
        string message = Console.ReadLine();

        if (string.IsNullOrEmpty(message))
            continue;

        ArraySegment<byte> buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
        await client.SendAsync(buffer, WebSocketMessageType.Text, true, cts.Token);
    }
}
catch (WebSocketException e)
{
    Console.WriteLine(e.Message);
}

Console.ReadLine();