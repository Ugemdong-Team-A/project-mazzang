# Player 파이프라인 구조

이 문서는 현재 소스에 실제로 적용된 Player Tick 구조와 반드시 지킬 확장 규칙만 요약한다.
미구현 기획과 검토 중인 개선안은 포함하지 않는다.

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
각 Tick 모듈 역시 동료 모듈의 구체 타입을 직접 참조하지 않으며, EditMode 계약 테스트가 이 규칙을 보호한다.

씬 로컬 `BattleCameraController`는 `NetworkPlayerData`의 로컬 캐릭터 변경을 관찰해
`NetworkRigidbody`의 Interpolation Target인 `PlayerHealth.CameraTarget`을 따라간다.
전투 카메라는 `CinemachinePositionComposer`의 데드존·축별 감쇠와 보간된 표시 대상의
프레임 이동량을 사용한다. 수평 이동은 방향 전환 지연과 최대 선행 거리를 두고,
수직 이동은 일정 속도로 지속 낙하할 때만 아래를 선행한다.
커서·조준·Facing과 Cinemachine Lookahead는 카메라 위치 입력으로 사용하지 않는다.
화면 오프셋과 Orthographic Size는 `Gameplay` 씬이 소유한다. `MapRuntime`은 시작 시
기존 `outZoneBounds`를 선택적인 카메라 컨트롤러에 전달하고, 카메라는 이 영역으로
`CinemachineConfiner2D`를 자동 구성한다. 맵 프리팹에 카메라 전용 설정은 추가하지 않는다.
대상의 큰 순간이동과 리스폰은 기존 감쇠 상태를 초기화한다. 대상이 사라지면
마지막 카메라 위치에서 대기하고, 경기 종료·결과 상태에서만 Winner Camera로 전환한다.
플레이어 모듈은 카메라에 자신을 등록하거나 카메라 존재 여부에 의존하지 않으므로,
카메라 컨트롤러가 없는 테스트 씬에서도 동일한 Spawn·사망·리스폰 파이프라인을 실행한다.

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
`ControlResolve`, `LateAction` Stage는 enum에 예약되어 있지만 현재 사용하는 모듈은 없다.

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
  보관하지 않고 이 에셋만 참조한다. 선택적인 `ActionAnimationData`를 연결하면 Startup,
  Active, Recovery가 각각 공용 Cast, Main, Recovery 연출 단계를 발행한다.
- `PlayerAttackData.Dash`가 있으면 공격 시작 Tick의 Aim 방향을 고정해 지정된 시간 동안
  일반 이동과 공격 이동 잠금보다 우선하는 속도를 `PlayerTickState`로 전달한다. 실제 Rigidbody
  변경은 계속 `PlayerMovement`가 담당하며, 대시 종료나 공격 취소 시 강제 속도를 제거한다.
- `ActionAnimationData`가 없는 기존 공격은 `PlayerAttackData.Aim`의 공격 자세를 사용한다.
  범용 액션 데이터가 있으면 현재 클립의 `ProceduralOverride`, `AnimationOnly`,
  `AnimationWithBodyAim` 설정이 표현 단계의 상체 조준 합성을 우선한다.
  `ProceduralAim` 공격은 기본 포즈에서 4본 CCD를 풀고, `AnimationOnly`는 상체 조준 보정을 끈다.
  `AnimationWithBodyAim`은 4본 CCD로 각 본을 다시 분배하지 않고, Animator가 만든 상체를
  하나의 포즈처럼 Aim 방향까지 추가 회전한다.
- 공격 중이 아닌 평상시 CCD는 현재 Animator 포즈에서 풀어 달리기와 대기의 가슴·팔 움직임을
  보존한다.
- 상체 CCD는 프리팹에서 꺼 두어 Animation 창의 클립 미리보기를 침범하지 않고,
  플레이 중 `ProceduralAim`이 선택됐을 때 `PlayerAim`이 켠다.
- `PlayerAim`은 저장된 상체 CCD 참조가 IK 재구성으로 사라져도 RAP의 부모 본과 Chain Root가
  같은 CCD를 다시 찾아 연결한다. Sprite Visual 창의 편집 정보 새로고침은 기존 IK를 재생성하지 않는다.
- `Standard2DRigIKSetup`은 편집기에서 표준 IK 구조를 생성하는 도구일 뿐이며, 플레이어 런타임
  컴포넌트는 이 도구의 존재나 보관 위치에 의존하지 않는다.
- `Standard2DCharacterSetup`은 `Animator`가 있는 캐릭터 Root에서 IK와 Sprite Visual Driver 제작
  단계를 순서대로 실행하는 편집기 진입점이다. 같은 Root의 `Animator`와 `Standard2DRigIKSetup`
  참조, 선택한 상체 조준 기준 본, 하위 `Standard2DAimAnchor`, `SpriteLibrary`, `SpriteResolver` 목록과
  전체 유효성만 관리한다. `abdomen`, `chest`, `neck` 중 선택한 기준 본으로 Body Aim CCD Chain 길이를
  계산하고, 그 직접 자식에 원점 자세의 `ResolvedAimPivot`, 그 아래에 로컬 Z -90°인 `WeaponSocket`을
  생성한다. CharSetup만 독립 Builder를 조립하며 각 관리 컴포넌트와 Builder는 서로 또는 CharSetup을
  참조하지 않는다. SLA가 아직 없어도 모든 `SpriteRenderer`에 Resolver와 Driver를
  먼저 구성할 수 있고, SLA 정보 동기화 실패는 생성 실패가 아닌 경고로 보고한다. Resolver의
  Category가 비어 있고 GameObject 이름과 SLA Category가 정확히 일치하면 Category와 초기 Label을
  자동 연결하며, 유사 이름은 추측하지 않는다.
