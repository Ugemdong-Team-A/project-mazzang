using Fusion;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.U2D.Animation;

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
    PlayerTickModule,
    IPlayerTickStateSource,
    IPlayerTickCommandSink,
    IDamageDealtReceiver
{
    private const int SkillSlotCount = 2;


    [Header("Default Skills")]
    [FormerlySerializedAs("skill1")]
    [SerializeField]
    private SkillData mainSkill;

    [FormerlySerializedAs("skill2")]
    [SerializeField]
    private SkillData ultimateSkill;


    private readonly Skill[] _skills =
        new Skill[SkillSlotCount];


    [Networked]
    public byte SkillAnimationSequence
    {
        get;
        private set;
    }

    [Networked]
    public SkillSlot LastSkillAnimationSlot
    {
        get;
        private set;
    }

    [Networked]
    public SkillAnimationPhase LastSkillAnimationPhase
    {
        get;
        private set;
    }


    // =========================================================
    // Network State - Input
    // =========================================================

    [Networked]
    private NetworkButtons PreviousButtons
    {
        get;
        set;
    }


    [Networked]
    private TickTimer SkillControlLockTimer
    {
        get;
        set;
    }


    // =========================================================
    // Network State - Slots
    // =========================================================

    [Networked, Capacity(SkillSlotCount)]
    private NetworkArray<SkillSlotRuntimeState>
        SlotStates =>
            default;


    // =========================================================
    // Public State
    // =========================================================

    public Skill Skill1 =>
        GetSkill(
            SkillSlot.Skill1);

    public Skill Skill2 =>
        GetSkill(
            SkillSlot.Skill2);


    public bool IsSkillControlLocked =>
        !SkillControlLockTimer
            .ExpiredOrNotRunning(
                Runner);


    // =========================================================
    // Fusion
    // =========================================================

    public override void Spawned()
    {
        // 런타임 Skill은 예측과 표현을 위해 모든 peer가 생성합니다.
        // Networked 슬롯 초기화는 Equip 내부에서 State Authority만 수행합니다.
        Equip(
            SkillSlot.Skill1,
            mainSkill);

        Equip(
            SkillSlot.Skill2,
            ultimateSkill);

        if (!HasStateAuthority)
            return;

        PreviousButtons =
            default;

        SkillControlLockTimer =
            TickTimer.None;
    }


    public override PlayerTickStage Stage =>
        PlayerTickStage.SkillIntent;

    public PlayerTickState TickState { get; private set; }

    public PlayerTickCommands TickCommands { get; private set; }


    public override void Simulate(
        in PlayerTick tick)
    {
        if (TickState == null) TickState = tick.State;

        if(TickCommands == null) TickCommands = tick.Commands;

        TickLateAction(
            tick.State.HasHealth &&
            tick.State.IsAlive);
    }

    void IPlayerTickStateSource.CaptureTickState(
        PlayerTickState state)
    {
        state.HasSkill = true;

        state.SkillAnimationSequence =
            SkillAnimationSequence;

        state.SkillAnimationSlot =
            LastSkillAnimationSlot;

        state.SkillAnimationPhase =
            LastSkillAnimationPhase;

        state.SkillAnimation =
            GetSkillData(
                LastSkillAnimationSlot)?
                .Animation;

        state.ActiveStatModifiers =
            GetActiveStatModifiers();

        state.ActiveAppearanceLibraryAsset =
            GetActiveAppearanceLibraryAsset();

        state.IsSkillControlLocked =
            IsSkillControlLocked;

        state.IsSkillActionLocked =
            IsActionLocked(
                SkillSlot.Skill1,
                Skill1) ||
            IsActionLocked(
                SkillSlot.Skill2,
                Skill2);
    }


    bool IPlayerTickCommandSink.ResolveTickCommands(
        PlayerTickCommands commands,
        PlayerTickState state)
    {
        if (!commands.TryConsumeSkillControlLock(
                out float duration))
        {
            return false;
        }

        LockSkillControl(
            duration);

        return true;
    }


    public override void Present(in PlayerTickState tickState)
    {
        foreach (Skill skill in _skills)
        {
            skill?.Render();
        }
    }


    internal bool TryGetCurrentInput(
        out PlayerInputData input)
    {
        return GetInput(
            out input);
    }


    private void TickLateAction(bool isAlive)
    {

        // 공통 Runtime State를 먼저 갱신합니다.
        UpdateSlotRuntime(
            SkillSlot.Skill1,
            Skill1,
            isAlive);

        UpdateSlotRuntime(
            SkillSlot.Skill2,
            Skill2,
            isAlive);


        // 갱신된 Phase를 기준으로
        // 실제 Skill 행동을 수행합니다.
        foreach (Skill skill in _skills)
        {
            skill?.FixedUpdateNetwork();
        }


        bool hasInput =
            GetInput(
                out PlayerInputData input);


        // ==========================================
        // Dead
        // ==========================================

        if (!isAlive)
        {
            SkillControlLockTimer =
                TickTimer.None;

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
        if (IsSkillControlLocked)
            return false;

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

        if (!HasRequiredMeter(
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

        ConsumeMeter(
            slot,
            skill);

        StartCooldown(
            slot,
            skill.Data.Cooldown);

        BeginUsePhase(
            slot,
            skill);

        PublishSkillAnimation(
            slot,
            GetUsePhase(slot) ==
                SkillUsePhase.Cast
                ? SkillAnimationPhase.Cast
                : SkillAnimationPhase.Release);


        skill.Activate(
            in useContext);

        return true;
    }


    private void PublishSkillAnimation(
        SkillSlot slot,
        SkillAnimationPhase phase)
    {
        SkillAnimationData animation =
            GetSkillData(slot)?.Animation;

        if (animation == null ||
            animation.GetClip(phase) == null)
        {
            return;
        }

        LastSkillAnimationSlot = slot;
        LastSkillAnimationPhase = phase;
        SkillAnimationSequence++;
    }


    // =========================================================
    // Runtime Update
    // =========================================================

    private void UpdateSlotRuntime(
        SkillSlot slot,
        Skill skill,
        bool isAlive)
    {
        if (skill == null)
            return;

        UpdateMeter(
            slot,
            skill,
            isAlive);

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
        return GetSlotState(
                slot)
            .Charges;
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
    // Meter
    // =========================================================

    private bool HasRequiredMeter(
        SkillSlot slot,
        Skill skill)
    {
        if (skill is not
            IMeterSkill meterSkill)
        {
            return true;
        }

        float cost =
            Mathf.Max(
                0f,
                meterSkill.MeterCost);

        return GetCurrentMeter(
                   slot) >=
               cost;
    }


    private void ConsumeMeter(
        SkillSlot slot,
        Skill skill)
    {
        if (skill is not
            IMeterSkill meterSkill)
        {
            return;
        }

        SetCurrentMeter(
            slot,
            GetCurrentMeter(slot) -
            Mathf.Max(
                0f,
                meterSkill.MeterCost));
    }


    private void UpdateMeter(
        SkillSlot slot,
        Skill skill,
        bool isAlive)
    {
        if (!isAlive ||
            skill is not
                IMeterSkill meterSkill)
        {
            return;
        }

        float gainPerSecond =
            Mathf.Max(
                0f,
                meterSkill
                    .PassiveGainPerSecond);

        if (gainPerSecond <= 0f ||
            GetCurrentMeter(slot) >=
                GetMaxMeter(slot))
        {
            return;
        }

        SetCurrentMeter(
            slot,
            GetCurrentMeter(slot) +
            gainPerSecond *
            Runner.DeltaTime);
    }


    public float GetCurrentMeter(
        SkillSlot slot)
    {
        return GetSlotState(
                slot)
            .Meter;
    }


    public float GetMaxMeter(
        SkillSlot slot)
    {
        return GetSkill(slot) is
            IMeterSkill meterSkill
                ? Mathf.Max(
                    0f,
                    meterSkill.MaxMeter)
                : 0f;
    }


    public float GetMeterNormalized(
        SkillSlot slot)
    {
        float maximum =
            GetMaxMeter(
                slot);

        return maximum > 0f
            ? Mathf.Clamp01(
                GetCurrentMeter(slot) /
                maximum)
            : 0f;
    }


    public void GrantMeter(
        SkillSlot slot,
        float amount)
    {
        if (!HasStateAuthority ||
            amount <= 0f)
        {
            return;
        }

        SetCurrentMeter(
            slot,
            GetCurrentMeter(slot) +
            amount);
    }


    void IDamageDealtReceiver.ReceiveDamageDealt(
        int appliedDamage)
    {
        if (!HasStateAuthority ||
            appliedDamage <= 0)
        {
            return;
        }

        for (int index = 0;
             index < _skills.Length;
             index++)
        {
            if (_skills[index] is not
                IMeterSkill meterSkill)
            {
                continue;
            }

            float gain =
                appliedDamage *
                Mathf.Max(
                    0f,
                    meterSkill
                        .DamageGainPerDamage);

            GrantMeter(
                (SkillSlot)index,
                gain);
        }
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

                PublishSkillAnimation(
                    slot,
                    SkillAnimationPhase.Release);

                break;


            case SkillUsePhase.Active:

                BeginRecoveryOrFinish(
                    slot,
                    skill);

                if (GetUsePhase(slot) ==
                    SkillUsePhase.Recovery)
                {
                    PublishSkillAnimation(
                        slot,
                        SkillAnimationPhase.Recovery);
                }

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
        return GetSlotState(
                slot)
            .Phase;
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

        SkillSlotRuntimeState state =
            GetSlotState(
                slot);

        state.AimDirection =
            direction;

        SetSlotState(
            slot,
            state);
    }


    internal Vector2 GetSkillAimDirection(
        SkillSlot slot)
    {
        return GetSlotState(
                slot)
            .AimDirection;
    }


    public PlayerStatModifiers GetActiveStatModifiers()
    {
        PlayerStatModifiers result =
            PlayerStatModifiers.Identity;

        CombineActiveStatModifiers(
            SkillSlot.Skill1,
            Skill1,
            ref result);

        CombineActiveStatModifiers(
            SkillSlot.Skill2,
            Skill2,
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


    public SpriteLibraryAsset
        GetActiveAppearanceLibraryAsset()
    {
        // 궁극기 슬롯을 우선합니다. null 외형은 다른 활성 스킬을 막지 않습니다.
        SpriteLibraryAsset asset =
            GetActiveAppearanceLibraryAsset(
                SkillSlot.Skill2,
                Skill2);

        return asset != null
            ? asset
            : GetActiveAppearanceLibraryAsset(
                SkillSlot.Skill1,
                Skill1);
    }


    private SpriteLibraryAsset
        GetActiveAppearanceLibraryAsset(
            SkillSlot slot,
            Skill skill)
    {
        if (GetUsePhase(slot) !=
                SkillUsePhase.Active ||
            skill is not IAppearanceModifierSkill modifierSkill)
        {
            return null;
        }

        return modifierSkill.AppearanceLibraryAsset;
    }


    private void LockSkillControl(
        float duration)
    {
        if (duration <= 0f)
            return;

        float remaining =
            SkillControlLockTimer
                .RemainingTime(Runner) ??
            0f;

        if (duration <= remaining)
            return;

        SkillControlLockTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                duration);
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
        return TryGetSlotIndex(
            slot,
            out int index)
                ? _skills[index]
                : null;
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
        int charges =
            skill is
                IChargeSkill chargeSkill
                ? Mathf.Clamp(
                    chargeSkill.MaxCharges,
                    1,
                    byte.MaxValue)
                : 0;

        SkillSlotRuntimeState state =
            new()
            {
                Phase =
                    SkillUsePhase.None,
                Charges =
                    (byte)charges,
                Meter =
                    0f,
                AimDirection =
                    Vector2.zero,
                CooldownTimer =
                    TickTimer.None,
                PhaseTimer =
                    TickTimer.None,
                RechargeTimer =
                    TickTimer.None
            };

        SetSlotState(
            slot,
            state);
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
            this);

        return skill;
    }


    private void SetSkill(
        SkillSlot slot,
        Skill skill)
    {
        if (!TryGetSlotIndex(
                slot,
                out int index))
        {
            return;
        }

        _skills[index] =
            skill;
    }


    // =========================================================
    // Internal State Access
    // =========================================================

    private SkillSlotRuntimeState GetSlotState(
        SkillSlot slot)
    {
        return TryGetSlotIndex(
            slot,
            out int index)
                ? SlotStates[index]
                : default;
    }


    private void SetSlotState(
        SkillSlot slot,
        SkillSlotRuntimeState state)
    {
        if (!TryGetSlotIndex(
                slot,
                out int index))
        {
            return;
        }

        SlotStates.Set(
            index,
            state);
    }


    private TickTimer GetCooldownTimer(
        SkillSlot slot)
    {
        return GetSlotState(
                slot)
            .CooldownTimer;
    }


    private void SetCooldownTimer(
        SkillSlot slot,
        TickTimer timer)
    {
        if (GetSkill(slot) is IMeterSkill)
            return;

        SkillSlotRuntimeState state =
            GetSlotState(
                slot);

        state.CooldownTimer =
            timer;

        SetSlotState(
            slot,
            state);
    }


    private TickTimer GetRechargeTimer(
        SkillSlot slot)
    {
        return GetSlotState(
                slot)
            .RechargeTimer;
    }


    private void SetRechargeTimer(
        SkillSlot slot,
        TickTimer timer)
    {
        if (GetSkill(slot) is IMeterSkill)
            return;

        SkillSlotRuntimeState state =
            GetSlotState(
                slot);

        state.RechargeTimer =
            timer;

        SetSlotState(
            slot,
            state);
    }


    private void SetCurrentCharges(
        SkillSlot slot,
        int charges)
    {
        if (GetSkill(slot) is IMeterSkill)
            return;

        byte value =
            (byte)Mathf.Clamp(
                charges,
                0,
                byte.MaxValue);

        SkillSlotRuntimeState state =
            GetSlotState(
                slot);

        state.Charges =
            value;

        SetSlotState(
            slot,
            state);
    }


    private void SetCurrentMeter(
        SkillSlot slot,
        float meter)
    {
        SkillSlotRuntimeState state =
            GetSlotState(
                slot);

        state.Meter =
            Mathf.Clamp(
                meter,
                0f,
                GetMaxMeter(slot));

        SetSlotState(
            slot,
            state);
    }


    private TickTimer GetPhaseTimer(
        SkillSlot slot)
    {
        return GetSlotState(
                slot)
            .PhaseTimer;
    }


    private void SetPhaseTimer(
        SkillSlot slot,
        TickTimer timer)
    {
        SkillSlotRuntimeState state =
            GetSlotState(
                slot);

        state.PhaseTimer =
            timer;

        SetSlotState(
            slot,
            state);
    }


    private void SetUsePhase(
        SkillSlot slot,
        SkillUsePhase phase)
    {
        SkillSlotRuntimeState state =
            GetSlotState(
                slot);

        state.Phase =
            phase;

        SetSlotState(
            slot,
            state);
    }


    // =========================================================
    // Utility
    // =========================================================

    private static bool TryGetSlotIndex(
        SkillSlot slot,
        out int index)
    {
        index =
            (int)slot;

        return index >= 0 &&
               index < SkillSlotCount;
    }


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
        for (int i = 0;
             i < _skills.Length;
             i++)
        {
            _skills[i]?
                .Dispose();

            _skills[i] =
                null;
        }
    }
}
