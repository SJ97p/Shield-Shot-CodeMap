# CombatPointerRouter

## 책임

정규화된 포인터 샘플을 공격 또는 방어 Gesture 경로로 전달한다.

## 협력 객체

- `ICombatInputChannelResolver`: 현재 레이아웃에서 입력 채널 판정
- 공격 `IPointerGestureSink`
- 방어 `IPointerGestureSink`

## 설계 이유

Router가 좌우·상하 계산식까지 소유하면 설정 정책과 전달 책임이 함께 바뀐다. 영역 계산은 Resolver로 분리하고 Router는 채널별 전달과 활성 포인터 추적에 집중한다.