- `SpriteVisualKeyingWindow`는 `Mazzang 2D Animation` 작업 공간의 캐릭터/무기 편집 모드를 제공한다.
  공통 영역의 캐릭터 기준과 비교용 클립은 모드를 바꿔도 유지한다. 캐릭터 편집 모드는 적합한
  캐릭터를 선택하고 Animation 창에 포커스가 오면 함께 열리며,
  버튼을 눌렀을 때만 팔·다리·발 Limb Target 6개의 현재 위치와 회전 또는 전체 Driver의 현재 모습과
  순서를 선택한 현재 프레임에 기록한다. 선택 부위의 모습과 순서 편집을 일상 작업으로 먼저 표시하고,
  Character Setup 새로고침은 그 바로 위, 선택 부위 상세 정보는 바로 아래에 둔다. 여러 부위 저장은
  접을 수 있는 영역으로, FK 굽기는 원본과 결과가 분리되는 제작 작업이므로 창 맨 아래에 둔다.
  구성 새로고침은 CharSetup Build 뒤 Driver 목록과 Animation 창 표시를 다시 동기화한다.
  무기 편집 모드는 캐릭터 클립을 읽기 전용으로 함께 시연하고 별도 무기 클립만 Animation 창에
  기록한다. 임시 `WeaponPose` 아래의 장착 외형과 내부 부품은 자유롭게 선택할 수 있으며,
  작업 범위 밖을 선택하면 세션을 없애지 않고 녹화만 멈춘다.
- `PlayerAim`은 상체 CCD Solver와 RAP Transform 하나만 직렬화한다. 하위에서 중립적인
  `Standard2DAimAnchor`를 찾으면 CharSetup을 참조하지 않고 표준 RAP으로 자동 동기화하며,
  조준 원점은 별도 `AimOrigin`이 아니라 RAP의 현재 월드 위치를 사용한다. 기준 본은 RAP의 부모,
  CCD Target은 Solver 체인에서 얻고 두 기준이 다르면 편집기 경고를 출력한다.
  `PlayerWeaponController`는 손 Solver와 표시용 `WeaponSocket`만 참조한다. 손 Solver의 명시적
  참조가 비어 있으면 Spawn 시 표준 리그 규격의 `arm_l_solver`, `arm_r_solver`를 대소문자와
  무관하게 한 번 찾아 복구한다. 이는 CharSetup을 참조하지 않는 런타임 안전망이며, 명시적
  참조가 있으면 하위 오브젝트를 검색하지 않는다.
- `PlayerAim`은 제한된 허리 각도와 최대 각도를 `PlayerTickState`에 공개한다. 무기는 이 스냅샷으로
  제한된 발사 방향을 계산하며 `PlayerAim`의 구체 타입을 직접 참조하지 않는다.
- `maxBodyAimAngle`은 허리가 꺾이는 범위만 제한하고 `facingFlipAngle`은 좌우 반전 시점만 결정한다.
  두 값은 독립적이며, 허리 제한 때문에 반전 각도를 자동으로 올리지 않는다.
- 표준 `AC_StandardPlayer`는 WholeBody 마스크의 Base Layer와 UpperChest 마스크의 Combat Layer를
  사용한다. 공격과 스킬 상태는 Combat Layer에서 상체를 덮어쓰고 하체 이동은 Base Layer에서
  계속 평가한다. AvatarMask 에셋을 런타임에 교체하지 않는다.
- 현재 Controller 상태들의 Write Defaults는 On/Off가 혼재한다. 따라서 커브가 없는 속성이 항상
  기준 포즈로 복구된다고 가정하지 않으며, 클립 전환과 IK Target 잔류 여부를 별도로 검증한다.
- Mary, Master, Aron의 `WeaponSocket`은 프리팹에서 `ResolvedAimPivot`의 직접 자식으로 두고,
  로컬 위치 `(0, 0, 0)`, 로컬 회전 `-90°`, 로컬 크기 `(1, 1, 1)`를 유지한다.
  런타임 코드는 이 계층을 재배치하거나 보정하지 않는다.
- 장착 외형은 `WeaponSocket/WeaponPose/HeldWeaponView` 순서로 구성한다. `WeaponSocket`은 장착 기준,
  `WeaponPose`는 반전과 무기 전용 동작, `HeldWeaponView`는 Muzzle·Grip과 내부 스프라이트를 담당한다.
  무기 클립은 `WeaponPose`를 재생 루트로 삼아 `HeldWeaponView`와 내부 부품의 로컬 속성만 기록한다.
  따라서 Animator와 RAP가 확정한 Socket 변환을 그대로 상속하며, 무기 클립이 RAP나 Socket을
  덮어쓰지 않는다. 무기 동작은 손 IK보다 앞선 실행 순서에서 평가한다.
- `PlayerAim`은 RAP 위치를 같은 Tick의 `PlayerTickState.AimOriginPosition`에 복사한다. `PlayerCombat`과
  `PlayerWeaponController`는 이 값을 근접 판정·드롭과 총기 Muzzle fallback 기준으로 재사용한다.
  따라서 기준 척추 본의 위치 애니메이션은 조준 원점에도 반영되며, 회전만으로는 로컬 원점인 RAP의
  위치가 변하지 않는다.
