using UnityEngine;

public abstract class Skill { }

/// <summary>
/// 여러 플레이어가 공유하는 스킬 원본 데이터입니다.
/// 실제 플레이어별 런타임 상태는 Skill 인스턴스가 가집니다.
/// </summary>
public abstract class SkillData :
    ScriptableObject
{
    public abstract Skill CreateSkill();
}