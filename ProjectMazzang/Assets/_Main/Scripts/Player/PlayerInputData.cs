using Fusion;
using UnityEngine;

public enum PlayerButton
{
    Jump = 0,
    Attack,
    Drop,
    Skill1,
    Skill2,
    Parry
}

public struct PlayerInputData :
    INetworkInput
{
    public Vector2 Move;

    /// <summary>
    /// 로컬 입력에서 계산한 마우스의 월드 좌표입니다.
    /// 실제 조준 방향 해석은 PlayerAim이 담당합니다.
    /// </summary>
    public Vector2 AimWorldPosition;

    public NetworkButtons Buttons;
}