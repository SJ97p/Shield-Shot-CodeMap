# ShieldDefenseInputAdapter

## 책임

`DefenseInputSample`을 현재 방패의 `ShieldOrbitController` 호출로 번역한다. 하나의 활성 방어 포인터를 추적하고 해제·취소 시 상태를 초기화한다.

## 설계 이유

방패 생성과 장착은 `WeaponManager`의 책임으로 유지하고, V2는 `CurrentShield` 계약만 사용한다. 방어 Gesture 해석과 실제 궤도 회전을 분리해 양쪽 변경의 파급 범위를 줄였다.

