using WebSocketServer;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton<RobotWebSocketHandler>();

// Clear logs
builder.Logging.ClearProviders();

// Re-add it with a timestamped formatter
builder.Logging.AddSimpleConsole(opts =>
{
    // e.g. 2025-08-02 14:35:07.123 –
    opts.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
    // Use local time; switch to true for UTC
    opts.UseUtcTimestamp = false;
});


var app = builder.Build();

// Configure the WebSocket request pipeline.
WebSocketOptions wsOptions = new() { KeepAliveInterval = TimeSpan.FromSeconds(120) };
app.UseWebSockets(wsOptions);

// Use the robot WebSocket Controller for WebSocket management.
RobotWebSocketHandler robotController = app.Services.GetRequiredService<RobotWebSocketHandler>();
app.Use(robotController.Handle);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseRouting();

// Disabling https for now
// app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

await Task.WhenAll(robotController.Processes);
