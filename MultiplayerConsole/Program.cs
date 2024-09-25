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
        _client = new TcpClient();
        await _client.ConnectAsync("127.0.0.1", 8080);
        _networkStream = _client.GetStream();

        _player = new Player { ID = "Player1", X = 10, Y = 20, Z = 30 };

        // Подписка на события сервера
        OnWorldStateReceived += HandleWorldState;
        OnChatMessageReceived += HandleChatMessage;
        OnPlayerEventReceived += HandlePlayerEvent;

        // Запуск потоков для обработки ввода и сообщений
        _ = Task.Run(HandleKeyboardInput);
        _ = Task.Run(ReceiveMessages);
        _ = Task.Run(SendPositionPeriodically);

        // Задержка для демонстрации (например, 30 секунд работы клиента)
        await Task.Delay(30000);

        // Пример отправки сообщения о выходе
        await SendExitMessageAsync(_player);

        // Закрытие соединения
        _networkStream.Close();
        _client.Close();
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
            await SendPositionAsync(_player);
            await Task.Delay(1000 / 10); // 10 раз в секунду
        }
    }

    private static async Task SendPositionAsync(Player player)
    {
        var msg = new Message<Player>
        {
            Type = "position",
            Payload = player
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

            var jsonString = Encoding.UTF8.GetString(buffer, 0, byteCount);
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

    private static void HandleWorldState(WorldState worldState)
    {
        Console.WriteLine($"World Update at {worldState.Timestamp}:");
        foreach (var player in worldState.Players)
        {
            Console.WriteLine($"Player {player.ID}: Position ({player.X}, {player.Y}, {player.Z})");
        }
    }

    private static void HandleChatMessage(ChatMessage chatMessage)
    {
        Console.WriteLine($"Chat Message from {chatMessage.ID}: {chatMessage.Message}");
    }

    private static void HandlePlayerEvent(PlayerEvent playerEvent)
    {
        Console.WriteLine($"Player {playerEvent.ID} has {playerEvent.Event}");
    }
}

// Структуры для данных
public class Player
{
    public string ID { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
}

public class Weather
{
    public string Condition { get; set; }
    public float Temperature { get; set; }
}

public class WorldState
{
    public List<Player> Players { get; set; }
    public Weather Weather { get; set; }
    public DateTime Timestamp { get; set; }
}

public class ChatMessage
{
    public string ID { get; set; }
    public string Message { get; set; }
}

public class PlayerEvent
{
    public string ID { get; set; }
    public string Event { get; set; }
}

public class Message<T>
{
    public string Type { get; set; }
    public T Payload { get; set; }
}
