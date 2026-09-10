using System;
using UnityEngine;
using UnityEngine.Serialization;

public enum ActionAnimationPhase : byte
{
    None = 0,
    Cast = 1,
    Main = 2,
    Recovery = 3
}

public enum ActionBodyMask : byte
{
    FullBody = 0,
    UpperBody,
    ArmsOnly
}

public enum ActionAimComposition : byte
{
    ProceduralOverride = 0,
    AnimationOnly,
    AnimationWithBodyAim
}

public enum ActionHandIkPolicy : byte
{
    Inherit = 0,
    AnimatedTargets,
    WeaponGrips
}

[Serializable]
public struct ActionAnimationClipData
{
    [SerializeField]
    private AnimationClip clip;

    [SerializeField]
    private ActionBodyMask bodyMask;

    [Tooltip(
        "Procedural Override는 CCD가 포즈를 만들고, Animation Only는 클립을 그대로 사용하며, " +
        "Animation With Body Aim은 클립 포즈에 조준 회전만 더합니다.")]
    [SerializeField]
    private ActionAimComposition aimComposition;

    [Tooltip(
        "Inherit는 현재 장착 상태를 유지합니다. Animated Targets는 클립의 손 IK Target을, " +
        "Weapon Grips는 장착 무기의 Grip을 사용합니다.")]
    [SerializeField]
    private ActionHandIkPolicy handIkPolicy;

    public AnimationClip Clip => clip;

    public ActionBodyMask BodyMask => bodyMask;

    public ActionAimComposition AimComposition =>
        aimComposition;

    public ActionHandIkPolicy HandIkPolicy =>
        handIkPolicy;

    public bool HasClip => clip != null;

    public ActionAnimationClipData(
        AnimationClip clip,
        ActionBodyMask bodyMask,
        ActionAimComposition aimComposition,
        ActionHandIkPolicy handIkPolicy)
    {
        this.clip = clip;
        this.bodyMask = bodyMask;
        this.aimComposition = aimComposition;
        this.handIkPolicy = handIkPolicy;
    }
}

[CreateAssetMenu(
    menuName = "Mazzang/Data/Animation/Action",
    fileName = "ActionAnimation")]
public class ActionAnimationData : ScriptableObject
{
    [Header("Cast")]
    [SerializeField]
    private ActionAnimationClipData cast;

    [Header("Main")]
    [FormerlySerializedAs("release")]
    [SerializeField]
    private ActionAnimationClipData main;

    [Header("Recovery")]
    [SerializeField]
    private ActionAnimationClipData recovery;

    public virtual bool HasAnyClip =>
        cast.HasClip ||
        main.HasClip ||
        recovery.HasClip;

    public virtual ActionAnimationClipData GetClipData(
        ActionAnimationPhase phase)
    {
        return phase switch
        {
            ActionAnimationPhase.Cast => cast,
            ActionAnimationPhase.Main => main,
            ActionAnimationPhase.Recovery => recovery,
            _ => default
        };
    }
}
