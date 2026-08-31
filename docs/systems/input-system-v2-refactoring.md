# 양손 조작을 믿을 수 있게 만들기 위한 Input V2

## 입력은 전투 감각의 출발점이었습니다

이 게임의 핵심은 한 손으로 방패를 유지하고 다른 손으로 무기를 조작하는 경험입니다. V1은 입력을 받은 뒤 원시 데이터를 세밀하게 보정하거나, 작은 움직임을 어느 단계에서 걸러낼지 관리하기 어려웠습니다. 손떨림 같은 작은 입력까지 그대로 전달되면 이후 제스처와 무기·방패 처리도 불필요하게 흔들릴 수 있다고 봤습니다.

실제 모바일 장애를 수치로 해결했다는 이야기가 아닙니다. 마감 전에 입력 데이터의 책임을 더 명확히 쥐고, 이후 환경과 네트워크에 적용할 때도 같은 기준으로 다루기 위해 선제적으로 다시 설계한 작업입니다.

## 시작 위치에서 역할을 정하고, 의미 있는 입력만 전달했습니다

터치가 시작되면 화면 위치로 공격 또는 방어 채널을 정하고, 그 손가락이 끝날 때까지 같은 역할을 유지합니다. V2는 Unity 입력 수집, 시작 위치 정책, 미세 움직임 필터, 프레임 내 이동 병합, 공격/방어 라우팅, 제스처 해석, 기존 무기·방패 API 연결을 나눴습니다.

```text
Unity Pointer
 → 작은 움직임 필터
 → 공격 / 방어 Router
 → 제스처 해석
 → WeaponAttackInputAdapter / ShieldDefenseInputAdapter
```

`PointerMovementThresholdFilter`는 최소 거리보다 작은 움직임을 걸러내고, `CombatPointerRouter`는 시작 위치에서 정한 공격·방어 역할을 포인터 수명 동안 유지합니다.

## V1을 남긴 채 비교했습니다

기존 V1을 바로 고치면 회귀가 생겼을 때 기준 동작과 비교하기 어려워집니다. 그래서 V1은 보존하고 V2를 별도 경로로 만들었으며, 동일 입력 시나리오를 반복 재생하는 벤치마크를 함께 만들었습니다. 입력 Marker 기준 Windows Development Build 76.36%, Galaxy S23+ 65.83% 감소를 확인했지만, 이는 전체 게임 CPU나 네트워크 트래픽이 아니라 입력 파이프라인만 비교한 결과입니다.

## 코드에서 확인할 수 있는 지점

- `UnityPointerSource`, `UnityPointerInputDriver`: Unity 입력을 공통 샘플로 수집
- `PointerMovementThresholdFilter`: 미세 이동 필터링
- `CombatPointerRouter`: 터치 시작 위치 기반 공격/방어 분기
- `AttackGestureInterpreter`, `DefenseGestureInterpreter`: 역할별 제스처 해석
- `WeaponAttackInputAdapter`, `ShieldDefenseInputAdapter`: 기존 게임플레이 API 연결

다음에는 V2의 데이터 경계를 네트워크 Tick과 패킷 정책에 어떻게 연결할지 별도의 기준과 측정으로 검증하고 싶습니다.
