using UnityEngine;

/// <summary>
/// 활성 스킬이 플레이어 기본 수치에 적용하는 배율 집합입니다.
/// 실제 수치의 소유권은 각 플레이어 모듈에 그대로 둡니다.
/// </summary>
public readonly struct PlayerStatModifiers
{
    public static PlayerStatModifiers Identity =>
        new(1f, 1f, 1f, 1f, 1f);

    public float MoveSpeed { get; }
    public float AttackDamage { get; }
    public float MaxHealth { get; }
    public float DamageTaken { get; }
    public float VisualScale { get; }

    public PlayerStatModifiers(
        float moveSpeed,
        float attackDamage,
        float maxHealth,
        float damageTaken,
        float visualScale)
    {
        MoveSpeed = Mathf.Max(0f, moveSpeed);
        AttackDamage = Mathf.Max(0f, attackDamage);
        MaxHealth = Mathf.Max(0.01f, maxHealth);
        DamageTaken = Mathf.Max(0f, damageTaken);
        VisualScale = Mathf.Max(0.01f, visualScale);
    }

    public PlayerStatModifiers Combine(
        in PlayerStatModifiers other)
    {
        return new PlayerStatModifiers(
            MoveSpeed * other.MoveSpeed,
            AttackDamage * other.AttackDamage,
            MaxHealth * other.MaxHealth,
            DamageTaken * other.DamageTaken,
            VisualScale * other.VisualScale);
    }
}

public interface IPlayerStatModifierSkill
{
    PlayerStatModifiers StatModifiers { get; }
}
