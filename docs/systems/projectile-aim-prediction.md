# Projectile Aim Prediction

## Problem

Aim line은 플레이어가 발사 전에 projectile 경로를 예측할 수 있게 해주는 중요한 UX입니다. 하지만 PvP 환경에서 aim line과 실제 network projectile 경로가 미세하게 어긋나는 문제가 있었습니다.

원인은 하나가 아니었습니다.

- 실제 network projectile은 `firePoint.position`이 아니라 `firePoint.position + direction * spawnForwardOffset`에서 생성됨
- aim line은 로컬 projectile radius를 사용하고, 실제 network projectile은 network prefab radius를 사용함
- `PvpWall` layer가 aim line raycast/spherecast 대상에 포함되지 않는 prefab이 존재함
- RandomReflect는 의도적으로 예측 불가능한 반사를 수행함

## Solution

`IProjectileAimPredictionProvider`를 통해 현재 fire handler가 실제 발사 기준을 aim line에 제공하도록 했습니다.

```text
AimLineRenderer
-> current WeaponBase.ProjectileFireHandler
-> IProjectileAimPredictionProvider
-> GetPredictedProjectileOrigin
-> TryGetProjectileCollisionRadius
-> SphereCast prediction
```

## Portfolio Point

이 문제는 단순한 line renderer 보정이 아니라, UX 예측과 실제 전투 판정이 같은 기준을 공유해야 한다는 설계 문제였습니다.
