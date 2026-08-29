using UnityEngine;

[CreateAssetMenu(
    fileName = "ParryData",
    menuName = "Game/Combat/Parry")]
public sealed class ParryData : ScriptableObject
{
    [Min(0.01f)] [SerializeField] private float activeDuration = 0.18f;
    [Min(0f)] [SerializeField] private float cooldown = 1.1f;
    [Min(0.1f)] [SerializeField] private float radius = 1f;
    [Range(10f, 180f)] [SerializeField] private float arcAngle = 110f;
    [Range(0f, 1f)] [SerializeField] private float aimInfluence = 0.85f;
    [Min(0f)] [SerializeField] private float speedMultiplier = 1.15f;
    [SerializeField] private float anchorForwardOffset = 0.45f;

    public float ActiveDuration => activeDuration;
    public float Cooldown => cooldown;
    public float Radius => radius;
    public float HalfAngle => arcAngle * 0.5f;
    public float AimInfluence => aimInfluence;
    public float SpeedMultiplier => speedMultiplier;
    public float AnchorForwardOffset => anchorForwardOffset;
}
