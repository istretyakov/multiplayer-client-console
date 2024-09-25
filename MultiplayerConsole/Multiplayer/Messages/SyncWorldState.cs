namespace MultiplayerConsole.Multiplayer.Messages;

public class SyncWorldState
{
    public List<SyncPlayer> Players { get; set; }
    public Weather Weather { get; set; }
    public DateTime Timestamp { get; set; }
}
