using MultiplayerConsole.Multiplayer;
using MultiplayerConsole.Multiplayer.Messages;

namespace MultiplayerConsole;

public class Program
{
    private static MultiplayerConnection _connection;

    private static Player _player;
    private static readonly float StepSize = 1.0f;

    public static async Task Main(string[] args)
    {
        using (_connection = new MultiplayerConnection())
        {
            try
            {
                await _connection.Connect("127.0.0.1", 8080);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Не удалось подключиться");
                return;
            }

            _player = new Player { X = 10, Y = 20, Z = 30 };

            _connection.OnWorldStateReceived += HandleWorldState;
            _connection.OnChatMessageReceived += HandleChatMessage;
            _connection.OnPlayerEventReceived += HandlePlayerEvent;

            _ = Task.Run(HandleKeyboardInput);
            _ = Task.Run(SendPositionPeriodically);

            await Task.Delay(120000);

            await _connection.SendExitMessageAsync(_player);
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
            await _connection.SendPositionAsync(new UpdatedPlayerState
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

    private static void HandleWorldState(SyncWorldState worldState)
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