- 총기는 장착 외형의 `HeldWeaponView.Muzzle`을 발사 포즈의 단일 제작 기준으로 사용한다.
  StateAuthority는 투사체를 Muzzle의 월드 위치에서 생성하고, 캐릭터 외형 계층의 좌우 반전을 포함한
  Muzzle 로컬 `+X`의 월드 방향으로 발사한다.
  커서 방향은 캐릭터와 무기 자세를 결정하지만 투사체 방향을 Muzzle과 별도로 다시 계산하지 않는다.
  장착 외형이 없는 환경에서만 같은 Tick의 `AimOrigin`, 확정된 무기 방향과 원본 Muzzle의
  루트 상대 위치·방향으로 같은 발사 포즈를 복구한다.
- 무기와 투사체의 장착 표현은 `IWeaponHandler` 계약으로 현재 무기, Socket, 방향, 정렬 정보를 읽으며
  `PlayerWeaponController` 구체 타입을 직접 참조하지 않는다.
- 플레이어를 따라가는 월드 공간 LineRenderer 연출은 시뮬레이션 루트가 아니라
  `IWeaponHandler.PresentationRoot`를 사용한다. `PlayerWeaponController`는 이 값을
  `NetworkRigidbody.InterpolationTarget`으로 제공하므로 방패와 패링 쿨다운 표시가 렌더 보간된
  캐릭터 외형과 같은 프레임 위치를 사용한다. 기본 패링의 표시 오브젝트는 `WeaponSocket` 자식으로
  만들고, 원호 좌표는 Animator와 IK가 Socket을 갱신한 뒤 `LateUpdate`에서 계산한다. 판정과
  Networked 상태를 소유하는 `PlayerParry` 자체는 계속 플레이어 루트에 둔다.
- Sword는 선택적인 `DashData`를 참조한다. 공격 준비 시간이 끝나는 Tick에
  `IPlayerTickCommandDispatcher`로 이동 속도와 Control Lock을 요청한 뒤 타격을 판정한다.
  방향 정책은 직렬화 설정에 따라 공격 입력 순간의 방향 또는 돌진 시작 Tick의 최신 Aim 방향을 사용한다.
- 양손 Limb Solver는 IK Target을 녹화한 일반 이동·공격 클립을 재생하기 위해 평상시에도 활성화한다.
  무기를 장착하면 각 손의 Target만 해당 Grip으로 교체하고, Grip이 없는 손은 애니메이션 Target을
  유지한다. 무기를 해제하면 두 손 모두 원래 애니메이션 Target으로 복원한다.
- `PlayerAnimation`은 수평 이동 방향과 `FacingRight`를 비교해 `MoveDirection`을 -1 또는 1로
  전달한다. 표준 Controller의 Run BlendTree는 -1에서 후진, 1에서 전진 클립을 재생하므로 조준으로
  바라보는 방향과 실제 이동 방향이 달라도 발동작이 맞는다.
- `PlayerAttackData.ComboFollowUp`이 있으면 Active 시작부터 Recovery 종료까지 공격 입력을
  예약하고, Recovery가 끝날 때 후속 공격으로 직접 전환한다. `AllowRepeatedComboInput`이 켜지면
  연속 입력을 한 번의 예약으로 취급하며, 런타임 콤보 깊이는 1단계로 제한한다.
- 같은 `AttackData`를 사용하더라도 실행 주체에 따라 타이밍과 사용 규칙은 달라질 수 있으므로,
  플레이어 전용 실행 정보는 `AttackData`에 두지 않는다.
- `Projectile` 프리팹은 `ProjectileBaseSettings`로 기본 초기 속도, 수명, 탄도와 충돌 반경을
  제공하고 `AttackData`와 충돌 행동을 보관한다. `ProjectileWeapon`은 이 설정을 다시 직렬화하지 않고,
  발사 순간의 `ProjectileStatSnapshot`을 적용해 최종 `ProjectileLaunchSettings`를 만든다.
- `ProjectileWeapon`은 탄약·쿨다운·발사 연출과 `ProjectileLaunchPlan`에 따른 공통 생성 절차를
  소유한다. 샷건처럼 발사 패턴이 다른 무기는 `BuildLaunchPlan`만 재정의해 펠릿별 위치·방향·설정을
  만들며, 실제 `Runner.Spawn` 절차와 투사체 프리팹 참조는 기반 클래스가 유지한다.
- 실제 `Projectile` 이동과 충돌은 State Authority의 `FixedUpdateNetwork`만 실행한다.
  실제체와 로컬 예측 표시는 모두 `ProjectileTrajectory`로 Fusion Tick 간격과 같은 고정 시간 단계를
  누적 계산하고, 한 Tick의 이동량이 크면 같은 기준으로 Substep을 나눈다. 예측 표시는 `Runner.Tick`이
  전진한 횟수만큼만 시뮬레이션하고 `Runner.LocalAlpha`로 현재·다음 로컬 위치 사이를 보간한다. 수명도
  실제체와 같은 `TickTimer` 만료 Tick으로 환산하므로 렌더 FPS나 한 프레임에 처리한 Tick 수가 궤적과
  사거리를 바꾸지 않는다. 원격 실제 투사체는 이 공식을 실행하지 않고
  `NetworkTransform` 보간 결과만 표시한다.
