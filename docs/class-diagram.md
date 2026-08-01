# Class Diagram

## Input System V2

```mermaid
classDiagram
    class UnityPointerInputDriver {
        -IPointerSource source
        -IPointerFrameSink sink
        +Update()
    }
    class PointerMovementThresholdFilter {
        +ShouldPass(sample)
        +Reset(pointerId)
    }
    class PointerMoveCoalescingSink {
        +Receive(sample)
        +CompleteFrame()
    }
    class CombatPointerRouter {
        -IPointerRoutePolicy policy
        +Receive(sample)
        +Reset()
    }
    class PointerGestureTracker {
        +Receive(sample)
        +Reset()
    }
    class AttackGestureInterpreter {
        +Receive(gesture)
        +Tick(deltaTime)
    }
    class DefenseGestureInterpreter {
        +Receive(gesture)
    }
    class WeaponAttackInputAdapter {
        +Receive(state)
        +ResetInput()
    }
    class ShieldDefenseInputAdapter {
        +Receive(state)
        +ResetInput()
    }

    UnityPointerInputDriver --> PointerMovementThresholdFilter
    PointerMovementThresholdFilter --> PointerMoveCoalescingSink
    PointerMoveCoalescingSink --> CombatPointerRouter
    CombatPointerRouter --> PointerGestureTracker
    PointerGestureTracker --> AttackGestureInterpreter
    PointerGestureTracker --> DefenseGestureInterpreter
    AttackGestureInterpreter --> WeaponAttackInputAdapter
    DefenseGestureInterpreter --> ShieldDefenseInputAdapter
```

각 연결은 구체 MonoBehaviour를 직접 호출하는 대신 작은 입력/출력 계약을 통과합니다. 따라서 입력 소스, 필터, 영역 정책, 공격·방어 해석기, 기존 무기 연결부를 서로 독립적으로 교체하고 측정할 수 있습니다.

## PvP Projectile / Combat Feedback

```mermaid
classDiagram
    class NetworkProjectileFireHandler {
        +Fire(firePoint, aimDirection, chargeRatio, isCritical)
        -CreateAugmentPayload()
        -SpawnProjectile(position, direction, chargeRatio, isCritical, payload)
        +GetPredictedProjectileOrigin(position, direction)
        +TryGetProjectileCollisionRadius(weaponType, radius)
    }

    class PvpProjectileAugmentPayload {
        +bool HasAnyAugment
        +PvpProjectileAugmentEntry Entries
    }

    class ProjectileBehaviorRegistry {
        +TryResolveBehavior(code, level)
    }

    class NetworkProjectileActor {
        +FixedUpdateNetwork()
        +SetElementVisual(element)
        -RPC_PlayHitVfx()
        -RPC_PlayCollisionVfx()
    }

    class PvpWeaponHitTarget {
        +CanBeHitBy(projectile)
        +ApplyHit(projectile, hitPosition)
    }

    class PvpWeaponHealth {
        +ApplyDamage(damage, hitPosition, isCritical)
        -RPC_ShowDamagePopup()
    }

    NetworkProjectileFireHandler --> PvpProjectileAugmentPayload
    NetworkProjectileFireHandler --> ProjectileBehaviorRegistry
    NetworkProjectileFireHandler --> NetworkProjectileActor
    NetworkProjectileActor --> PvpWeaponHitTarget
    PvpWeaponHitTarget --> PvpWeaponHealth
```

## ElementField / Arena

```mermaid
classDiagram
    class ElementFieldGrid {
        +CellToWorld(coord)
        +WorldToCell(position)
        +Paint(position, context, duration)
        +PaintCircle(position, context, duration, radius)
        +SetTerrainCell(coord, terrain)
    }

    class ElementFieldCellData {
        +Vector2Int Coord
        +ElementType CurrentElement
        +TerrainElementType TerrainElement
    }

    class ElementReactionResolver {
        +Resolve(current, terrain, incoming)
    }

    class ElementFieldEffectSystem {
        +Register(target)
        +Unregister(target)
        -SampleColliderBounds(target)
    }

    class ArenaTerrainPainter {
        +GenerateThemeTerrain(seed)
        +ResetArenaTerrain()
    }

    class ArenaRandomReflectWallBuilder {
        +Build(seed)
    }

    ElementFieldGrid --> ElementFieldCellData
    ElementFieldGrid --> ElementReactionResolver
    ElementFieldEffectSystem --> ElementFieldGrid
    ArenaTerrainPainter --> ElementFieldGrid
    ArenaRandomReflectWallBuilder --> ElementFieldGrid
```
