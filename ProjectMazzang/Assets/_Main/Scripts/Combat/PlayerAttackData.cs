using UnityEngine;

public enum PlayerAttackMovementMode : byte
{
    Free = 0,
    Locked
}

[CreateAssetMenu(
    menuName = "Mazzang/Data/Combat/Player Attack",
    fileName = "PlayerAttackData")]
public sealed class PlayerAttackData :
    ScriptableObject
{
    [SerializeField]
    private AttackData attack;

    [Header("Timing")]
    [SerializeField]
    [Min(0f)]
    private float startupDuration;

    [SerializeField]
    [Min(0f)]
    private float activeDuration;

    [SerializeField]
    [Min(0f)]
    private float recoveryDuration;

    [SerializeField]
    [Min(0f)]
    private float cooldown;

    [Header("Player Rules")]
    [SerializeField]
    private PlayerAttackAimData aim;

    [SerializeField]
    private PlayerAttackMovementMode movementMode;

    [Tooltip(
        "공격 시작과 동시에 Aim 방향으로 실행할 대시입니다. " +
        "비워두면 공격은 대시하지 않습니다.")]
    [SerializeField]
    private DashData dash;

    [Header("Combo")]
    [Tooltip(
        "이 공격의 Active 시작부터 Recovery 종료까지 공격 입력을 받으면 이어서 실행할 공격입니다.")]
    [SerializeField]
    private PlayerAttackData comboFollowUp;

    [Tooltip(
        "켜면 콤보 입력을 여러 번 눌러도 한 번 입력한 것으로 취급합니다. " +
        "끄면 정확히 한 번 입력해야 콤보가 실행됩니다.")]
    [SerializeField]
    private bool allowRepeatedComboInput = true;


    public AttackData Attack =>
        attack;

    public float StartupDuration =>
        startupDuration;

    public float ActiveDuration =>
        activeDuration;

    public float RecoveryDuration =>
        recoveryDuration;

    public float Cooldown =>
        cooldown;

    public PlayerAttackAimData Aim =>
        aim;

    public PlayerAttackMovementMode MovementMode =>
        movementMode;

    public DashData Dash =>
        dash;

    public PlayerAttackData ComboFollowUp =>
        comboFollowUp;

    public bool AllowRepeatedComboInput =>
        allowRepeatedComboInput;


    public bool IsValid =>
        attack != null;
}