- `ProjectileCollision`은 이동 구간의 CircleCast와 자신·발사자 제외 규칙만 담당하는 수동 컴포넌트다.
  이동이나 피해를 직접 실행하지 않는다. State Authority의 실제 `Projectile`은 조회 결과로 피해와
  Networked 충돌 상태를 확정하고, 발사 Client의 예측 복제본은 같은 충돌 마스크·반경·발사자로 조회해
  로컬 투사체를 접촉 위치에 멈추고 Trail을 끝내는 데만 사용한다. 예측 충돌은 피해·넉백·패링·카메라
  반응을 실행하지 않으며 Host의 확정 결과를 바꾸지 않는다. 실제 투사체가 패링되면 충돌 조회의 제외
  대상도 새 `Source`인 패링 소유자로 갱신해 패링 소유자를 즉시 다시 맞히지 않고 원래 발사자는 다시
  맞힐 수 있게 한다.
- 실제체와 예측 표시는 별도의 외형 결과 구조체를 만들지 않고 같은 `ProjectileStatSnapshot`을
  `ProjectileVisual.Apply`에 전달한다. `ProjectileVisual`은 프리팹의 원본 크기·Sprite·Trail 설정을
  기준으로 배율을 적용하므로, 고유 외형이 다른 투사체도 같은 능력치 공식을 사용한다. 현재는
  명시적인 `ScaleMultiplier`만 크기와 Trail 폭에 반영하며, 피해량 같은 다른 능력치를 임의로
  색상이나 크기에 연결하지 않는다.
- 비-Host Input Authority만 Forward 실행에서 판정 없는 `PredictedProjectile`을 즉시 표시한다.
  Host는 같은 Tick에 실제 투사체를 생성하므로 별도 예측 표시를 만들지 않는다.
- 무기 `NetworkObject`의 Input Authority는 장착할 때 장착 플레이어에게 전달하고 드롭할 때 제거한다.
  따라서 비-Host 장착자도 `Ammo`, `FireCooldown`과 발사 표시를 자신의 예측 Tick에서 먼저 처리한다.
- `ProjectileWeapon`은 별도 예측 프리팹이나 전역 매니저 없이 자신의 `GameObjectPool`을 지연 생성한다.
  풀 인스턴스는 실제 투사체 프리팹 전체를 복제한 뒤 `NetworkObject`, `NetworkBehaviour`, Collider와
  Rigidbody 시뮬레이션을 끄고 `PredictedProjectile`만 로컬 이동에 사용한다. 따라서 Sprite, Trail,
  Animator, Particle과 프리팹별 고유 외형 설정을 별도 복사 코드 없이 그대로 공유한다. 무기가 제거되면
  활성 예측체와 비활성 풀 루트를 함께 정리한다.
- Host가 생성한 실제 투사체는 발사 무기 ID, 발사 Tick, 발사 Sequence, 투사체 순번으로 이루어진
  `ProjectilePredictionKey`를 Networked 상태로 전달한다. 실제 투사체 자체에는 Input Authority를 주지 않고,
  발사 무기의 Input Authority로 로컬 발사자를 식별한다. 발사 Client는 실제체의 보간 스냅샷이 준비되면
  같은 키의 예측체에 권위 발사가 존재함을 연결하되 실제체 Transform을 추적하지 않는다. 예측체는 자신의
  로컬 궤도를 계속 표시하고 실제체의 외형은 숨겨 두므로 두 외형을 동시에 표시하거나 보간 지연 위치로
  되돌아가지 않는다. 로컬 예측체 매칭 여부가 아직 확정되지 않은 실제체도 숨긴 채 다음 Render에서
  재시도하며, 매칭 실패를 원격 투사체로 오판해 한 프레임 노출하지 않는다. 연결된 실제체가 Despawn될 때
  예측체를 풀에 반환한다.
- 패링이나 명시적 반사처럼 권위 궤도가 불연속적으로 바뀌면 State Authority가 `TrajectoryRevision`을
  증가시킨다. 이 변경을 받은 발사 Client는 기존 예측체를 풀에 반환하고, 숨겨 두었던 실제체를 현재
  `NetworkTransform` 보간 위치에서 표시한다. 모든 Peer는 그 위치에서 Trail을 다시 시작하므로 최초
  발사 위치로 되감거나 패링 전·후 위치 사이에 긴 Trail 선을 만들지 않는다. 패링 여부와 반사 궤도는
  로컬 예측하지 않고 Host 확정을 따른다.
- 다른 Client는 실제 투사체의 보간 스냅샷이 준비된 뒤 실제체 외형을 표시하고, Host는 생성한 실제체를
  즉시 표시한다. `Projectile`의 원격 루트 표시는 `NetworkTransform` 보간만 사용하며 `Render()`에서 같은
  루트 Transform에 별도 Lerp나 궤도 계산을 다시 적용하지 않는다. Trail은 각 Peer가 표시를 시작하는 현재
  보간 위치에서 시작한다.
- `ProjectileSkillData`는 생성 위치와 생성할 투사체 프리팹처럼 투사체 행동에만 필요한 값을
  보관한다. 시전과 회복 시간은 공통 Pattern에서만 설정한다.
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
- `SkillData.Patterns`는 비활성이 기본인 공통 패턴 설정을 보관한다. 실행과 UI는
  `SkillPatternView`를 통해 활성 설정을 조회하며 비활성 패턴은 null로 노출한다.
