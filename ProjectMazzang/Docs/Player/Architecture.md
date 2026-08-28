# Player 파이프라인 구조

이 문서는 현재 소스에 실제로 적용된 Player Tick 구조와 확장 규칙을 요약한다.
미구현 제안은 [RefactoringBacklog.md](./RefactoringBacklog.md)에 따로 기록한다.

## 목표

- 플레이어 모듈이 서로의 구체 클래스 타입을 몰라도 같은 네트워크 Tick 안에서 협력한다.
- 모든 클라이언트와 State Authority가 동일한 순서로 결정론적 게임플레이를 계산한다.
- 시뮬레이션 결과와 화면 표현을 구분한다.
- 새로운 캐릭터, 스킬, 무기를 기존 모듈의 직접 참조 증가 없이 추가한다.

## 전체 흐름

```text
Fusion FixedUpdateNetwork
        │
        ▼
PlayerController
  1. 모든 StateSource에서 현재 상태 Capture
  2. Stage → Order 순서로 Module.Simulate 실행
  3. 각 Module 뒤에 다시 Capture
  4. Command 요청이 생기면 즉시 Sink들에 Dispatch
  5. 처리 뒤 다시 Capture하여 같은 Tick의 다음 Module에 반영
        │
        ▼
Fusion Render
  PlayerController → 각 Module.Present(read-only 용도의 TickState)
```

`PlayerController`는 모듈의 구체 타입이나 게임 규칙을 알지 않는다. 같은 `NetworkObject`에 속한
`PlayerTickModule`을 수집하고 `Stage`, `Order`만으로 실행한다. 같은 플레이어에서
`Stage + Order`가 중복되면 오류를 출력하고 파이프라인을 시작하지 않는다.

## 현재 Tick 순서

| 순서 | 모듈 | Stage | Order |
| ---: | --- | --- | ---: |
| 1 | PlayerHealth | Begin | 0 |
| 2 | PlayerSkillController | SkillIntent | 0 |
| 3 | PlayerParry | DefenseIntent | 0 |
| 4 | PlayerWeaponController | PrepareAction | 0 |
| 5 | PlayerCombat | Action | 0 |
| 6 | PlayerMovement | Motion | 0 |
| 7 | PlayerVisual | Motion | 100 |
| 8 | PlayerAim | Aim | 0 |
| 9 | PlayerAnimation | Finalize | 0 |
| 10 | PlayerStatusUI | Finalize | 100 |

같은 Stage에 여러 모듈이 들어가는 것은 정상이다. `Order`는 그 Stage 안의 정밀한 선후관계만 표현한다.
Unity의 `DefaultExecutionOrder`가 아니라 `PlayerController`가 네트워크 시뮬레이션 순서를 보장한다.

## 핵심 객체와 계약

### PlayerTickModule

- 모든 Tick 모듈의 공통 기반 클래스다.
- `Stage`, `Order`, `Simulate(in PlayerTick)`으로 시뮬레이션 위치를 선언한다.
- `Present(in PlayerTickState)`는 Render 시점의 표현 갱신에 사용한다.
- 자체 `FixedUpdateNetwork`와 `Render`를 실행하지 않고 `PlayerController`의 호출을 따른다.
- 보호된 `Commands`는 모듈 내부 보조 메서드에서도 요청을 보낼 수 있게 연결된다.

### PlayerTickState

- 현재 Tick에서 다른 모듈이 읽을 수 있는 플레이어 상태 스냅샷이다.
- 각 상태의 실제 소유 모듈이 `IPlayerTickStateSource.CaptureTickState`로 값을 채운다.
- `Simulate`에서 다른 모듈 소유의 State를 직접 수정하지 않는다.
- 같은 Tick의 선행 모듈 또는 Command 처리 결과는 재Capture 후 후행 모듈이 읽는다.
- `HasMovement`, `HasCombat` 같은 `Has...` 값으로 선택적 모듈 존재 여부를 확인한다.

### PlayerTickCommands

- 상태의 직접 변경이 아니라 **담당 모듈에 보내는 즉시 처리 요청**이다.
- 요청자는 담당 모듈의 구체 타입을 알 필요가 없다.
- 요청 직후 `PlayerController`가 모든 `IPlayerTickCommandSink`에 처리 기회를 준다.
- Command 처리 중 새 Command가 생기면 남은 Sink 또는 다음 Resolve Pass에서 처리한다.
- 최대 8 Pass 뒤에도 남은 요청은 오류다. 재진입 중 Dispatch는 중첩 실행하지 않는다.
- 요청이 없는 `TryConsume...`은 `false`와 기본 출력값을 반환한다.

현재 주요 예시는 공격 취소, 넉백, Aim override, Facing, 종류별 Control Lock,
강제 이동 속도, 무기 사용 요청이다. 각 Command는 의미상 하나의 담당 Sink만 소비해야 한다.

