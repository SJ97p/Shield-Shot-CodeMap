# Network Weapon & Shield Spawn

## Problem

PvP에서도 로비/인벤토리에서 장착한 무기와 방패를 사용해야 했습니다. 하지만 원격 클라이언트는 상대의 로컬 `WeaponItem` 또는 `ShieldItem` 인스턴스를 그대로 가질 수 없습니다.

## Solution

장착 데이터를 직접 참조하는 대신, networked ID를 기준으로 각 클라이언트가 같은 prefab을 복구하도록 설계했습니다.

```text
InputAuthority client
-> PlayerDataManager에서 장착 무기/방패 확인
-> WeaponId / ShieldId / WeaponType 전송
-> StateAuthority가 Networked 값 저장
-> 각 peer가 Resources/Prefabs/Network에서 prefab 로드
-> WeaponCore_Network 하위에 생성
```

## Portfolio Point

이 구조는 로컬 장착 데이터와 PvP 원격 표시 사이의 간극을 ID 기반 복구로 해결한 사례입니다.
