// See https://aka.ms/new-console-template for more information
using Newtonsoft.Json;
using System.Net.WebSockets;
using System.Text;

await Task.Delay(1000);

var port = Environment.GetEnvironmentVariable("PORT");
if (port is null)
{
    Console.WriteLine("Environment variable PORT was not found in WebSocketProducer. Defaulting to 5000...");
    port = "5000";
}

using ClientWebSocket client = new();
Uri uri = new($"ws://[::]:{port}/oculus");
CancellationTokenSource cts = new();
cts.CancelAfter(TimeSpan.FromSeconds(120));
try
{
    await client.ConnectAsync(uri, cts.Token);
    while (client.State == WebSocketState.Open)
    {
        Console.WriteLine("Enter message to send");
        string message = Console.ReadLine()!;

        if (string.IsNullOrEmpty(message))
            continue;

        Message msg = new(message);

        string json = JsonConvert.SerializeObject(msg);

        ArraySegment<byte> buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(json));
        await client.SendAsync(buffer, WebSocketMessageType.Text, true, cts.Token);
    }
}
catch (WebSocketException e)
{
    Console.WriteLine(e.Message);
}

Console.ReadLine();

record Message(string message);
