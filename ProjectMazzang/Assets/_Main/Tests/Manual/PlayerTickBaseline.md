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
- 공격 도중 control lock이 시작되면 공격이 취소된다.

### 피격과 control lock

- 유효한 피해는 Health를 정확히 한 번 감소시킨다.
- 피해가 적용된 Tick에 진행 중인 공격이 취소된다.
- Knockback이 0이 아니면 동일한 피해 정보의 속도가 Movement에 적용된다.
- 공격 취소와 Knockback 명령이 다음 Tick까지 남지 않고 요청된 시뮬레이션 호출에서 처리된다.
- KnockbackControlLock 동안 이동 및 일반 공격 입력이 적용되지 않는다.
- Timer 만료 뒤 다음 유효 Tick부터 이동과 일반 공격이 가능하다.

### 사망과 리스폰

- Health가 0이 된 Tick에 IsDead가 true가 되고 공격이 취소된다.
- 사망 중 Movement 입력이 적용되지 않는다.
- 리스폰 시 KnockbackControlTimer와 ControlLockTimer가 초기화된다.
- 리스폰 무적 시간 동안 피해가 적용되지 않는다.

### Aim과 Animation

- Movement에서 확정된 Facing을 Aim이 같은 Tick에 읽는다.
- Aim에서 확정된 방향과 각도를 Animation이 같은 Tick에 읽는다.
- Aim override가 끝난 뒤 일반 입력 Aim으로 복귀한다.
- 공격의 Aim override와 Weapon 사용 요청이 요청된 Tick 안에서 처리된다.

### 예측과 권위 상태

- Client 입력으로 이동, 점프, 공격할 때 지속적인 위치 correction이 발생하지 않는다.
- 공격과 피격이 같은 Tick 근처에서 발생해도 Host와 Client의 AttackState가 일치한다.
- Knockback 종료 Tick에 Host와 Client가 동시에 입력 제어를 회복한다.
- 반복 입력 또는 resimulation 뒤 Damage, DeathSequence, AttackSequence가 중복 증가하지 않는다.
- Despawn 및 Respawn 뒤 이전 Tick의 control lock이나 공격 상태가 남지 않는다.

## 실패 기록

실패 시 다음 값을 함께 기록한다.

- Runner Tick
- State Authority 및 Input Authority
- PlayerInputData
- Health 및 IsDead
- AttackState 및 CurrentAttackId
- Movement Velocity 및 IsControlLocked
- AimDirection 및 FacingRight
- 해당 Tick이 prediction인지 resimulation인지
