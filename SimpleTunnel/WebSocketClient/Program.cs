// See https://aka.ms/new-console-template for more information
using Newtonsoft.Json;
using System.Net.WebSockets;
using System.Text;

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
