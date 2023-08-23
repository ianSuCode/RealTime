using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyHeader()
               .AllowAnyMethod();
    });
});

var app = builder.Build();
app.UseCors();

var connections = new ConcurrentDictionary<string, WebSocket>();
var topics = new ConcurrentDictionary<string, string>();

app.UseWebSockets();
app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        try
        {
            var ws = await context.WebSockets.AcceptWebSocketAsync();
            var connectionId = Guid.NewGuid().ToString();
            connections.TryAdd(connectionId, ws);
            Console.WriteLine($"[WS] Connection({connectionId}) established. {connections.Count} users connected");

            // Register a callback for application shutdown
            var appLifetime = context.RequestServices.GetRequiredService<IHostApplicationLifetime>();
            appLifetime.ApplicationStopping.Register(async () =>
            {
                // Close the WebSocket connection gracefully during application shutdown
                if (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived)
                {
                    await ws.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Application is shutting down",
                        CancellationToken.None
                    );
                }
            });

            var buffer = new byte[1024 * 4];
            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close || ws.State == WebSocketState.Aborted)
                {
                    connections.TryRemove(connectionId, out ws);
                    topics.TryRemove(connectionId, out string? value);
                    Console.WriteLine($"[WS] Connection({connectionId}) aborted. {connections.Count} users remain");
                    break;
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    var receivedMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Console.WriteLine($"[WS] Received from Connection({connectionId}): {receivedMessage}");

                    if (receivedMessage.StartsWith("subscribe:"))
                    {
                        string topic = receivedMessage["subscribe:".Length..].Trim();
                        Console.WriteLine($"     topic={topic}");
                        topics[connectionId] = topic;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WS] Error: {ex.Message}");
        }
    }
    else
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
    }
});

app.MapGet("/send/{topic}/{message}", (string topic, string message) =>
{
    Console.WriteLine($"[WS] Send: topic={topic}; message={message}");
    var bytes = Encoding.UTF8.GetBytes($"{DateTime.Now};{message}");
    var arraySegment = new ArraySegment<byte>(bytes, 0, bytes.Length);

    var receivers = 0;
    foreach (var connection in connections)
    {
        if (connection.Value.State == WebSocketState.Open && topics.ContainsKey(connection.Key) && topics[connection.Key] == topic)
        {
            receivers++;
            connection.Value.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
    return $"Sent message to {receivers} clients.";
});

app.Run("http://localhost:5050");