### StateSource와 CommandSink

- `IPlayerTickStateSource`: 자신이 소유한 Networked 상태를 공용 TickState에 복사한다.
- `IPlayerTickCommandSink`: 자신이 담당하는 Command만 소비하고 실제 Networked 상태를 변경한다.
- 하나의 모듈이 두 역할을 함께 가질 수 있다.
- 새로운 Command를 추가할 때는 요청 API와 담당 Sink를 함께 정하고, 같은 Tick 안에 완전히 소비되는지 확인한다.

## 공격 데이터

- `AttackData`는 공격 ID, 피해, 넉백, CC처럼 대상에게 적용되는 공통 결과를 보관한다.
- CC는 `CrowdControlType`으로 의미를 저장하고 `CrowdControlRules`가 현재의 `PlayerControlLock` 조합으로 변환한다. 외부 표를 연결할 때도 값이 없거나 읽기에 실패하면 코드의 기본 규칙을 사용한다.
- `stopMovementOnApply`는 적중 즉시 속도 제거, `activationDelay`는 CC 잠금의 발동 시점을 담당한다.
- `BoxAttackData`는 공통 결과에 박스 판정의 위치와 크기를 추가한다.
- `PlayerAttackData`는 플레이어가 공격을 실행하는 Startup, Active, Recovery,
  Cooldown과 Aim, Movement 규칙을 SO로 보관한다. `PlayerCombat`은 인라인 공격 설정을
  보관하지 않고 이 에셋만 참조한다.
- 공격 자세는 `ProceduralAim`, `AnimationOnly`, `AnimationWithBodyAim` 중 하나를 선택한다.
  `ProceduralAim` 공격은 기본 포즈에서 4본 CCD를 풀고, `AnimationOnly`는 상체 조준 보정을 끈다.
  `AnimationWithBodyAim`은 4본 CCD로 각 본을 다시 분배하지 않고, Animator가 만든 상체를
  하나의 포즈처럼 Aim 방향까지 추가 회전한다.
- 공격 중이 아닌 평상시 CCD는 현재 Animator 포즈에서 풀어 달리기와 대기의 가슴·팔 움직임을
  보존한다.
- 상체 CCD는 프리팹에서 꺼 두어 Animation 창의 클립 미리보기를 침범하지 않고,
  플레이 중 `ProceduralAim`이 선택됐을 때 `PlayerAim`이 켠다.
- `Standard2DRigIKSetup`은 편집기에서 표준 IK 구조를 생성하는 도구일 뿐이며, 플레이어 런타임
  컴포넌트는 이 도구의 존재나 보관 위치에 의존하지 않는다.
- `PlayerAim`은 상체 CCD Solver만 명시적으로 참조하고 Target과 기준 본은 Solver 체인에서 얻는다.
  `PlayerWeaponController`는 손 Solver와 표시용 `WeaponSocket`만 명시적으로 참조한다.
- 실제 `AimOrigin` 트랜스폼은 `PlayerAim`만 소유한다. 같은 Tick의 `PlayerCombat`과
  `PlayerWeaponController`는 `PlayerTickState`에 복사된 위치를 판정·발사·드롭 기준으로 재사용하며,
  애니메이션을 따라 움직이는 `WeaponSocket`은 게임플레이 원점으로 사용하지 않는다.
- Render에서 얻은 `WeaponSocket` 좌표는 외관에만 사용하고 다음 Tick의 판정 값으로 넘기지 않는다.
- 양손 Limb Solver는 평상시 `ProceduralAim`에서 꺼 두어 고정된 손 Target이 Animator의 팔
  자세를 덮지 않게 한다. `AnimationOnly`와 `AnimationWithBodyAim` 공격 중에는 클립이 움직이는
  원래 손 Target을 복원하며, 무기를 장착했다면 각 손의 Grip이 해당 Target보다 우선한다.
- 같은 `AttackData`를 사용하더라도 실행 주체에 따라 타이밍과 사용 규칙은 달라질 수 있으므로,
  플레이어 전용 실행 정보는 `AttackData`에 두지 않는다.
- `Projectile` 프리팹은 자신의 초기 속도, 수명, `AttackData`를 보관하고 충돌 시 공격 결과를 전달한다.
- `ProjectileSkillData`는 시전과 회복 시간, 생성 위치, 생성할 투사체 프리팹만 보관한다.
- 스킬은 투사체의 방향과 소유자만 초기화하며, 투사체의 밸런스 값을 중복해서 보관하지 않는다.

## Control Lock

`PlayerControlLock`은 Flags이며 현재 세 영역을 독립적으로 제어한다.

| 종류 | 담당 모듈 | 의미 |
| --- | --- | --- |
| Movement | PlayerMovement | 새 이동·점프 입력 제한 |
| Attack | PlayerCombat | 새 기본 공격 입력 제한 |
| Skill | PlayerSkillController | 새 스킬 사용 제한 |

