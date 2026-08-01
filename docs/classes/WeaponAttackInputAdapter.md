# WeaponAttackInputAdapter

## 책임

`AttackInputSample`을 기존 `WeaponBase` 입력 API로 번역한다. 활성 포인터별로 입력 시작 당시 무기를 기억해 무기 교체 또는 입력 취소 상황을 안전하게 처리한다.

## 설계 이유

공격 Interpreter가 `WeaponManager`, `WeaponBase`, 활 발사 규칙을 직접 알면 V2 도메인이 기존 게임플레이 구현에 결합된다. Adapter가 두 세계 사이의 변환 책임을 맡는다.

