using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using MultiplayerConsole.Multiplayer.Messages;

namespace MultiplayerConsole.Multiplayer;

public class MultiplayerConnection : IDisposable
{
    private TcpClient _client;
    private NetworkStream _networkStream;

    public event Action<SyncWorldState> OnWorldStateReceived;
    public event Action<ChatMessage> OnChatMessageReceived;
    public event Action<PlayerEvent> OnPlayerEventReceived;

    public async Task Connect(string ip, int port)
    {
        _client = new TcpClient();

        await _client.ConnectAsync(ip, port);
        _networkStream = _client.GetStream();

        _ = Task.Run(ReceiveMessages);
    }

    public async Task SendPositionAsync(UpdatedPlayerState updatedPositionState)
    {
        var msg = new Message<UpdatedPlayerState>
        {
            Type = "position",
            Payload = updatedPositionState
        };
        await SendMessageAsync(msg);
    }

    public async Task SendChatMessageAsync(ChatMessage chatMessage)
    {
        var msg = new Message<ChatMessage>
        {
            Type = "chat",
            Payload = chatMessage
        };
        await SendMessageAsync(msg);
    }

    public async Task SendExitMessageAsync(Player player)
    {
        var msg = new Message<Player>
        {
            Type = "exit",
            Payload = player
        };
        await SendMessageAsync(msg);
    }

    private async Task SendMessageAsync<T>(Message<T> message)
    {
        var jsonString = JsonSerializer.Serialize(message);
        var buffer = Encoding.UTF8.GetBytes(jsonString + "\n");
        await _networkStream.WriteAsync(buffer, 0, buffer.Length);
    }

    private async Task ReceiveMessages()
    {
        var buffer = new byte[1024];
        while (true)
        {
            var byteCount = await _networkStream.ReadAsync(buffer, 0, buffer.Length);
            if (byteCount == 0) return;

            foreach (var item in ReadStructures(buffer, buffer.Length, byteCount))
            {
                var jsonString = Encoding.UTF8.GetString(item, 0, item.Length);

                var msg = JsonSerializer.Deserialize<Message<object>>(jsonString, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                });

                switch (msg.Type)
                {
                    case "world_state":
                        var worldState = JsonSerializer.Deserialize<SyncWorldState>(msg.Payload.ToString(), new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        });
                        OnWorldStateReceived?.Invoke(worldState);
                        break;
                    case "chat":
                        var chatMsg = JsonSerializer.Deserialize<ChatMessage>(msg.Payload.ToString(), new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        });
                        OnChatMessageReceived?.Invoke(chatMsg);
                        break;
                    case "player_event":
                        var playerEvent = JsonSerializer.Deserialize<PlayerEvent>(msg.Payload.ToString(), new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        });
                        OnPlayerEventReceived?.Invoke(playerEvent);
                        break;
                    default:
                        Console.WriteLine("Unknown message type");
                        break;
                }
            }
        }
    }

    public IEnumerable<byte[]> ReadStructures(byte[] buffer, int bufferSize, int bytesRead)
    {
        using var memoryStream = new MemoryStream();
        int prevOffset = 0;

        int startIndex = 0;

        for (int i = 0; i < bytesRead; i++)
        {
            if (buffer[i] == 0)
            {
                // Записываем данные до символа \0 в memoryStream
                if (memoryStream.Length > 0 || i > startIndex)
                {
                    memoryStream.Write(buffer, startIndex, i - startIndex);
                    yield return memoryStream.ToArray();
                    memoryStream.SetLength(0); // Очистить memoryStream для следующей структуры
                }

                startIndex = i + 1; // Обновляем начальный индекс для следующей структуры
            }
        }

        if (startIndex < bytesRead)
        {
            // Записываем оставшиеся данные в memoryStream
            memoryStream.Write(buffer, startIndex, bytesRead - startIndex);
        }

        prevOffset = bytesRead - startIndex;

        // Если есть данные в memoryStream после завершения чтения
        if (memoryStream.Length > 0)
        {
            yield return memoryStream.ToArray();
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
        _networkStream?.Dispose();
    }
}