Control Lock은 새 입력을 막을 뿐 이미 진행 중인 행동을 자동으로 취소하지 않는다.
공격 취소, 강제 속도, 넉백 등은 별도 Command다. 복합 Lock 요청은 종류별 pending 값으로 나뉘며,
같은 종류의 요청이 겹치면 더 긴 시간이 유지된다.

## 스킬 확장 규칙

- `SkillData`는 조정 가능한 정적 설정을 보관한다.
- `Skill` 런타임은 사용 조건과 실제 행동을 구현한다.
- `PlayerSkillController`는 슬롯, 입력 진입점, 쿨다운, 충전량, 선택적 Meter와
  `Cast → Active → Recovery` Networked 수명 주기를 관리한다.
- 런타임 `Skill` 인스턴스는 예측과 표현을 위해 모든 peer에서 만들고,
  Networked 슬롯 상태의 초기화와 외부 보상 지급은 State Authority만 수행한다.
- `IMeterSkill`의 Meter는 생존 중 자연 충전되고 사용 성공 시 비용이 차감된다.
  장착 변경 시 0으로 초기화하며 사망과 리스폰 사이에는 유지한다.
- 피해 기반 충전은 `CombatDamageService`가 State Authority에서 확정된 실제 체력 감소량만
  공격자의 `IDamageDealtReceiver`에 전달하며, Meter 특성을 가진 모든 슬롯이 각 비율로 받는다.
- 첫 Meter 콘텐츠인 `UltimateAwakeningSkill`은 Meter, Duration, Stat Modifier 계약을 조합하며
  테스트 캐릭터인 기사의 `ultimateSkill`에 장착한다. Meter 처리는 슬롯 역할이 아닌 인터페이스로 판별한다.
- 스킬은 `PlayerTickState`를 읽고 `PlayerTickCommands`로 변경을 요청한다.
- 네트워크 결과에 영향을 주는 방향과 타이밍은 Fusion Tick 입력에서 계산하고
  필요한 경우 Networked 슬롯 상태에 한 번 저장한다.
- Render 프레임의 로컬 입력이나 `Time.deltaTime`으로 게임플레이 결과를 결정하지 않는다.

### Dash 확장 방향

현재 Dash는 사용 시점의 마우스 Aim 방향을 고정하고 Startup, Active, Recovery 전체 동안
Movement, Attack, Skill 입력을 잠근다. 향후 일반적인 Dash 변형은 `DashSkillData`에서
다음 두 축을 독립적으로 설정하는 방식이 적합하다.

```text
Direction Source  : Aim / MoveInput / Facing
Capture Timing    : OnUseStarted / OnDashStarted
Control Locks     : Movement, Attack, Skill의 Flags 조합
```

`OnDashStarted` 방향은 실제 Active 진입 Tick의 `PlayerInputData`에서 한 번 확정하고
Networked 방향값에 저장한다. 이후 모든 Active Tick은 저장된 같은 방향만 사용해야
prediction과 resimulation 결과가 일치한다. MoveInput이 0일 때의 fallback은 Aim 또는 Facing처럼
데이터 정책으로 명시한다.

단계별 잠금이 필요해지면 Startup, Active, Recovery의 Lock 정책을 분리할 수 있다.
단순한 방향 고정 Dash, 충돌 Dash, 후딜 Dash는 공통 런타임으로 처리하고 순간이동, 추적,
연쇄 이동처럼 수명 주기 자체가 다른 특수 스킬만 별도 `Skill` 구현으로 분리한다.

### Dash 이펙트

VFX와 SFX 설정을 Data에 추가하는 것은 가능하지만 판정과 표현의 실행 위치는 구분한다.

- 이동, 충돌, 피해: `Simulate`의 네트워크 게임플레이
- 궤적, 잔상, 소리, 카메라 피드백: `Present` 또는 별도 표현 객체
- 한 번만 재생할 효과: Networked phase 변화나 byte sequence를 관찰하여 중복 재생 방지

## 새 기능 체크리스트

1. 이 값의 실제 소유 모듈은 누구인지 정한다.
2. 다른 모듈이 읽기만 하면 StateSource에 공개한다.
3. 다른 모듈이 변경을 요청해야 하면 Command와 담당 Sink를 만든다.
4. 실행 시점을 Stage와 Order로 정한다.
5. 게임플레이 계산은 Tick 입력과 `Runner.DeltaTime`만 사용한다.
6. 표현은 Networked 결과를 읽고 Render에서 갱신한다.
7. Host와 Client 양쪽에서 prediction, resimulation, 사망·리스폰을 검증한다.

상세 수동 검증 항목은
[PlayerTickBaseline.md](../../Assets/_Main/Tests/Manual/PlayerTickBaseline.md)를 따른다.