- `SkillPatternView`는 공통 Pattern의 유일한 런타임 해석 경로다. 구체 SkillData와 Skill은
  Cast, Duration, Recovery, Charge, Meter, ActionLock, StatModifier, Appearance 값을 다시
  선언하지 않고 투사체, 설치 위치, Dash처럼 실제 행동에만 필요한 데이터와 로직만 가진다.
- 공통 패턴 Inspector는 활성 체크와 펼침 상태를 분리한다. 비활성 패턴은 저장값을 보존한 채
  편집만 막고, 활성 패턴 안에서도 현재 조합에 쓰이지 않는 설정은 숨기되 값을 변경하지 않는다.
  시간 회복 Charge의 Meter 회복 결과, Meter 회복 Charge의 시작 횟수·회복 시간,
  제한 시간 없음의 시간·갱신 정책, 비차감 Meter의 차감량이 이에 해당한다.
- 세부 한국어 표시명은 `ChargeSettings`와 `MeterSettings` 및 관련 enum에만 적용한다.
  Inspector에서는 각각 `사용 횟수`, `스킬 게이지`로 표시하고 Meter의 역할은 현재 Charge 조합에
  맞는 짧은 설명으로 안내한다. 나머지 Pattern의 하위 필드는 코드 이름을 그대로 표시한다.
- `meterRefillMode`는 `resetByMeterMode`, `meterRechargePolicy`를,
  `chargeWindowMode`와 `chargeWindowDuration`은 각각 `useWindowMode`, `useWindowDuration`을,
  `consumeMode`는 오타가 있던 `comsumeMode`를 `FormerlySerializedAs`로 읽는다.
  기존 enum의 숫자값도 유지해 저장된 에셋의 의미를 바꾸지 않는다.
- `ValidatePatterns`는 활성 조합에서 실제 사용하는 수치만 검증하며 값을 변경하지 않는다.
  Inspector와 장착 경로가 같은 검증을 사용한다. Meter 회복 Charge에는 활성 Meter가 필요하고,
  Active 단계에서만 적용되는 StatModifier와 Appearance에는 활성 Duration이 필요하다.
- Duration은 개별 실행의 Active 지속시간만 담당한다. Settings는 직접 지정한 시간, Behavior는
  `Skill.BehaviorDuration`을 사용한다. 현재 대시는 `DashData.Duration`을 제공한다.
- Cast, Active, Recovery 시간은 `SkillPatternView.GetPhaseDuration`으로 해석하고 세 단계의 합은
  `TotalUseDuration`으로 계산한다. 세 단계는 하나의 Networked `SkillUsePhase`와 `PhaseTimer`를
  공유하며 별도 실행 타임라인을 만들지 않는다.
- Charge의 `ChargeWindowMode`가 `SkillChargeWindowMode.Timed`이면 설정된 초를 별도 Networked
  `ChargeWindowTimer`로 관리한다. 성공한 사용 뒤 다시 사용할 횟수가 남아 있을 때 구간을 열며,
  `SkillChargeWindowRefreshMode.Fixed`는 추가 사용으로 시간을 늘리지 않고
  `RefreshOnUse`는 성공한 추가 사용마다 제한 시간을 처음부터 다시 시작한다.
  다음 사용에 필요한 횟수가 남지 않으면 구간과 테두리를 즉시 닫되 진행 중인 시간 회복은 유지한다.
  Persistent이면 남은 횟수에 제한 시간을 두지 않는다.
  만료 시 남은 횟수를 폐기하고 재충전 타이머를 초기화하되 이미 실행 중인 행동은 취소하지 않는다.
  사망 중에도 구간 시간은 흐르며 장착 변경 시 초기화된다. 구간 전체의 버프 적용은 하지 않는다.
- 개별 Active 타이머와 대시 잠금은 항상 `ActiveDuration`을 사용한다. Charge 사용 구간과
  Active 지속시간은 서로 영향을 주지 않는다. UI는 Timed 사용 구간이 열리면 스킬 아이콘의
  4면 테두리로 남은 비율을 표시하고, 기존 Duration 바는 Active Phase만 표시한다.
- 투사체 시전 연출 시간은 기존 개별 Data 필드가 아니라 공통 Cast 설정을 사용한다.
- `ActionAnimationData`는 기본 공격, 무기, 스킬이 공유하는 선택적 연출 에셋이다. Cast,
  실제 효과가 발동하는 Main, Recovery의 각 클립마다 `FullBody`, `UpperBody`, `ArmsOnly`
  고정 마스크 레이어와 상체 조준 합성, 손 IK 정책을 독립적으로 지정한다. 단계에 클립이
  없으면 그 단계는 재생하지 않지만 게임플레이 단계 진행은 멈추지 않는다. Main은 게임플레이의
  Active 지속 여부가 아니라 실제 행동 실행 시점에 발행하므로 Active 시간이 0이어도 재생된다.
  무기 액션이 끝나거나 교체·해제되면 남은 액션 레이어를 Base 자세로 되돌린다.
  기존 스킬 애니메이션 에셋도 같은 형식으로 변환되어
  별도의 스킬 전용 애니메이션 타입은 두지 않는다. 기존 `release` 직렬화 값은 `main`으로
  자동 이전한다. Inspector의 단계명은 Cast·Main·Recovery로, 나머지 설정은 한국어 작업 용어로 표시한다.
  각 단계는 단계명과 클립을 한 줄로 표시하고, 클립이 있는 단계만 세부 설정을 펼칠 수 있다.
  스킬에서는 기존 게임플레이 단계의 Cast를 Cast, Active 진입을 Main, Recovery를 Recovery로
  발행하며 `ActionAnimationData`가 별도 시간이나 게임플레이 Phase를 소유하지 않는다.
