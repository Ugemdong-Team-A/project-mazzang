using UnityEngine;

/// <summary>
/// 충돌 전까지 실제 투사체가 보관하고 Host 판정에 투입하는 확정 전투 값입니다.
/// </summary>
public readonly struct ProjectileCombatPayload
{
    public int AttackId
    {
        get;
    }

    public int Damage
    {
        get;
    }

    public Vector2 LocalKnockback
    {
        get;
    }

    public CrowdControlDefinition CrowdControl
    {
        get;
    }

    public bool HasDamage =>
        Damage > 0;

    public bool HasKnockback =>
        LocalKnockback.sqrMagnitude >
        0.0001f;


    public ProjectileCombatPayload(
        int attackId,
        int damage,
        Vector2 localKnockback,
        CrowdControlDefinition crowdControl)
    {
        AttackId =
            Mathf.Max(
                0,
                attackId);

        Damage =
            Mathf.Max(
                0,
                damage);

        LocalKnockback =
            localKnockback;

        CrowdControl =
            crowdControl;
    }
}
