# ProjectMazzang 작업 지침

`Assets/_Main/Scripts/Player`, `Assets/_Main/Scripts/Skills`,
`Assets/_Main/Scripts/Weapons`의 플레이어 파이프라인을 변경하기 전에 다음 문서를 확인한다.

- `Docs/Player/Architecture.md`: 현재 소스에 적용된 구조와 반드시 지킬 규칙
- `Assets/_Main/Tests/Manual/PlayerTickBaseline.md`: 멀티플레이 수동 회귀 검증 기준

`Architecture.md`와 실제 소스가 다르면 소스를 기준으로 판단하고 문서를 함께 갱신한다.
미구현 기획이나 대화 중인 개선안은 아키텍처 문서에 기록하지 않는다. 구현이 실제로 반영된 뒤
현재 구조와 제약만 문서에 갱신한다.
