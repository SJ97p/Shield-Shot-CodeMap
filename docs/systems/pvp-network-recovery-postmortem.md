# PvP Network Recovery Postmortem

## Context

main 병합 이후 기존 네트워크 브랜치에서 작업했던 PvP 기능 일부가 빠지거나 깨진 상태가 되었습니다. 이 문서는 `05_PVP` 씬 기준으로 네트워크 플레이어, 무기, 방패, 카메라, hit, VFX를 다시 점검하고 복구한 과정입니다.

## Symptoms

- 카메라 위치와 회전이 어긋남
- 플레이어/무기 prefab이 정상 생성되지 않음
- shield는 생성되지만 무기와 위치가 어긋남
- VFX가 사라지거나 클라이언트에서 보이지 않음
- projectile hit 판정이 발생하지 않음

## Recovery Strategy

임시 Transform 기반 대응보다 기존 설계 방향을 유지했습니다.

```text
PvP spawn
-> scene Transform이 아니라 cell coordinate 기반

Weapon / Shield
-> local item instance가 아니라 ID 기반 network prefab 복구

Camera
-> actor 내부 camera가 아니라 scene-level camera + local side perspective

VFX / Popup
-> local spawn이 아니라 network RPC + manager instance 보장
```

## Portfolio Point

진행 중인 팀 프로젝트에서는 병합 후 기능이 깨질 수 있습니다. 이 사례는 임시 대응으로 구조를 우회하지 않고, 기존 설계 방향인 cell-based spawn, ID-based prefab recovery, network RPC feedback 경로를 유지하면서 기능을 복구한 과정입니다.
