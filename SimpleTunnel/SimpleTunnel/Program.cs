// See https://aka.ms/new-console-template for more information
using System.Net.WebSockets;
using System.Text;

await Task.Delay(1000);

using ClientWebSocket client = new();
Uri uri = new Uri("ws://localhost:5000/receive");
CancellationTokenSource cts = new();
cts.CancelAfter(TimeSpan.FromSeconds(120));
try
{
    await client.ConnectAsync(uri, cts.Token);
    int n = 0;

    while (client.State == WebSocketState.Open)
    {
        byte[] resBuffer = new byte[1024];
        int offset = 0;
        int packet = 1024;
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