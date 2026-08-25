# Skill Meter 설계 메모

## 확정 방향

- Meter는 궁극기 전용 타입이 아니라 개별 스킬이 선택적으로 사용하는 공통 특성이다.
- 현재 Meter는 슬롯별 `SkillSlotRuntimeState`에 보관하는 Networked 상태로 설계한다.
- 자연 충전과 사용 비용은 Player Tick에서 계산한다.
- 피해 기반 충전은 피격자의 실제 체력 감소량을 Host가 확정한 뒤 공격자에게 지급한다.
- 궁극기 사용 가능 여부는 확정된 Networked Meter를 기준으로 판단한다.

## 후속 검토

- 피해 Meter의 즉각적인 클라이언트 예측은 별도의 적중 예상 및 표시 전용 보정 흐름이 필요하다.
  우선 Host 확정값을 HUD에서 부드럽게 표시하고, 실제 플레이에서 지연이 문제가 될 때만
  확정 Meter와 분리된 예상 표시 파이프라인을 검토한다.
- 피해 보상 수신자는 첫 구현에서 공격자 `NetworkObject`의 `IDamageDealtReceiver`를 조회한다.
  타격 빈도나 공격 주체 종류가 늘어 조회 비용 또는 책임 구분이 문제가 되면 Spawn/Despawn에
  맞춘 수신자 캐시나 명시적인 Instigator 계약으로 리팩터링한다.
