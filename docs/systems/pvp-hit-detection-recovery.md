# PvP Hit Detection Recovery

## Problem

PvP에서 화살이 상대 프리팹에 닿아도 hit 판정이 발생하지 않고 그냥 지나가는 문제가 있었습니다. 처음에는 damage 처리나 `PvpWeaponHitTarget.CanBeHitBy()` 문제처럼 보였지만, 로그상 target candidate 단계까지 도달하지 못하고 있었습니다.

## Diagnosis

원인을 분리하기 위해 projectile collision 단계에 진단 로그를 추가했습니다.

```text
No sphere cast hit
Sphere cast environment hit
Sphere cast target hit candidate
ExecuteHit candidate
ExecuteHit rejected by PvpWeaponHitTarget.CanBeHitBy
```

## Cause

최종 원인은 damage 로직이 아니라 hitbox 위치와 layer 정규화 문제였습니다.

```text
Actor position: (-2.54, 2.08, 8.86)
Player collider bounds center: (5.00, 0.00, 1.00)
```

`WeaponCore_Network.prefab` 내부 `Player` hitbox prefab override에 `localPosition = (5, 0, 1)`이 남아 있었고, 기존 런타임 layer 보정은 모든 자식 collider를 `PvpWeapon`으로 바꾸면서 projectile pool collider까지 target layer가 되는 부작용을 만들 수 있었습니다.

## Solution

- 모든 자식 collider를 무조건 `PvpWeapon`으로 바꾸는 방식을 중단
- 이름 또는 tag가 `Player`인 hitbox만 PvP hit target으로 정규화
- `Player` hitbox transform을 루트 기준으로 보정
- 해당 hitbox 아래 collider만 `PvpWeapon` layer로 설정

## Portfolio Point

이 사례는 네트워크 전투 디버깅에서 증상을 바로 damage 로직으로 단정하지 않고, collision candidate, hit filter, damage application 단계를 나눠 원인을 좁힌 과정입니다.
