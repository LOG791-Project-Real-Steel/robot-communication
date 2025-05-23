using System.Net;
using System.Net.WebSockets;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseRouting();
WebSocketOptions wsOptions = new() { KeepAliveInterval = TimeSpan.FromSeconds(120) };
app.UseWebSockets(wsOptions);

WebSocket? receiver = null;
HttpContext? receiverContext = null;
WebSocket? producer = null;
HttpContext? producerContext = null;

Task? receiverTask = null;
Task? producerTask = null;

async Task Process()
{
    if (producer is null)
        return;

    byte[] buffer = new byte[256];
    WebSocketReceiveResult result = await producer.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

    if (result is not null)
    {
        while (!result.CloseStatus.HasValue)
        {
            if (receiver is null)
                continue;

            await receiver.SendAsync(new ArraySegment<byte>(buffer), result.MessageType, result.EndOfMessage, CancellationToken.None);
            buffer = new byte[256];
            result = await producer.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        }
        await producer.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);

        if (receiver is null)
            return;

        await receiver.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
    }
}

async Task Receive()
{
    if (receiver is null)
        return;

    byte[] buffer = new byte[256];
    WebSocketReceiveResult result = await receiver.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

    if (result is not null)
    {
        while (!result.CloseStatus.HasValue)
        {
            if (producer is null)
                continue;

            await producer.SendAsync(new ArraySegment<byte>(buffer), result.MessageType, result.EndOfMessage, CancellationToken.None);
            buffer = new byte[256];
            result = await receiver.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        }

        if (receiver is not null)
            await receiver.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
    }
}

app.Use((Func<HttpContext, Func<Task>, Task>)(async (context, next) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;

    if (context.Request.Path == "/send")
    {
        producer = await context.WebSockets.AcceptWebSocketAsync();
        producerContext = context;
        await Process();
    }

    if (context.Request.Path == "/receive")
    {
        receiver = await context.WebSockets.AcceptWebSocketAsync();
        await Receive();
    }
}));

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

await Task.WhenAll(receiverTask ?? Task.CompletedTask, producerTask ?? Task.CompletedTask);