- `SkillData` Inspector는 공통 정보와 선택 기능을 먼저 표시한다. 선택 기능의 상위 항목은
  한국어로 구분하고, 사용 횟수와 스킬 게이지의 하위 필드·선택지만 한국어 작업 용어를 사용한다.
  개별 스킬 전용 필드는 기존 구조와 순서를 유지한다.
- `SkillData.Animation`, `PlayerAttackData.Animation`, `Weapon`의 방향별 애니메이션은 모두 같은
  `ActionAnimationData` 타입을 참조한다. 무기는 성공한 기본 사용을 Main 단계로 발행한다.
- 무기의 `Aim`, `Facing`, `Brawlhalla` 방향 규칙은 입력을 받은 `PlayerWeaponController`가 한 번만
  확정한다. 확정된 같은 방향을 무기 판정과 방향별 애니메이션 선택에 사용한다.
- `Facing`과 `Brawlhalla` 무기 행동 중에는 확정한 좌우를 `PlayerTickState`로 공개한다.
  `PlayerAim`은 이 스냅샷을 따라 마우스로 인한 중간 반전을 막으며, 두 모듈은 서로 직접 참조하지 않는다.
- Primary와 Secondary는 각각 방향 규칙을 가진다. `Brawlhalla`는 위 입력과 입력 없음을
  `NoDirection`, 좌우 입력을 `Side`, 아래 입력을 `Down` 슬롯으로 확정한다.
- 방향별 공격 슬롯은 사용 여부, 캐릭터 AAD, 선택적인 무기 자체 클립을 함께 가지며, 근접 무기는 같은 슬롯에 데미지,
  Box, 판정 시점, 돌진 설정까지 공용 `WeaponAttackSlotData`에 함께 둔다. 구체 무기 이름을
  포함한 슬롯 타입이나 방향 판정과 연출 설정을 나눈 별도 배열은 만들지 않는다.
  현재 실제 방향별 슬롯은 Sword의 주 공격에만 있으며, 방패와 총기는 방향 규칙과 무관하게
  기존 주/보조 공격 데이터를 재사용한다. Inspector는 Sword에서 선택한 규칙에 맞는 슬롯만
  표시하고, 방향별 슬롯이 없는 무기에는 같은 데이터가 사용된다는 안내를 표시한다.
- 무기의 Stance 방향은 공격 방향과 별도로 설정한다. `Facing` Stance는 이동 입력으로 정한 좌우를
  유지하고 평상시 상체 CCD를 끄며, Stance 클립이 비어 있으면 기존 Idle을 그대로 사용한다.
- `PlayerAnimation`은 장착 무기의 선택적 Stance 클립만 기본 Idle 슬롯에 교체한다.
  공격 클립은 기존 방향별 `ActionAnimationData`와 Action 레이어를 그대로 사용한다.
  무기 자체 클립은 AAD에 넣지 않고 같은 `WeaponActionData`에 나란히 둔다. 성공한 무기 사용 시
  캐릭터 AAD와 무기 클립을 같은 Sequence로 시작하고, 둘 중 긴 클립이 끝날 때까지 무기 연출 상태를
  유지한다. 무기 클립이 끝나거나 무기가 교체·해제되면 `WeaponPose` 아래 속성을 기본값으로 복구한다.
- `PlayerSkillController`는 구체 스킬 타입을 검사하지 않고 사용 슬롯과 애니메이션 단계를
  Networked 이벤트로 알린다. `PlayerAnimation`은 플레이어마다 만든
  `AnimatorOverrideController` 인스턴스의 공통 Cast/Main/Recovery 슬롯을 교체하므로 공유
  Controller 에셋을 런타임에 수정하지 않는다. 기본 공격과 무기 역시 같은 재생 경로를 사용한다.
  각 슬롯은 실제 액션 클립이 아니라 이름이 고유한
  빈 Placeholder 클립을 Motion으로 가진다. `SkillPhase` 1/2/3은 현재 단계를 표시하고,
  `PlayerAnimation`은 해당 Cast/Main/Recovery State로 0.1초 고정 CrossFade한다.
  단계별 Body Mask가 달라지면 세 고정 Action 레이어의 가중치도 같은 시간 동안 함께 보간한다.
  따라서 `FullBody → UpperBody/ArmsOnly`, `UpperBody → ArmsOnly`에서 새 마스크가 놓는 부위는
  이전에 평가된 포즈에서 Base 포즈로 자연스럽게 돌아간다. 본을 직접 덮어쓰지 않으므로
  `ProceduralOverride`와 `AnimationWithBodyAim`의 후속 상체 합성을 침범하지 않는다.
- 표준 스킬 클립은 캐릭터 본을 직접 키로 잡는 대신 `arm_l_solver/arm_l_solver_Target`과
  `arm_r_solver/arm_r_solver_Target` 같은 공통 IK Target 경로를 사용할 수 있다. 클립은 Target만
  움직이며 Solver의 활성 여부를 바꾸지 않는다. Solver 활성 정책은 기존 프리팹 설정과 무기
  장착 로직이 계속 소유한다.
