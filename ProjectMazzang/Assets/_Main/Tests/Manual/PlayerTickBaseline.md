# Player Tick Baseline

이 문서는 플레이어 Tick 구조 리팩터링 전에 보존해야 하는 현재 동작을 기록한다.
자동 EditMode 테스트로 검증할 수 없는 Fusion 예측 및 권위 실행은 아래 절차로 확인한다.

## 실행 구성

- Host 1명과 Client 1명으로 동일한 Gameplay 씬을 실행한다.
- 두 플레이어 모두 같은 캐릭터와 공격 데이터를 사용한다.
- Host와 Client 양쪽에서 각 항목을 공격자와 피격자로 한 번씩 수행한다.

## Tick 순서 기준

현재 PlayerController가 보장하는 Tick 단계 순서는 다음과 같다.

1. PlayerHealth (Begin, 0)
2. PlayerSkillController (SkillIntent, 0)
3. PlayerParry (DefenseIntent, 0)
4. PlayerWeaponController (PrepareAction, 0)
5. PlayerCombat (Action, 0)
6. PlayerMovement (Motion, 0)
7. PlayerVisual (Motion, 100)
8. PlayerAim (Aim, 0)
9. PlayerAnimation (Finalize, 0)
10. PlayerStatusUI (Finalize, 100)

같은 Stage에는 여러 모듈이 들어갈 수 있다. 이때 Order가 낮은 모듈이 먼저 실행되며,
같은 플레이어 안에서 Stage와 Order 조합이 겹치면 파이프라인을 시작하지 않는다.

## 검증 항목

### 공격과 이동

- 공격이 시작된 Tick부터 MovementMode가 Locked인 공격은 이동 입력을 적용하지 않는다.
- Startup, Active, Recovery가 설정된 순서로 한 번씩 진행된다.
- Recovery 종료 뒤 공격 상태가 None으로 복귀한다.
- Attack Control Lock 동안 새 일반 공격이 시작되지 않는다.
- 진행 중인 공격 취소는 Control Lock이 아니라 CancelAttack 명령이 담당한다.

### 피격과 control lock

- 유효한 피해는 Health를 정확히 한 번 감소시킨다.
- 피해가 적용된 Tick에 진행 중인 공격이 취소된다.
- Knockback이 0이 아니면 동일한 피해 정보의 속도가 Movement에 적용된다.
- 공격 취소와 Knockback 명령이 다음 Tick까지 남지 않고 요청된 시뮬레이션 호출에서 처리된다.
- HitStun CC 지속 시간 동안 이동 및 일반 공격 입력이 적용되지 않는다.
- `stopMovementOnApply`가 켜진 공격은 CC 지연 여부와 관계없이 적중 즉시 현재 이동 속도를 0으로 만든다.
- `activationDelay` 후에 CC가 발동하고, 같은 틱의 여러 지연 CC가 서로 덮어쓰지 않는다.
- Timer 만료 뒤 다음 유효 Tick부터 이동과 일반 공격이 가능하다.
- 같은 종류의 더 짧은 후속 잠금이 기존의 긴 잠금 시간을 줄이지 않는다.

### Dash와 종류별 control lock

- Dash 시작 시 Movement, Attack, Skill Control Lock이 각각 적용된다.
- Dash는 자기 자신이 요청한 Skill Control Lock으로 취소되지 않는다.
- Skill Control Lock 동안 새 스킬만 시작되지 않고 이미 실행 중인 스킬은 계속 진행된다.
- Skill1과 Skill2를 같은 Tick에 눌렀을 때 먼저 실행된 스킬이 Skill Control Lock을 요청하면 두 번째 스킬은 시작되지 않는다.

### 사망과 리스폰

- Health가 0이 된 Tick에 IsDead가 true가 되고 공격이 취소된다.
- 사망 중 Movement 입력이 적용되지 않는다.
- 리스폰 시 Movement, Attack, Skill Control Lock이 모두 해제되어 있다.
- 리스폰 무적 시간 동안 피해가 적용되지 않는다.

### Aim과 Animation

- Movement에서 확정된 Facing을 Aim이 같은 Tick에 읽는다.
- Aim에서 확정된 방향과 각도를 Animation이 같은 Tick에 읽는다.
- Aim override가 끝난 뒤 일반 입력 Aim으로 복귀한다.
- 공격의 Aim override와 Weapon 사용 요청이 요청된 Tick 안에서 처리된다.
- `ProceduralAim` 공격은 기본 상체 포즈에서 CCD 조준을 적용한다.
- 공격하지 않고 달리거나 대기할 때는 CCD 조준 중에도 클립의 가슴과 팔 움직임이 유지된다.
- `AnimationOnly`는 상체 CCD를 끄고 공격 클립의 자세를 그대로 사용한다.
- `AnimationWithBodyAim`은 공격 클립의 상체 상대 자세를 유지한 채 기준 척추에
  필요한 회전만 더해 최종 조준 방향을 맞춘다.
- 무기가 없는 평상시에도 양손 Limb Solver가 애니메이션 Target을 사용해 IK 기반 이동·공격
  클립의 팔 동작을 재생한다.
- 무기를 장착하면 각 손은 무기의 Grip을 우선하며, Grip이 없는 손은 항상 애니메이션 Target을
  유지한다. 무기를 해제하면 두 손 모두 애니메이션 Target으로 즉시 복원된다.
