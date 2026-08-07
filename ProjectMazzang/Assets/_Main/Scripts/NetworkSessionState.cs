using Fusion;

public enum NetworkSessionState
{
    Offline = 0,

    LobbyConnecting,
    LobbyReady,

    RoomConnecting,
    InRoom,

    ShuttingDown
}

public enum NetworkOperation
{
    None = 0,

    ConnectLobby,
    CreateRoom,
    JoinRoom,
    LeaveRoom,

    ConnectionLost
}

public readonly struct NetworkOperationFailure
{
    public NetworkOperation Operation { get; }
    public ShutdownReason? ShutdownReason { get; }
    public string Message { get; }

    public NetworkOperationFailure(
        NetworkOperation operation,
        string message,
        ShutdownReason? shutdownReason = null)
    {
        Operation = operation;
        Message = message;
        ShutdownReason = shutdownReason;
    }
}