using WebSocketServer;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton<RobotWebSocketHandler>();

var app = builder.Build();

// Configure the WebSocket request pipeline.
WebSocketOptions wsOptions = new() { KeepAliveInterval = TimeSpan.FromSeconds(120) };
app.UseWebSockets(wsOptions);

// Use the robot WebSocket Controller for WebSocket management.
RobotWebSocketHandler robotController = app.Services.GetRequiredService<RobotWebSocketHandler>();
app.Use(robotController.Control);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseRouting();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

await Task.WhenAll(robotController.Processes);