- 바라보는 방향으로 이동하면 전진 Run, 반대 방향으로 이동하면 Backrun이 재생되며 방향 반전 후에도
  같은 기준이 유지된다.
- 잽의 Active 시작부터 Recovery 종료까지 공격을 한 번 이상 누르면 Counter가 이어지고,
  중복 입력 허용을 끈 데이터는 정확히 한 번 눌렀을 때만 이어진다.
- Counter 이후에는 후속 공격 참조가 잘못 연결되어 있어도 콤보가 다시 이어지지 않는다.
- Counter가 시작되면 시작 당시 Aim 방향으로 짧게 전진하고, 대시 중 입력으로 방향이 바뀌지 않는다.
- Counter 대시가 끝나거나 공격이 취소되면 대시 속도가 남지 않으며 이후 이동 입력이 정상 복구된다.
- 상체 조준과 공격 애니메이션으로 `WeaponSocket`이 움직여도 근접 판정, 무기 발사, 무기 드롭의
  기준점은 같은 Tick의 `AimOrigin`에서 흔들리지 않는다.
- 캐릭터에서 `Standard2DRigIKSetup`을 제거하거나 별도 에디터 작업용 오브젝트로 옮겨도,
  런타임에 명시적으로 연결한 상체 CCD와 손 Solver가 정상 동작한다.
- 재시뮬레이션 횟수와 관계없이 Aim 보간, 피격 색상, 무적 깜빡임 속도가 일정하다.
- 처음 표시할 때 이미 존재하던 Jump, Attack, Skill, Death Sequence를 새 이벤트로 재생하지 않는다.
- Health, MaxHealth, Lives와 생존 여부가 상태 UI 및 캐릭터 표시와 일치한다.

### 예측과 권위 상태

- Client 입력으로 이동, 점프, 공격할 때 지속적인 위치 correction이 발생하지 않는다.
- 공격과 피격이 같은 Tick 근처에서 발생해도 Host와 Client의 AttackState가 일치한다.
- Knockback 종료 Tick에 Host와 Client가 동시에 입력 제어를 회복한다.
- 반복 입력 또는 resimulation 뒤 Damage, DeathSequence, AttackSequence가 중복 증가하지 않는다.
- Despawn 및 Respawn 뒤 이전 Tick의 control lock이나 공격 상태가 남지 않는다.

### Skill Meter

- 기사 `mainSkill`에서 Dash가 시작되고 `ultimateSkill`에서 `UltimateAwakeningSkill`이 시작되는지 확인한다.
- Meter UI의 충전 레일·내부 면·퍼센트가 같은 값으로 갱신되고, 정수 증가 시 한 번만 반응하는지 확인한다.
- Host와 Client가 각각 공격자일 때 실제 감소한 Health에 `DamageGainPerDamage`를 곱한 만큼만 충전된다.
- 남은 Health보다 큰 피해는 남은 Health만큼만 충전되고, 무적·사망·0 피해에는 충전되지 않는다.
- Meter 특성을 가진 슬롯이 여러 개면 각 슬롯이 자신의 비율대로 충전되고 최대값을 넘지 않는다.
- Client의 피해 기반 Meter는 Host 확정 상태를 받은 뒤 반영되며, prediction이나 resimulation으로 중복 충전되지 않는다.
- 사망한 공격자의 잔존 투사체 피해는 충전되고, 사망과 리스폰 사이에 Meter가 유지된다.
- 피해로 사용 비용에 도달하는 Tick에 스킬 입력이 겹쳐도 최종 사용 여부와 Meter가 Host 상태로 수렴한다.
- 100 Meter 미만에서는 각성이 시작되지 않고, 100 Meter에서 한 번 사용하면 0으로 소모된다.
- 각성 8초 동안 이동·공격·최대 체력·피해 감소·크기 배율이 적용되고 종료 뒤 원래 값으로 복귀한다.

### Skill Animation

- MaryProjectileSkill을 사용하면 Cast 동안 양손이 모이는 검증 자세가 재생된다.
- Cast가 끝나 투사체가 생성되는 Tick에 양손을 앞으로 내미는 Release 자세로 전환된다.
- Recovery 클립이 있는 스킬은 `SkillPhase` 3에서 전용 Placeholder가 해당 클립으로 교체된다.
- 스킬 재생이 손 Limb Solver의 기존 활성 여부를 임의로 변경하지 않는다.
- 같은 MaryProjectileSkill을 다른 스킬 슬롯에 장착해도 슬롯 번호와 무관하게 같은 클립이 재생된다.
- Host와 Client에서 Cast와 Release 전환 횟수가 같고 prediction 또는 resimulation으로 중복 재생되지 않는다.

## 실패 기록

실패 시 다음 값을 함께 기록한다.

- Runner Tick
- State Authority 및 Input Authority
- Damage Source의 NetworkObject 및 Input Authority와 `AppliedDamage`
- Skill1 및 Skill2의 변경 전·후 Meter
- PlayerInputData
- Health 및 IsDead
- AttackState 및 CurrentAttackId
- Movement Velocity 및 IsMovementControlLocked
- IsAttackControlLocked 및 IsSkillControlLocked
- AimDirection 및 FacingRight
- 해당 Tick이 prediction인지 resimulation인지
