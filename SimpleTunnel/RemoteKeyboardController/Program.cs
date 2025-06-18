// See https://aka.ms/new-console-template for more information
using RemoteKeyboardController;

var port = Environment.GetEnvironmentVariable("PORT");
if (port is null)
{
    Console.WriteLine("Environment variable PORT was not found in WebSocketProducer. Defaulting to 5000...");
    port = "5000";
}

ConsoleHandler handler = new($"[::]:{port}");

handler.Run();