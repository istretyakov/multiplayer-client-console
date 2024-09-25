using System.Net.Sockets;
using System.Text;
using System.Text.Json;

public class Program
{
    private static TcpClient _client;
    private static NetworkStream _networkStream;
    private static Player _player;
    private static readonly float StepSize = 1.0f;

    public static event Action<WorldState> OnWorldStateReceived;
    public static event Action<ChatMessage> OnChatMessageReceived;
    public static event Action<PlayerEvent> OnPlayerEventReceived;

    public static async Task Main(string[] args)
    {
        using (_client = new TcpClient())
        {
            await _client.ConnectAsync("127.0.0.1", 8080);
            using (_networkStream = _client.GetStream())
            {

                _player = new Player { X = 10, Y = 20, Z = 30 };

                // Подписка на события сервера
                OnWorldStateReceived += HandleWorldState;
                OnChatMessageReceived += HandleChatMessage;
                OnPlayerEventReceived += HandlePlayerEvent;

                // Запуск потоков для обработки ввода и сообщений
                _ = Task.Run(HandleKeyboardInput);
                _ = Task.Run(ReceiveMessages);
                _ = Task.Run(SendPositionPeriodically);

                // Задержка для демонстрации (например, 30 секунд работы клиента)
                await Task.Delay(120000);

                // Пример отправки сообщения о выходе
                await SendExitMessageAsync(_player);
            }
        }
    }

    private static async Task HandleKeyboardInput()
    {
        while (true)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true).Key;

                switch (key)
                {
                    case ConsoleKey.W:
                        _player.Y += StepSize;
                        break;
                    case ConsoleKey.A:
                        _player.X -= StepSize;
                        break;
                    case ConsoleKey.S:
                        _player.Y -= StepSize;
                        break;
                    case ConsoleKey.D:
                        _player.X += StepSize;
                        break;
                }
            }

            await Task.Delay(50); // Маленькая задержка для уменьшения нагрузки на CPU
        }
    }

    private static async Task SendPositionPeriodically()
    {
        while (true)
        {
            await SendPositionAsync(new UpdatedPositionState
            {
                Position = new Vector3
                {
                    X = _player.X,
                    Y = _player.Y,
                    Z = _player.Z
                }
            });
            await Task.Delay(1000 / 10); // 10 раз в секунду
        }
    }

    private static async Task SendPositionAsync(UpdatedPositionState updatedPositionState)
    {
        var msg = new Message<UpdatedPositionState>
        {
            Type = "position",
            Payload = updatedPositionState
        };
        await SendMessageAsync(msg);
    }

    private static async Task SendChatMessageAsync(ChatMessage chatMessage)
    {
        var msg = new Message<ChatMessage>
        {
            Type = "chat",
            Payload = chatMessage
        };
        await SendMessageAsync(msg);
    }

    private static async Task SendExitMessageAsync(Player player)
    {
        var msg = new Message<Player>
        {
            Type = "exit",
            Payload = player
        };
        await SendMessageAsync(msg);
    }

    private static async Task SendMessageAsync<T>(Message<T> message)
    {
        var jsonString = JsonSerializer.Serialize(message);
        var buffer = Encoding.UTF8.GetBytes(jsonString + "\n");
        await _networkStream.WriteAsync(buffer, 0, buffer.Length);
    }

    private static async Task ReceiveMessages()
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
                        var worldState = JsonSerializer.Deserialize<WorldState>(msg.Payload.ToString(), new JsonSerializerOptions
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

    public static IEnumerable<byte[]> ReadStructures(byte[] buffer, int bufferSize, int bytesRead)
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

    private static void HandleWorldState(WorldState worldState)
    {
        Console.WriteLine($"World Update at {worldState.Timestamp}:");
        foreach (var player in worldState.Players)
        {
            Console.WriteLine($"Player {player.Id}: Position ({player.Position.X}, {player.Position.Y}, {player.Position.Z})");
        }
    }

    private static void HandleChatMessage(ChatMessage chatMessage)
    {
        Console.WriteLine($"Chat Message from {chatMessage.Id}: {chatMessage.Message}");
    }

    private static void HandlePlayerEvent(PlayerEvent playerEvent)
    {
        Console.WriteLine($"Player {playerEvent.Id} has {playerEvent.Event}");
    }
}

// Структуры для данных
public class Player
{
    public int Id { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
}

public class UpdatedPositionState
{
    public Vector3 Position { get; set; }
}

public class Vector3
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
}

public class Weather
{
    public string Condition { get; set; }
    public float Temperature { get; set; }
}

public class WorldStatePlayer
{
    public int Id { get; set; }

    public Vector3 Position { get; set; }
}

public class WorldState
{
    public List<WorldStatePlayer> Players { get; set; }
    public Weather Weather { get; set; }
    public DateTime Timestamp { get; set; }
}

public class ChatMessage
{
    public int Id { get; set; }
    public string Message { get; set; }
}

public class PlayerEvent
{
    public int Id { get; set; }
    public string Event { get; set; }
}

public class Message<T>
{
    public string Type { get; set; }
    public T Payload { get; set; }
}
