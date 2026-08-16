using Fusion;
using UnityEngine;

/// <summary>
/// 플레이어의 Skill Slot과 Runtime Skill을 관리합니다.
///
/// 모든 Active Skill의 기본 Cooldown과,
/// Skill이 구현한 공통 패턴
/// (Charge / Cast / Duration / Recovery)의
/// Network Runtime State를 관리합니다.
///
/// 구체 스킬의 실제 행동은 Skill이 담당합니다.
/// </summary>
[DefaultExecutionOrder(-80)]
public sealed class PlayerSkillController :
    PlayerModule,
    IPlayerTickModule,
    IPlayerTickStateSource
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
    // Network State - Input
    // =========================================================

    [Networked]
    private NetworkButtons PreviousButtons
    {
        get;
        set;
    }


    // =========================================================
    // Network State - Cooldown
    // =========================================================

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
    // Network State - Charge
    // =========================================================

    [Networked]
    private byte Skill1Charges
    {
        get;
        set;
    }

    [Networked]
    private byte Skill2Charges
    {
        get;
        set;
    }

    [Networked]
    private TickTimer Skill1RechargeTimer
    {
        get;
        set;
    }

    [Networked]
    private TickTimer Skill2RechargeTimer
    {
        get;
        set;
    }


    // =========================================================
    // Network State - Use Phase
    // =========================================================

    [Networked]
    private SkillUsePhase Skill1Phase
    {
        get;
        set;
    }

    [Networked]
    private SkillUsePhase Skill2Phase
    {
        get;
        set;
    }

    [Networked]
    private TickTimer Skill1PhaseTimer
    {
        get;
        set;
    }

    [Networked]
    private TickTimer Skill2PhaseTimer
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

        ResetSlotRuntime(
            SkillSlot.Skill1,
            _skill1);

        ResetSlotRuntime(
            SkillSlot.Skill2,
            _skill2);
    }


    PlayerTickStage IPlayerTickModule.Stage =>
        PlayerTickStage.SkillIntent;


    void IPlayerTickModule.Simulate(
        in PlayerTick tick)
    {
        TickLateAction(
            tick.State.HasHealth &&
            tick.State.IsAlive,
            false);
    }

    [Networked]
    private Vector2 Skill1AimDirection
    {
        get;
        set;
    }

    [Networked]
    private Vector2 Skill2AimDirection
    {
        get;
        set;
    }


    void IPlayerTickStateSource.CaptureTickState(
        PlayerTickState state)
    {
        state.HasSkill = true;
        state.IsSkillActionLocked =
            IsActionLocked(
                SkillSlot.Skill1,
                _skill1) ||
            IsActionLocked(
                SkillSlot.Skill2,
                _skill2);
    }


    public override void FixedUpdateNetwork()
    {
        if (IsTickControlled)
            return;

        TickLateAction();
    }


    public override void Render()
    {
        _skill1?.Render();
        _skill2?.Render();
    }


    internal bool TryGetCurrentInput(
        out PlayerInputData input)
    {
        return GetInput(
            out input);
    }


    internal void TickLateAction()
    {
        TickLateAction(
            _healthState != null &&
            _healthState.IsAlive,
            true);
    }


    private void TickLateAction(
        bool isAlive,
        bool requireContext)
    {
        if (requireContext &&
            !IsContextReady)
            return;


        // 공통 Runtime State를 먼저 갱신합니다.
        UpdateSlotRuntime(
            SkillSlot.Skill1,
            _skill1);

        UpdateSlotRuntime(
            SkillSlot.Skill2,
            _skill2);


        // 갱신된 Phase를 기준으로
        // 실제 Skill 행동을 수행합니다.
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

        if (!isAlive)
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
        // Input
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

        if (GetUsePhase(
                slot) !=
            SkillUsePhase.None)
        {
            return false;
        }

        if (IsOnCooldown(
                slot))
        {
            return false;
        }

        if (!HasAvailableCharge(
                slot,
                skill))
        {
            return false;
        }

        if (!skill.CanUse(
                in useContext))
        {
            return false;
        }


        ConsumeCharge(
            slot,
            skill);

        StartCooldown(
            slot,
            skill.Data.Cooldown);

        BeginUsePhase(
            slot,
            skill);


        skill.Activate(
            in useContext);

        return true;
    }


    // =========================================================
    // Runtime Update
    // =========================================================

    private void UpdateSlotRuntime(
        SkillSlot slot,
        Skill skill)
    {
        if (skill == null)
            return;

        UpdateRecharge(
            slot,
            skill);

        UpdateUsePhase(
            slot,
            skill);
    }


    // =========================================================
    // Cooldown
    // =========================================================

    public bool IsOnCooldown(
        SkillSlot slot)
    {
        return !GetCooldownTimer(
                slot)
            .ExpiredOrNotRunning(
                Runner);
    }


    public float GetCooldownRemaining(
        SkillSlot slot)
    {
        return GetCooldownTimer(
                   slot)
               .RemainingTime(
                   Runner)
               ?? 0f;
    }


    private void StartCooldown(
        SkillSlot slot,
        float duration)
    {
        SetCooldownTimer(
            slot,
            CreateTimer(
                duration));
    }


    // =========================================================
    // Charge
    // =========================================================

    private bool HasAvailableCharge(
        SkillSlot slot,
        Skill skill)
    {
        if (skill is not
            IChargeSkill)
        {
            return true;
        }

        return GetCurrentCharges(
                   slot) >
               0;
    }


    private void ConsumeCharge(
        SkillSlot slot,
        Skill skill)
    {
        if (skill is not
            IChargeSkill chargeSkill)
        {
            return;
        }

        int current =
            GetCurrentCharges(
                slot);

        if (current <= 0)
            return;

        SetCurrentCharges(
            slot,
            current - 1);


        TickTimer rechargeTimer =
            GetRechargeTimer(
                slot);

        if (rechargeTimer
            .ExpiredOrNotRunning(
                Runner))
        {
            SetRechargeTimer(
                slot,
                CreateTimer(
                    chargeSkill
                        .RechargeDuration));
        }
    }


    private void UpdateRecharge(
        SkillSlot slot,
        Skill skill)
    {
        if (skill is not
            IChargeSkill chargeSkill)
        {
            return;
        }

        int maximum =
            Mathf.Clamp(
                chargeSkill.MaxCharges,
                1,
                byte.MaxValue);

        int current =
            GetCurrentCharges(
                slot);

        if (current >= maximum)
        {
            SetCurrentCharges(
                slot,
                maximum);

            SetRechargeTimer(
                slot,
                TickTimer.None);

            return;
        }


        TickTimer timer =
            GetRechargeTimer(
                slot);

        if (!timer.Expired(
                Runner))
        {
            return;
        }


        current++;

        SetCurrentCharges(
            slot,
            current);


        if (current < maximum)
        {
            SetRechargeTimer(
                slot,
                CreateTimer(
                    chargeSkill
                        .RechargeDuration));
        }
        else
        {
            SetRechargeTimer(
                slot,
                TickTimer.None);
        }
    }


    public int GetCurrentCharges(
        SkillSlot slot)
    {
        return slot switch
        {
            SkillSlot.Skill1 =>
                Skill1Charges,

            SkillSlot.Skill2 =>
                Skill2Charges,

            _ =>
                0
        };
    }


    public int GetMaxCharges(
        SkillSlot slot)
    {
        Skill skill =
            GetSkill(
                slot);

        return skill is
            IChargeSkill chargeSkill
                ? Mathf.Max(
                    1,
                    chargeSkill.MaxCharges)
                : 1;
    }


    public float GetRechargeRemaining(
        SkillSlot slot)
    {
        return GetRechargeTimer(
                   slot)
               .RemainingTime(
                   Runner)
               ?? 0f;
    }


    public float GetRechargeNormalized(
        SkillSlot slot)
    {
        Skill skill =
            GetSkill(
                slot);

        if (skill is not
            IChargeSkill chargeSkill)
        {
            return 0f;
        }

        float duration =
            chargeSkill.RechargeDuration;

        if (duration <= 0f)
            return 1f;

        float remaining =
            GetRechargeRemaining(
                slot);

        return Mathf.Clamp01(
            1f -
            remaining /
            duration);
    }


    // =========================================================
    // Use Phase
    // =========================================================

    private void BeginUsePhase(
        SkillSlot slot,
        Skill skill)
    {
        if (skill is
                ICastTimeSkill castSkill &&
            castSkill.CastDuration > 0f)
        {
            SetUsePhase(
                slot,
                SkillUsePhase.Cast);

            SetPhaseTimer(
                slot,
                CreateTimer(
                    castSkill.CastDuration));

            return;
        }

        BeginActiveOrRecovery(
            slot,
            skill);
    }


    private void UpdateUsePhase(
        SkillSlot slot,
        Skill skill)
    {
        SkillUsePhase phase =
            GetUsePhase(
                slot);

        if (phase ==
            SkillUsePhase.None)
        {
            return;
        }

        TickTimer timer =
            GetPhaseTimer(
                slot);

        if (!timer.Expired(
                Runner))
        {
            return;
        }


        switch (phase)
        {
            case SkillUsePhase.Cast:

                BeginActiveOrRecovery(
                    slot,
                    skill);

                break;


            case SkillUsePhase.Active:

                BeginRecoveryOrFinish(
                    slot,
                    skill);

                break;


            case SkillUsePhase.Recovery:

                FinishUse(
                    slot,
                    skill);

                break;
        }
    }


    private void BeginActiveOrRecovery(
        SkillSlot slot,
        Skill skill)
    {
        if (skill is
                IDurationSkill durationSkill &&
            durationSkill.Duration > 0f)
        {
            SetUsePhase(
                slot,
                SkillUsePhase.Active);

            SetPhaseTimer(
                slot,
                CreateTimer(
                    durationSkill.Duration));

            return;
        }

        BeginRecoveryOrFinish(
            slot,
            skill);
    }


    private void BeginRecoveryOrFinish(
        SkillSlot slot,
        Skill skill)
    {
        if (skill is
                IRecoverySkill recoverySkill &&
            recoverySkill.RecoveryDuration > 0f)
        {
            SetUsePhase(
                slot,
                SkillUsePhase.Recovery);

            SetPhaseTimer(
                slot,
                CreateTimer(
                    recoverySkill
                        .RecoveryDuration));

            return;
        }

        FinishUse(
            slot,
            skill);
    }


    private void FinishUse(
        SkillSlot slot,
        Skill skill)
    {
        SetUsePhase(
            slot,
            SkillUsePhase.None);

        SetPhaseTimer(
            slot,
            TickTimer.None);

        skill?
            .OnUseEnded();
    }


    /// <summary>
    /// Skill이 Active 상태를 조기에 종료할 때 사용합니다.
    /// 충돌로 Dash를 멈추는 경우 등이 해당됩니다.
    /// </summary>
    internal void EndActiveEarly(
        SkillSlot slot)
    {
        if (GetUsePhase(
                slot) !=
            SkillUsePhase.Active)
        {
            return;
        }

        Skill skill =
            GetSkill(
                slot);

        BeginRecoveryOrFinish(
            slot,
            skill);
    }


    public SkillUsePhase GetUsePhase(
        SkillSlot slot)
    {
        return slot switch
        {
            SkillSlot.Skill1 =>
                Skill1Phase,

            SkillSlot.Skill2 =>
                Skill2Phase,

            _ =>
                SkillUsePhase.None
        };
    }


    public float GetPhaseRemaining(
        SkillSlot slot)
    {
        return GetPhaseTimer(
                   slot)
               .RemainingTime(
                   Runner)
               ?? 0f;
    }


    public bool IsUsing(
        SkillSlot slot)
    {
        return GetUsePhase(
                   slot) !=
               SkillUsePhase.None;
    }


    internal void SetSkillAimDirection(
        SkillSlot slot,
        Vector2 direction)
    {
        if (!HasStateAuthority)
            return;

        direction =
            direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector2.zero;

        switch (slot)
        {
            case SkillSlot.Skill1:
                Skill1AimDirection = direction;
                break;

            case SkillSlot.Skill2:
                Skill2AimDirection = direction;
                break;
        }
    }


    internal Vector2 GetSkillAimDirection(
        SkillSlot slot)
    {
        return slot switch
        {
            SkillSlot.Skill1 => Skill1AimDirection,
            SkillSlot.Skill2 => Skill2AimDirection,
            _ => Vector2.zero
        };
    }


    public PlayerStatModifiers GetActiveStatModifiers()
    {
        PlayerStatModifiers result =
            PlayerStatModifiers.Identity;

        CombineActiveStatModifiers(
            SkillSlot.Skill1,
            _skill1,
            ref result);

        CombineActiveStatModifiers(
            SkillSlot.Skill2,
            _skill2,
            ref result);

        return result;
    }


    private void CombineActiveStatModifiers(
        SkillSlot slot,
        Skill skill,
        ref PlayerStatModifiers result)
    {
        if (GetUsePhase(slot) !=
                SkillUsePhase.Active ||
            skill is not IPlayerStatModifierSkill modifierSkill)
        {
            return;
        }

        PlayerStatModifiers modifiers =
            modifierSkill.StatModifiers;

        result =
            result.Combine(in modifiers);
    }


    private bool IsActionLocked(
        SkillSlot slot,
        Skill skill)
    {
        SkillUsePhase phase =
            GetUsePhase(slot);

        return skill is IActionLockSkill actionLockSkill &&
               phase != SkillUsePhase.None &&
               actionLockSkill.IsActionLocked(phase);
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

        if (HasStateAuthority)
        {
            ResetSlotRuntime(
                slot,
                newSkill);
        }

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

        if (HasStateAuthority)
        {
            ResetSlotRuntime(
                slot,
                null);
        }
    }


    // =========================================================
    // Cancel
    // =========================================================

    public void Cancel(
        SkillSlot slot)
    {
        Skill skill =
            GetSkill(
                slot);

        skill?
            .Cancel();

        SetUsePhase(
            slot,
            SkillUsePhase.None);

        SetPhaseTimer(
            slot,
            TickTimer.None);

        SetSkillAimDirection(
            slot,
            Vector2.zero);
    }


    public void CancelAll()
    {
        Cancel(
            SkillSlot.Skill1);

        Cancel(
            SkillSlot.Skill2);
    }


    // =========================================================
    // Runtime Reset
    // =========================================================

    private void ResetSlotRuntime(
        SkillSlot slot,
        Skill skill)
    {
        SetCooldownTimer(
            slot,
            TickTimer.None);

        SetRechargeTimer(
            slot,
            TickTimer.None);

        SetUsePhase(
            slot,
            SkillUsePhase.None);

        SetPhaseTimer(
            slot,
            TickTimer.None);


        SetSkillAimDirection(
            slot,
            Vector2.zero);


        int charges =
            skill is
                IChargeSkill chargeSkill
                ? Mathf.Clamp(
                    chargeSkill.MaxCharges,
                    1,
                    byte.MaxValue)
                : 0;

        SetCurrentCharges(
            slot,
            charges);
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
        }
    }


    // =========================================================
    // Internal State Access
    // =========================================================

    private TickTimer GetCooldownTimer(
        SkillSlot slot)
    {
        return slot switch
        {
            SkillSlot.Skill1 =>
                Skill1CooldownTimer,

            SkillSlot.Skill2 =>
                Skill2CooldownTimer,

            _ =>
                TickTimer.None
        };
    }


    private void SetCooldownTimer(
        SkillSlot slot,
        TickTimer timer)
    {
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


    private TickTimer GetRechargeTimer(
        SkillSlot slot)
    {
        return slot switch
        {
            SkillSlot.Skill1 =>
                Skill1RechargeTimer,

            SkillSlot.Skill2 =>
                Skill2RechargeTimer,

            _ =>
                TickTimer.None
        };
    }


    private void SetRechargeTimer(
        SkillSlot slot,
        TickTimer timer)
    {
        switch (slot)
        {
            case SkillSlot.Skill1:
                Skill1RechargeTimer =
                    timer;
                break;

            case SkillSlot.Skill2:
                Skill2RechargeTimer =
                    timer;
                break;
        }
    }


    private void SetCurrentCharges(
        SkillSlot slot,
        int charges)
    {
        byte value =
            (byte)Mathf.Clamp(
                charges,
                0,
                byte.MaxValue);

        switch (slot)
        {
            case SkillSlot.Skill1:
                Skill1Charges =
                    value;
                break;

            case SkillSlot.Skill2:
                Skill2Charges =
                    value;
                break;
        }
    }


    private TickTimer GetPhaseTimer(
        SkillSlot slot)
    {
        return slot switch
        {
            SkillSlot.Skill1 =>
                Skill1PhaseTimer,

            SkillSlot.Skill2 =>
                Skill2PhaseTimer,

            _ =>
                TickTimer.None
        };
    }


    private void SetPhaseTimer(
        SkillSlot slot,
        TickTimer timer)
    {
        switch (slot)
        {
            case SkillSlot.Skill1:
                Skill1PhaseTimer =
                    timer;
                break;

            case SkillSlot.Skill2:
                Skill2PhaseTimer =
                    timer;
                break;
        }
    }


    private void SetUsePhase(
        SkillSlot slot,
        SkillUsePhase phase)
    {
        switch (slot)
        {
            case SkillSlot.Skill1:
                Skill1Phase =
                    phase;
                break;

            case SkillSlot.Skill2:
                Skill2Phase =
                    phase;
                break;
        }
    }


    // =========================================================
    // Utility
    // =========================================================

    private TickTimer CreateTimer(
        float duration)
    {
        return duration > 0f
            ? TickTimer.CreateFromSeconds(
                Runner,
                duration)
            : TickTimer.None;
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
