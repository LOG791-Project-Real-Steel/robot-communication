// See https://aka.ms/new-console-template for more information
using Newtonsoft.Json;
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
    Task recvTask = Receive();
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

    await recvTask;
}
catch (WebSocketException e)
{
    Console.WriteLine(e.Message);
}

Console.ReadLine();

async Task Receive()
{
    while (client.State == WebSocketState.Open)
    {
        byte[] buffer = new byte[256];
        WebSocketReceiveResult result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        await client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, cts.Token);
    }
}

record Message(string message);
