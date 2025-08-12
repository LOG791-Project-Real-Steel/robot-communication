// See https://aka.ms/new-console-template for more information
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
Uri uri = new Uri($"ws://[::]:{port}/robot");
CancellationTokenSource cts = new();
cts.CancelAfter(TimeSpan.FromSeconds(120));
try
{
    await client.ConnectAsync(uri, cts.Token);

    const int offset = 0;
    while (client.State == WebSocketState.Open)
    {
        var resBuffer = new byte[1024];
        const int packet = 1024;
        while (true)
        {
            ArraySegment<byte> bytesRes = new ArraySegment<byte>(resBuffer, offset, packet);
            WebSocketReceiveResult res = await client.ReceiveAsync(bytesRes, cts.Token);
            string resMessage = Encoding.UTF8.GetString(resBuffer, offset, res.Count);
            Console.WriteLine(resMessage);
        }
    }
}
catch (WebSocketException e)
{
    Console.WriteLine(e.Message);
}

Console.ReadLine();