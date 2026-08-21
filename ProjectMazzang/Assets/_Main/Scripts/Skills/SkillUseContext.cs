using UnityEngine;

public readonly struct SkillUseContext
{
    public Vector2 MoveInput
    {
        get;
    }

    public Vector2 AimWorldPosition
    {
        get;
    }


    public SkillUseContext(
        Vector2 moveInput,
        Vector2 aimWorldPosition)
    {
        MoveInput =
            moveInput;

        AimWorldPosition =
            aimWorldPosition;
    }
}