- `Standard2DAnimationBaker`는 편집 중인 원본을 바꾸지 않고 별도 캐릭터 복제본에서 Limb IK를
  프레임별로 평가한다. 결과에는 표준 Skeleton 전체의 FK Transform과 6개 Limb Target의 완전한
  Transform 키를 함께 기록한다. 베이크 계산은 저장 위치나 실행 시점을 모르며, 현재 저장 방식과
  사용자 진입점은 각각 별도 에셋 유틸리티와 `SpriteVisualKeyingWindow`가 담당한다.
- 스킬 효과 발동 시점은 Networked Phase가 결정한다. Animation Event는 게임플레이 발동이나
  네트워크 결과를 결정하지 않는다.
- `Skill` 런타임은 사용 조건과 실제 행동을 구현한다.
- `PlayerSkillController`는 슬롯, 입력 진입점, 쿨다운, 충전량, 선택적 Meter와
  `Cast → Active → Recovery` Networked 수명 주기를 관리한다.
- 런타임 `Skill` 인스턴스는 예측과 표현을 위해 모든 peer에서 만들고,
  Networked 슬롯 상태의 초기화와 외부 보상 지급은 State Authority만 수행한다.
- 활성 Meter 설정은 생존 중 자연 충전되고 사용 시 ConsumeMode에 따라 처리된다.
  장착 변경 시 InitialMeter로 초기화하며 사망과 리스폰 사이에는 유지한다.
  사용 요구량은 RequiredMeter, 소모량은 Cost로 구분하며 Cost 방식은 둘 다 충족해야 한다.
- Meter 방식 Charge의 Meter는 횟수 생산용이다. 사용 시 Meter를 다시 소모하지 않고 완충 시
  `SkillChargeMeterRefillMode.Full`은 최대 횟수, OneByOne은 한 횟수로 바꾸며 Meter를 0으로 만든다.
  이 조합에서는 RequiredMeter, ConsumeMode, Cost를 사용하지 않는다. 초과 충전은 이월하지 않는다.
  Full은 사용 가능한 횟수가 없고 Timed ChargeWindow가 닫혔을 때만,
  OneByOne은 최대 횟수 미만일 때 충전한다.
  자연 충전과 Host 피해 보상은 동일한 허용 규칙을 사용하되 외부 지급 권한은 Host에 유지한다.
- Timed Charge는 Meter와 독립적으로 RechargeDuration마다 한 횟수씩 회복한다.
  초기 횟수 0에서도 타이머를 시작하며 0초는 재충전 갱신 시 즉시 최대 횟수로 복구한다.
  Meter를 함께 사용하면 기본적으로 매번 요구·소모하고, Timed ChargeWindow에서는 구간을 여는
  첫 사용에만 요구·소모한다. 열린 구간에서도 자연·피해 Meter 충전은 유지한다.
- Meter 방식에 Meter가 없거나, Charge 차감량이 최대 횟수를 넘거나, 스킬 사용 자원인 Meter의
  요구량·차감량이 최대치를 넘거나, Timed ChargeWindow에 명시적인 양의 시간이 없는 설정은
  장착 전에 거부한다. 쿨다운은 계속 매 사용 시 시작한다.

현재 Charge와 Meter 조합의 의미는 다음과 같다.

| 사용 횟수 | 스킬 게이지 | 런타임 의미 |
| --- | --- | --- |
| 꺼짐 | 켜짐 | 궁극기형 게이지처럼 요구량과 사용 후 처리가 스킬 사용을 결정한다. |
| 시간으로 회복 | 꺼짐 | 횟수만 소비하고 RechargeDuration마다 회복한다. |
| 시간으로 회복 | 켜짐 | 횟수와 게이지를 모두 요구한다. Timed ChargeWindow가 열리면 첫 사용만 게이지를 처리한다. |
| 스킬 게이지로 회복 | 켜짐 | 게이지가 최대치에 도달할 때 횟수로 변환하며 스킬 사용 자체는 횟수만 소비한다. |

- 피해 기반 충전은 `CombatDamageService`가 State Authority에서 확정된 실제 체력 감소량만
  공격자의 `IDamageDealtReceiver`에 전달하며, Meter 특성을 가진 모든 슬롯이 각 비율로 받는다.
- Meter 처리는 슬롯 이름이나 구체 스킬 타입이 아니라 활성 Meter 설정으로 판별한다.
- `SkillSlotUI`도 활성 Meter 설정을 기준으로 Meter 레일과 퍼센트를 표시한다. 스킬이
  Meter를 사용하지 않으면 해당 UI를 숨기며, 슬롯 종류를 기준으로 궁극기라고 가정하지 않는다.
  Meter의 READY 표시는 Charge와 Meter의 자원 조건이며 쿨다운·행동 잠금을 포함한 판정은 아니다.
  횟수 재충전 UI는 Meter 활성 여부가 아니라 Charge의 Timed 방식으로 판별한다.
- `PlayerSkillController`는 활성 스킬의 합산 능력치 배율을 `PlayerTickState.ActiveStatModifiers`에
  공개한다. 이동과 외형처럼 배율을 소비하는 모듈은 SkillController를 직접 참조하지 않는다.
- 공통 Appearance 설정은 선택적인 `SpriteLibraryAsset`을 보관한다. `PlayerSkillController`는 구체 각성 타입을
  모르며, 활성 스킬의 SLA를 `PlayerTickState.ActiveAppearanceLibraryAsset`에 공개한다.
