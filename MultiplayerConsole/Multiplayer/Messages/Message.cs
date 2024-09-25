namespace MultiplayerConsole.Multiplayer.Messages;

public class Message<T>
{
    public string Type { get; set; }
    public T Payload { get; set; }
}
