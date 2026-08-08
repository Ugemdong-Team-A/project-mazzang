using Fusion;

public enum PlayerButton
{
    Jump = 0,
}

public struct PlayerInputData : INetworkInput
{
    public float MoveX;
    public NetworkButtons Buttons;
}