- 외형 SLA 자체는 Networked 값으로 전송하지 않는다. 이미 Networked인 스킬 슬롯의 Active 단계를
  모든 peer가 같은 정적 SkillData에 적용해 동일한 요청을 재구성한다. 두 슬롯이 동시에 외형을
  요청하면 궁극기 슬롯을 우선하고, null 요청은 다른 활성 외형을 막지 않는다.
- `PlayerVisual`이 캐릭터 반전·크기·피격 표시와 함께 외형 SLA 적용도 담당한다. 자식에서
  `SpriteLibrary`를 찾을 수 있으면 기본 SLA를 보관하고, 요청 SLA가 null이면 기본값을 유지한다.
  실제 SpriteLibrary setter는 참조가 바뀔 때만 호출한다. 별도
  `PlayerSpriteLibraryAppearance` 컴포넌트는 현재 플레이어 프리팹에서 사용하지 않는다.
- 기본 공격, 대시, 무기는 공격을 만드는 Tick의 공격력 배율을 `DamageInfo`에 반영한다.
  투사체와 설치물은 생성 시점의 배율을 Networked 값으로 보관하므로 비행·대기 중 버프가
  끝나거나 소유자가 사라져도 피해량이 바뀌지 않는다.
- `PlayerHealth`는 공격자의 스킬 모듈을 조회하지 않는다. 이미 확정된 공격 피해에 자신의
  `PlayerTickState` 피해 수신 배율만 적용하며, 최대 체력 배율도 같은 State에서 읽는다.
- 스킬은 `PlayerTickState`를 읽고 `PlayerTickCommands`로 변경을 요청한다.
- 스킬의 행동 잠금은 기본 공격과 무기 사용을 막을 수 있지만 무기 버리기는 막지 않는다.
  로컬 Drop 눌림은 다음 Fusion 입력 수집까지 보관해 짧은 탭도 유실되지 않게 한다.
- 네트워크 결과에 영향을 주는 방향과 타이밍은 Fusion Tick 입력에서 계산하고
  필요한 경우 Networked 슬롯 상태에 한 번 저장한다.
- Render 프레임의 로컬 입력이나 `Time.deltaTime`으로 게임플레이 결과를 결정하지 않는다.

## 기본 능력치 데이터

- 각 플레이어 프리팹은 일반 `MonoBehaviour`인 `PlayerStatsInstaller`와 선택적인
  `PlayerStatsData` 참조를 소유한다. `CharacterData`와 `PlayerController`는 이 배포를 담당하지 않는다.
- Installer는 같은 루트 GameObject의 `IStatsConsumer`만 찾아 초기화한다. 따라서 중첩된
  `NetworkObject`나 다른 플레이어의 소비자를 잘못 초기화하지 않는다.
- Installer는 Tick 모듈이 아니다. Stage, State, Command를 소유하지 않고 `Awake`에서 정적 설정만
  전달한다.
- `PlayerStatsData`가 없거나 Installer 자체가 없는 프리팹에서도 각 소비자는 자신의 안전한 기본값으로
  동작해야 한다. 현재 기본값은 이동 속도 7, 최대 체력 100이다.
- 현재 중앙화한 값은 실제로 공통 Modifier와 결합되는 이동 속도와 최대 체력뿐이다. 공격 데이터,
  이동 가속도, 점프 규칙처럼 소유자가 명확한 설정은 해당 Data 또는 모듈에 유지한다.
- 활성 스킬 배율은 계속 `PlayerTickState.ActiveStatModifiers`로 전달한다. 기본 능력치 Data는 TickState를
  대체하지 않으며, 소비자는 `기본값 × 활성 배율`로 최종 값을 계산한다.

## Dash 데이터와 실행

`DashData`는 스킬, 기본 공격, 무기가 함께 참조할 수 있는 이동 시간과 속도,
선택적인 플레이어 충돌 공격을 보관한다. `DashSkillData`는 충전 수와 시전·회복 시간처럼
스킬에만 해당하는 규칙을 보관하고 공통 `DashData`를 참조한다.

현재 Dash는 사용 시점의 마우스 Aim 방향을 고정하고 Startup, Active, Recovery 전체 동안
Movement, Attack, Skill 입력을 잠근다. 확정 방향은 Aim Override와 슬롯의 Networked 상태로
유지하며 Active 중 입력 변화에 따라 바뀌지 않는다. 대시 충돌 피해가 있으면 State Authority가
한 대상에 한 번만 적용하고, 생성 시점의 공격력 배율을 `DamageInfo`에 반영한다.

## 구조 변경 체크리스트

1. 이 값의 실제 소유 모듈은 누구인지 정한다.
2. 다른 모듈이 읽기만 하면 StateSource에 공개한다.
3. 다른 모듈이 변경을 요청해야 하면 Command와 담당 Sink를 만든다.
4. 실행 시점을 Stage와 Order로 정한다.
5. 게임플레이 계산은 Tick 입력과 `Runner.DeltaTime`만 사용한다.
6. 표현은 Networked 결과를 읽고 Render에서 갱신한다.
7. Host와 Client 양쪽에서 prediction, resimulation, 사망·리스폰을 검증한다.

상세 수동 검증 항목은
[PlayerTickBaseline.md](../../Assets/_Main/Tests/Manual/PlayerTickBaseline.md)를 따른다.
