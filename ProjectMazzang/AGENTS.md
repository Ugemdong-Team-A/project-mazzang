# ProjectMazzang 작업 지침

`Assets/_Main/Scripts/Player`, `Assets/_Main/Scripts/Skills`,
`Assets/_Main/Scripts/Weapons`의 플레이어 파이프라인을 변경하기 전에 다음 문서를 확인한다.

- `Docs/Player/Architecture.md`: 현재 소스에 적용된 구조와 반드시 지킬 규칙
- `Docs/Player/RefactoringBacklog.md`: 검토 중이지만 아직 적용하지 않은 개선안
- `Assets/_Main/Tests/Manual/PlayerTickBaseline.md`: 멀티플레이 수동 회귀 검증 기준

`Architecture.md`와 실제 소스가 다르면 소스를 기준으로 판단하고 문서를 함께 갱신한다.
`RefactoringBacklog.md`의 항목은 현재 구현으로 간주하지 않으며, 명시적인 작업 범위 없이 구현하지 않는다.
