# Player 리팩터링 Backlog

이 문서는 현재 구조에서 발견된 개선 후보를 보관한다.
아래 내용은 **아직 적용된 구조가 아니며**, 실제 필요성과 비용을 다시 확인한 뒤 별도 커밋으로 진행한다.

## 우선 원칙

- 새 전투 기능을 추가하는 동안 현재 State와 Command의 의미가 유지되면 아래 작업을 먼저 할 필요는 없다.
- 스킬, 무기, 공격 데이터가 늘어도 구체 모듈 직접 참조를 새로 만들지 않는 한 대부분 독립적으로 확장할 수 있다.
- 추상화는 실제 중복이나 오용 사례가 생겼을 때 추가한다.
- 네트워크 상태, 직렬화 필드, 기존 프리팹 할당을 바꾸는 작업은 별도 검증 가능한 커밋으로 분리한다.

## 개선 후보

### 1. Present 읽기 경계 강화

**현재:** `PlayerTickModule.Present(in PlayerTickState)`가 같은 State 객체를 읽는다.
표현 전용 컴포넌트도 Tick Module이어야 하는지는 아직 확정하지 않았다.

**아쉬운 점:** `in`은 객체 참조의 재대입만 막는다. 같은 Assembly의 코드는
`internal set`에 접근할 수 있어 표현 코드의 완전한 읽기 전용 계약은 아니다.

**검토안:**

- Present에 읽기 전용 View 또는 인터페이스만 전달한다.
- 시뮬레이션이 전혀 없는 표현 객체가 늘어나면 별도의 가벼운 Present 계약을 둔다.
- 모든 모듈을 다시 감싸는 공통 `PlayerComponent` 기반 클래스는 즉시 도입하지 않는다.

**도입 시점:** 표현 전용 객체가 실제로 여러 개 생기거나 State 오염 실수가 발생할 때.

### 2. PlayerTickState 쓰기 권한 제한

**현재:** State 값은 `internal set`이며 StateSource의 Capture에서만 쓰는 것을 규칙으로 삼는다.

**아쉬운 점:** 컴파일러가 소유권 규칙을 완전히 강제하지는 않는다.

**검토안:** 외부에는 getter 전용 View를 전달하고, Capture 전용 Writer/Builder만 setter에 접근시킨다.
State를 요청 창구로 사용하지 않으며 상태 변화는 계속 Command를 거친다.

**도입 시점:** State 필드가 더 늘어나거나 협업 중 직접 대입 실수가 반복될 때.

### 3. Command 영역 분리와 소비 진단

**현재:** 모든 요청이 하나의 `PlayerTickCommands`에 있고, 의미별 담당 Sink가
`TryConsume...`으로 자기 요청을 소비한다. 미소비 요청은 최대 8 Pass 뒤 오류가 난다.

**아쉬운 점:** 클래스 이름만으로 각 Command의 담당 Sink를 바로 알기 어렵고,
두 Sink가 같은 요청을 소비하려는 설계 오류를 별도로 진단하지 않는다.

**검토안:**

- Command 수가 충분히 늘어나면 Movement, Combat, Skill 등 의미 영역별 API로 묶는다.
- 요청별 request/consume count와 최초 소비자 정보를 Dispatch 단위로 기록한다.
- 소비자가 없거나 둘 이상이면 즉시 명확한 경고를 출력한다.
- 등록용 인터페이스를 다수 추가하는 ContextUnit식 의식은 되풀이하지 않는다.

**도입 시점:** Command 활용이 안정되고 담당자 혼동이나 중복 소비가 실제 문제로 나타날 때.

### 4. Control Lock 정책 확장

**현재:** Movement, Attack, Skill 세 종류이며 새 입력만 막는다.

**검토안:** 필요할 때만 Weapon, Defense, Interaction 같은 별도 의미를 추가한다.
UI가 잠금 이유나 남은 시간을 표현해야 한다면 단순 bool을 넘어 읽기 전용 Lock 상태를 공개한다.
Dash처럼 단계별 정책이 필요한 기능은 Startup, Active, Recovery별 Lock 조합을 Data로 둘 수 있다.

**주의:** `Attack`에 무기 버리기, 보조 무기, 패링까지 임의로 포함하지 않는다.
기존 enum 값의 의미를 넓히기보다 새 영역을 명시적으로 추가한다.

### 5. DashData 일반화

**현재:** Dash 방향과 Control Lock 조합이 `DashSkill`에 고정되어 있다.

**검토안:**

- `DirectionSource`: Aim / MoveInput / Facing
- `CaptureTiming`: OnUseStarted / OnDashStarted
- MoveInput이 0일 때의 fallback
- `PlayerControlLock` 조합
- 필요 시 단계별 Lock, 이동 곡선, 충돌 종료 정책
- VFX, SFX, 카메라 피드백 설정

방향 Source와 Capture Timing을 하나의 거대한 enum으로 합치지 않는다.
실제 돌진 시작 시점의 방향은 로컬 Render 입력이 아니라 그 Tick의 Fusion 입력에서 확정한다.

### 6. 레거시 Context 파일 정리

**현재:** 런타임 Player 파이프라인은 `PlayerContext`, `IPlayerContextUnit`, `PlayerModule`을
사용하지 않는다. 관련 파일과 주석 처리된 코드는 유물로 남아 있다.

**검토안:** 새 구조가 충분히 안정된 뒤 참조와 직렬화 영향이 없음을 확인하고 별도 정리 커밋에서 삭제한다.
기능 개발과 동시에 제거할 필요는 없다.

### 7. 파이프라인 가시성 도구

**현재:** Stage와 Order 충돌은 실행 시 오류로 확인한다.

**검토안:** 모듈 수가 많아지면 현재 플레이어의 정렬된 Stage/Order, StateSource,
CommandSink 목록을 Inspector 또는 개발 로그에서 한 번에 확인하는 진단 기능을 추가한다.

**도입 시점:** 캐릭터별 모듈 구성이 달라져 순서 파악이 어려워질 때.

## 작업 전 판단 질문

Backlog 항목을 시작하기 전에 다음을 확인한다.

1. 실제 버그나 반복되는 개발 비용을 해결하는가?
2. 새 타입과 인터페이스 수보다 제거되는 결합과 실수가 더 큰가?
3. Host와 Client의 Tick 순서를 바꾸는가?
4. 기존 Networked 상태 또는 Unity 직렬화 할당에 영향을 주는가?
5. 독립적인 커밋과 수동 멀티플레이 검증으로 되돌릴 수 있는가?
