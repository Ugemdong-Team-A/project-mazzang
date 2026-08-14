using Fusion;
using UnityEngine;

/// <summary>
/// 플레이어가 보유한 스킬 슬롯과
/// 플레이어별 Runtime Skill 인스턴스를 관리합니다.
///
/// 입력 감지, 공통 사용 가능 여부, 쿨타임은
/// PlayerSkillController가 담당하고,
/// 실제 스킬별 행동은 Skill이 담당합니다.
/// </summary>
[DefaultExecutionOrder(-80)]
public sealed class PlayerSkillController :
    PlayerModule
{
    [Header("Default Skills")]
    [SerializeField]
    private SkillData skill1;

    [SerializeField]
    private SkillData skill2;


    private Skill _skill1;
    private Skill _skill2;

    private IPlayerHealthState
        _healthState;


    // =========================================================
    // Network State
    // =========================================================

    [Networked]
    private NetworkButtons PreviousButtons
    {
        get;
        set;
    }


    [Networked]
    private TickTimer Skill1CooldownTimer
    {
        get;
        set;
    }


    [Networked]
    private TickTimer Skill2CooldownTimer
    {
        get;
        set;
    }


    // =========================================================
    // Public State
    // =========================================================

    public Skill Skill1 =>
        _skill1;

    public Skill Skill2 =>
        _skill2;


    public bool IsSkill1OnCooldown =>
        !Skill1CooldownTimer
            .ExpiredOrNotRunning(
                Runner);


    public bool IsSkill2OnCooldown =>
        !Skill2CooldownTimer
            .ExpiredOrNotRunning(
                Runner);


    // =========================================================
    // Context
    // =========================================================

    protected override void OnContextReady()
    {
        _healthState =
            Context.Get<
                IPlayerHealthState>();

        _skill1 =
            CreateSkill(
                skill1,
                SkillSlot.Skill1);

        _skill2 =
            CreateSkill(
                skill2,
                SkillSlot.Skill2);
    }


    // =========================================================
    // Fusion
    // =========================================================

    public override void Spawned()
    {
        if (!HasStateAuthority)
            return;

        PreviousButtons =
            default;

        Skill1CooldownTimer =
            TickTimer.None;

        Skill2CooldownTimer =
            TickTimer.None;
    }


    public override void FixedUpdateNetwork()
    {
        if (!IsContextReady)
            return;


        // ==========================================
        // Runtime Skill Update
        // ==========================================

        _skill1?
            .FixedUpdateNetwork();

        _skill2?
            .FixedUpdateNetwork();


        bool hasInput =
            GetInput(
                out PlayerInputData input);


        // ==========================================
        // Dead
        // ==========================================

        if (_healthState == null ||
            !_healthState.IsAlive)
        {
            CancelAll();

            if (hasInput)
            {
                PreviousButtons =
                    input.Buttons;
            }

            return;
        }


        if (!hasInput)
            return;


        // ==========================================
        // Skill Input
        // ==========================================

        bool skill1Pressed =
            input.Buttons.WasPressed(
                PreviousButtons,
                PlayerButton.Skill1);

        bool skill2Pressed =
            input.Buttons.WasPressed(
                PreviousButtons,
                PlayerButton.Skill2);

        PreviousButtons =
            input.Buttons;


        SkillUseContext useContext =
            new(
                input.Move,
                input.AimWorldPosition);


        if (skill1Pressed)
        {
            TryUse(
                SkillSlot.Skill1,
                in useContext);
        }

        if (skill2Pressed)
        {
            TryUse(
                SkillSlot.Skill2,
                in useContext);
        }
    }


    public override void Despawned(
        NetworkRunner runner,
        bool hasState)
    {
        DisposeSkills();
    }


    // =========================================================
    // Use
    // =========================================================

    public bool TryUse(
        SkillSlot slot,
        in SkillUseContext useContext)
    {
        Skill skill =
            GetSkill(
                slot);

        if (skill == null)
            return false;

        if (IsOnCooldown(
                slot))
        {
            return false;
        }

        if (!skill.CanUse(
                in useContext))
        {
            return false;
        }

        skill.Activate(
            in useContext);

        StartCooldown(
            slot,
            skill.Data.Cooldown);

        return true;
    }


    // =========================================================
    // Cooldown
    // =========================================================

    public bool IsOnCooldown(
        SkillSlot slot)
    {
        return slot switch
        {
            SkillSlot.Skill1 =>
                !Skill1CooldownTimer
                    .ExpiredOrNotRunning(
                        Runner),

            SkillSlot.Skill2 =>
                !Skill2CooldownTimer
                    .ExpiredOrNotRunning(
                        Runner),

            _ =>
                false
        };
    }


    private void StartCooldown(
        SkillSlot slot,
        float duration)
    {
        TickTimer timer =
            duration > 0f
                ? TickTimer.CreateFromSeconds(
                    Runner,
                    duration)
                : TickTimer.None;

        switch (slot)
        {
            case SkillSlot.Skill1:
                Skill1CooldownTimer =
                    timer;
                break;


            case SkillSlot.Skill2:
                Skill2CooldownTimer =
                    timer;
                break;
        }
    }


    // =========================================================
    // Skill Access
    // =========================================================

    public Skill GetSkill(
        SkillSlot slot)
    {
        return slot switch
        {
            SkillSlot.Skill1 =>
                _skill1,

            SkillSlot.Skill2 =>
                _skill2,

            _ =>
                null
        };
    }


    public SkillData GetSkillData(
        SkillSlot slot)
    {
        return GetSkill(
            slot)?.Data;
    }


    // =========================================================
    // Equip
    // =========================================================

    public bool Equip(
        SkillSlot slot,
        SkillData data)
    {
        Skill newSkill =
            CreateSkill(
                data,
                slot);

        if (data != null &&
            newSkill == null)
        {
            return false;
        }

        Skill previousSkill =
            GetSkill(
                slot);

        previousSkill?
            .Dispose();

        SetSkill(
            slot,
            newSkill);

        ClearCooldown(
            slot);

        return true;
    }


    public void Unequip(
        SkillSlot slot)
    {
        Skill skill =
            GetSkill(
                slot);

        skill?
            .Dispose();

        SetSkill(
            slot,
            null);

        ClearCooldown(
            slot);
    }


    private void ClearCooldown(
        SkillSlot slot)
    {
        switch (slot)
        {
            case SkillSlot.Skill1:
                Skill1CooldownTimer =
                    TickTimer.None;
                break;


            case SkillSlot.Skill2:
                Skill2CooldownTimer =
                    TickTimer.None;
                break;
        }
    }


    // =========================================================
    // Cancel
    // =========================================================

    public void Cancel(
        SkillSlot slot)
    {
        GetSkill(
            slot)?
            .Cancel();
    }


    public void CancelAll()
    {
        _skill1?
            .Cancel();

        _skill2?
            .Cancel();
    }


    // =========================================================
    // Creation
    // =========================================================

    private Skill CreateSkill(
        SkillData data,
        SkillSlot slot)
    {
        if (data == null)
            return null;

        Skill skill =
            data.CreateSkill();

        if (skill == null)
        {
            Debug.LogError(
                $"[{nameof(PlayerSkillController)}] " +
                $"{data.name}이 Runtime Skill을 " +
                "생성하지 못했습니다.",
                data);

            return null;
        }

        skill.Initialize(
            data,
            slot,
            this,
            Context);

        return skill;
    }


    private void SetSkill(
        SkillSlot slot,
        Skill skill)
    {
        switch (slot)
        {
            case SkillSlot.Skill1:
                _skill1 =
                    skill;
                break;


            case SkillSlot.Skill2:
                _skill2 =
                    skill;
                break;


            default:
                Debug.LogError(
                    $"지원하지 않는 SkillSlot입니다: {slot}",
                    this);
                break;
        }
    }


    // =========================================================
    // Dispose
    // =========================================================

    private void DisposeSkills()
    {
        _skill1?
            .Dispose();

        _skill2?
            .Dispose();

        _skill1 =
            null;

        _skill2 =
            null;
    }
}