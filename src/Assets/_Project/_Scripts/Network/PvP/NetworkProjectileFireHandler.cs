using Fusion;
using Shield_Shot.GameplayCore.Field;
using Shield_Shot.GameplayCore.Network.Match;
using Shield_Shot.GameplayCore.Render;
using Shield_Shot.GameplayCore.Weapon.Projectile;
using Shield_Shot.InputSystem.Data;
using UnityEngine;

namespace Shield_Shot.GameplayCore.Network.Pvp
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(PlayerStatus))]
    [RequireComponent(typeof(StatCalculator))]
    public sealed class NetworkProjectileFireHandler : NetworkBehaviour, IProjectileFireHandler, IProjectileAimPredictionProvider
    {
        [Header("Projectile")]
        [SerializeField] private NetworkObject _projectilePrefab;
        [SerializeField] private float _spawnForwardOffset = 0.35f;

        private PlayerStatus _playerStatus;
        private StatCalculator _statCalculator;
        private PvpWeaponActorIdentity _weaponIdentity;
        private PvpProjectileAugmentSnapshotProvider _augmentSnapshotProvider;
        private ProjectileBehaviorRegistry _behaviorRegistry;
        private PvpMatchStateController _matchStateController;

        private void Awake()
        {
            _playerStatus = GetComponent<PlayerStatus>();
            _statCalculator = GetComponent<StatCalculator>();
            _weaponIdentity = GetComponent<PvpWeaponActorIdentity>();
            _augmentSnapshotProvider = GetComponent<PvpProjectileAugmentSnapshotProvider>();
        }

        public void Fire(Transform firePoint, Vector3 aimDirection, float chargeRatio, bool isCritical)
        {
            if (Object == null || !Object.HasInputAuthority)
            {
                return;
            }

            if (!CanFireInCurrentMatchState())
            {
                Debug.Log("[NetworkProjectileFireHandler] Fire blocked by match state.");
                return;
            }

            if (firePoint == null)
            {
                Debug.LogWarning("[NetworkProjectileFireHandler] FirePoint is missing.");
                return;
            }

            Vector3 worldDirection = ToWorldXZ(aimDirection);
            if (worldDirection.sqrMagnitude <= 0.0001f)
            {
                worldDirection = firePoint.forward;
                worldDirection.y = 0f;
                worldDirection = worldDirection.sqrMagnitude > 0.0001f
                    ? worldDirection.normalized
                    : Vector3.forward;
            }

            PvpProjectileAugmentPayload payload = CreateAugmentPayload();

            Debug.Log(payload.HasAnyAugment
                ? "[NetworkProjectileFireHandler] Fire with augment payload."
                : "[NetworkProjectileFireHandler] Fire with empty augment payload.");

            isCritical = (chargeRatio >= 0.99f);

            if (Object.HasStateAuthority)
            {
                SpawnProjectile(GetSpawnPosition(firePoint.position, worldDirection), worldDirection, chargeRatio, isCritical, payload);
                return;
            }


            RPC_RequestFire(GetSpawnPosition(firePoint.position, worldDirection), worldDirection, chargeRatio, isCritical, payload);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_RequestFire(
    Vector3 position,
    Vector3 worldDirection,
    float chargeRatio,
    bool isCritical,
    PvpProjectileAugmentPayload payload)
        {
            if (!CanFireInCurrentMatchState())
            {
                Debug.Log("[NetworkProjectileFireHandler] RPC fire blocked by match state.");
                return;
            }

            SpawnProjectile(position, worldDirection, chargeRatio, isCritical, payload);
        }

        private void SpawnProjectile(
    Vector3 position,
    Vector3 worldDirection,
    float chargeRatio,
    bool isCritical,
    PvpProjectileAugmentPayload payload)
        {
            if (_projectilePrefab == null)
            {
                Debug.LogWarning("[NetworkProjectileFireHandler] Projectile prefab is missing.");
                return;
            }

            if (_playerStatus == null || _statCalculator == null)
            {
                Debug.LogWarning("[NetworkProjectileFireHandler] Required references are missing.");
                return;
            }

            Quaternion rotation = Quaternion.LookRotation(worldDirection, Vector3.up);
            ProjectileStats finalStats = _statCalculator.Calculate(chargeRatio, isCritical);
            Debug.Log($"[NetworkProjectileFireHandler] Spawn network projectile. Authority: {Object.InputAuthority}, Position: {position}, Direction: {worldDirection}, Speed: {finalStats.Speed}, Critical: {isCritical}");
            LogFireAlignmentDiagnostics(position, worldDirection);

            Runner.Spawn(
                _projectilePrefab,
                position,
                rotation,
                Object.InputAuthority,
                (_, spawnedObject) =>
                {
                    if (!spawnedObject.TryGetComponent(out ProjectileBase projectile))
                    {
                        Debug.LogWarning("[NetworkProjectileFireHandler] Spawned object does not have ProjectileBase.");
                        return;
                    }
                    if (spawnedObject.TryGetComponent(out NetworkProjectileIdentity projectileIdentity))
                    {
                        PlayerRef owner = _weaponIdentity != null ? _weaponIdentity.Owner : Object.InputAuthority;
                        PlayerSide ownerSide = _weaponIdentity != null ? _weaponIdentity.Side : PlayerSide.Bottom;

                        projectileIdentity.Initialize(owner, ownerSide);
                    }
                    else
                    {
                        Debug.LogWarning("[NetworkProjectileFireHandler] Spawned object does not have NetworkProjectileIdentity.");
                    }

                    projectile.IsCritical = isCritical;
                    projectile.ChargeRatio = chargeRatio;
                    projectile.ProjectileDamage = finalStats.Damage;
                    projectile.BaseSpeed = finalStats.Speed;
                    projectile.Velocity = worldDirection * finalStats.Speed;

                    ApplyProjectileBehaviors(projectile, payload);

                    if (spawnedObject.TryGetComponent(out NetworkProjectileActor projectileActor))
                    {
                        projectileActor.SetElementVisual(ResolveProjectileVisualElement(payload));
                    }
                });
        }

        private Vector3 GetSpawnPosition(Vector3 firePointPosition, Vector3 worldDirection)
        {
            if (worldDirection.sqrMagnitude <= 0.0001f || _spawnForwardOffset <= 0f)
            {
                return firePointPosition;
            }

            return firePointPosition + worldDirection.normalized * _spawnForwardOffset;
        }

        private void LogFireAlignmentDiagnostics(Vector3 position, Vector3 worldDirection)
        {
            if (worldDirection.sqrMagnitude <= 0.0001f)
            {
                Debug.LogWarning("[NetworkProjectileFireHandler] Fire alignment skipped. World direction is zero.");
                return;
            }

            Vector3 normalizedDirection = worldDirection.normalized;
            PvpWeaponActorIdentity[] actors = FindObjectsByType<PvpWeaponActorIdentity>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = 0; i < actors.Length; i++)
            {
                PvpWeaponActorIdentity actor = actors[i];
                if (actor == null || actor == _weaponIdentity)
                {
                    continue;
                }

                Vector3 toActor = actor.transform.position - position;
                toActor.y = 0f;
                float forwardDistance = Vector3.Dot(toActor, normalizedDirection);
                Vector3 closestPoint = position + normalizedDirection * forwardDistance;
                closestPoint.y = actor.transform.position.y;
                float lateralDistance = Vector3.Distance(
                    new Vector3(actor.transform.position.x, 0f, actor.transform.position.z),
                    new Vector3(closestPoint.x, 0f, closestPoint.z));

                Debug.Log(
                    $"[NetworkProjectileFireHandler] Fire alignment. Shooter: {_weaponIdentity?.Owner}/{_weaponIdentity?.Side}, Target: {actor.Owner}/{actor.Side}, FirePosition: {position}, FireDirection: {normalizedDirection}, TargetPosition: {actor.transform.position}, ForwardDistance: {forwardDistance}, LateralDistance: {lateralDistance}");
            }
        }

        public Vector3 GetPredictedProjectileOrigin(Vector3 firePointPosition, Vector3 worldDirection)
        {
            return GetSpawnPosition(firePointPosition, worldDirection);
        }

        public bool TryGetProjectileCollisionRadius(WeaponType weaponType, out float radius)
        {
            radius = 0f;
            if (_projectilePrefab == null)
            {
                return false;
            }

            ProjectileBase projectile = _projectilePrefab.GetComponent<ProjectileBase>();
            if (projectile == null)
            {
                return false;
            }

            radius = projectile.ProjectileRadius;
            return radius > 0f;
        }

        private void ApplyProjectileBehaviors(ProjectileBase projectile, PvpProjectileAugmentPayload payload)
        {
            if (projectile == null || !payload.HasAnyAugment)
            {
                return;
            }

            int appliedCount = 0;
            if (_behaviorRegistry == null)
            {
                _behaviorRegistry = FindFirstObjectByType<ProjectileBehaviorRegistry>();
            }

            payload.ForEach(entry =>
            {
                if (_behaviorRegistry != null &&
                    _behaviorRegistry.TryGet(entry.BehaviorCode, out ProjectileBehaviorSO behaviorSO))
                {
                    behaviorSO.InjectBehavior(projectile, entry.Level);
                    appliedCount++;
                    Debug.Log($"[NetworkProjectileFireHandler] Applied behavior from registry: {behaviorSO.BehaviorName}({entry.BehaviorCode}) Lv.{entry.Level}");
                    return;
                }

                if (ApplyEntryDirectly(projectile, entry))
                {
                    appliedCount++;
                }
            });

            Debug.Log($"[NetworkProjectileFireHandler] Applied projectile payload. Count: {appliedCount}");
        }

        private static int ApplyPayloadDirectly(ProjectileBase projectile, PvpProjectileAugmentPayload payload)
        {
            int appliedCount = 0;

            payload.ForEach(entry =>
            {
                if (ApplyEntryDirectly(projectile, entry))
                {
                    appliedCount++;
                }
            });

            return appliedCount;
        }

        private static bool ApplyEntryDirectly(ProjectileBase projectile, PvpProjectileAugmentEntry entry)
        {
            switch (entry.BehaviorCode)
            {
                case PvpProjectileBehaviorCode.StandardReflect:
                    projectile.AddCollisionBehavior(new StandardReflectCollisionBehavior(entry.Level), 40);
                    Debug.Log($"[NetworkProjectileFireHandler] Fallback applied StandardReflect Lv.{entry.Level}");
                    return true;

                case PvpProjectileBehaviorCode.RandomReflect:
                    projectile.AddCollisionBehavior(new RandomReflectCollisionBehavior(entry.Level), 50);
                    Debug.Log($"[NetworkProjectileFireHandler] Fallback applied RandomReflect Lv.{entry.Level}");
                    return true;

                case PvpProjectileBehaviorCode.Split:
                    projectile.AddCollisionBehavior(new SplitCollisionBehavior(entry.Level), 10);
                    Debug.Log($"[NetworkProjectileFireHandler] Fallback applied Split Lv.{entry.Level}");
                    return true;

                case PvpProjectileBehaviorCode.Pierce:
                    projectile.AddHitBehavior(new PierceHitBehavior(entry.Level), 90);
                    Debug.Log($"[NetworkProjectileFireHandler] Fallback applied Pierce Lv.{entry.Level}");
                    return true;

                case PvpProjectileBehaviorCode.Poison:
                    projectile.AddHitBehavior(
                        new PoisonHitBehavior(
                            duration: 4f,
                            tickInterval: 0.5f,
                            damagePerTick: projectile.ProjectileDamage * (0.8f + 0.2f * Mathf.Max(0, entry.Level - 1)) / 8f,
                            showDamagePopup: true,
                            tickVfxType: VFXType.Hit,
                            vfxAutoReleaseTime: 1.5f
                        ),
                        100
                    );
                    Debug.Log($"[NetworkProjectileFireHandler] Fallback applied Poison Lv.{entry.Level}");
                    return true;

                case PvpProjectileBehaviorCode.ElementTrailFire:
                    projectile.AddMovementBehavior(
                        new FieldPaintMovementBehavior(
                            ElementType.Fire,
                            entry.Level,
                            2f,
                            0.6f + 0.1f * Mathf.Max(0, entry.Level - 1)
                        ),
                        100
                    );
                    Debug.Log($"[NetworkProjectileFireHandler] Fallback applied ElementTrailFire Lv.{entry.Level}");
                    return true;

                case PvpProjectileBehaviorCode.ElementTrailWind:
                    projectile.AddMovementBehavior(
                        new FieldPaintMovementBehavior(
                            ElementType.Wind,
                            entry.Level,
                            2f + 0.2f * Mathf.Max(0, entry.Level - 1),
                            0.6f + 0.1f * Mathf.Max(0, entry.Level - 1)
                        ),
                        100
                    );
                    WindFieldProjectileBoostBehavior windTrailBoostBehavior = new WindFieldProjectileBoostBehavior(
                        entry.Level,
                        baseBoostDuration: 0.6f,
                        durationPerLevel: 0.12f,
                        baseSpeedMultiplier: 1.25f,
                        speedMultiplierPerLevel: 0.05f
                    );
                    projectile.AddMovementBehavior(windTrailBoostBehavior, 100);
                    projectile.AddHitBehavior(
                        new WindBoostKnockbackHitBehavior(
                            windTrailBoostBehavior,
                            entry.Level,
                            baseKnockbackSpeed: 8f,
                            knockbackSpeedPerLevel: 1.5f,
                            knockbackDuration: 0.15f
                        ),
                        100
                    );
                    Debug.Log($"[NetworkProjectileFireHandler] Fallback applied ElementTrailWind Lv.{entry.Level}");
                    return true;

                case PvpProjectileBehaviorCode.ElementTrailIce:
                    projectile.AddMovementBehavior(
                        new FieldPaintMovementBehavior(
                            ElementType.Ice,
                            entry.Level,
                            2f + 0.2f * Mathf.Max(0, entry.Level - 1),
                            0.6f + 0.1f * Mathf.Max(0, entry.Level - 1)
                        ),
                        100
                    );
                    projectile.AddHitBehavior(
                        new FreezeHitBehavior(1.5f + 0.2f * Mathf.Max(0, entry.Level - 1)),
                        100
                    );
                    Debug.Log($"[NetworkProjectileFireHandler] Fallback applied ElementTrailIce Lv.{entry.Level}");
                    return true;

                case PvpProjectileBehaviorCode.FreezeHit:
                    projectile.AddHitBehavior(
                        new FreezeHitBehavior(1.5f + 0.2f * Mathf.Max(0, entry.Level - 1)),
                        100
                    );
                    Debug.Log($"[NetworkProjectileFireHandler] Fallback applied FreezeHit Lv.{entry.Level}");
                    return true;

                case PvpProjectileBehaviorCode.WindFieldBoost:
                    WindFieldProjectileBoostBehavior boostBehavior = new WindFieldProjectileBoostBehavior(
                        entry.Level,
                        baseBoostDuration: 0.6f,
                        durationPerLevel: 0.12f,
                        baseSpeedMultiplier: 1.25f,
                        speedMultiplierPerLevel: 0.05f
                    );
                    projectile.AddMovementBehavior(boostBehavior, 100);
                    projectile.AddHitBehavior(
                        new WindBoostKnockbackHitBehavior(
                            boostBehavior,
                            entry.Level,
                            baseKnockbackSpeed: 8f,
                            knockbackSpeedPerLevel: 1.5f,
                            knockbackDuration: 0.15f
                        ),
                        100
                    );
                    Debug.Log($"[NetworkProjectileFireHandler] Fallback applied WindFieldBoost Lv.{entry.Level}");
                    return true;

                default:
                    Debug.LogWarning($"[NetworkProjectileFireHandler] No direct fallback for behavior code: {entry.BehaviorCode}");
                    return false;
            }
        }

        private static ElementType ResolveProjectileVisualElement(PvpProjectileAugmentPayload payload)
        {
            ElementType visualElement = ElementType.None;

            payload.ForEach(entry =>
            {
                if (entry.BehaviorCode == PvpProjectileBehaviorCode.ElementTrailIce ||
                    entry.BehaviorCode == PvpProjectileBehaviorCode.FreezeHit)
                {
                    visualElement = ElementType.Ice;
                }
            });

            return visualElement;
        }

        private PvpProjectileAugmentPayload CreateAugmentPayload()
        {
            if (_augmentSnapshotProvider == null)
            {
                _augmentSnapshotProvider = GetComponent<PvpProjectileAugmentSnapshotProvider>();
            }

            if (_augmentSnapshotProvider != null)
            {
                PvpProjectileAugmentPayload payload = _augmentSnapshotProvider.CreatePayload();
                if (payload.HasAnyAugment)
                {
                    return payload;
                }
            }

            if (LocalPlayerStatusContext.TryGet(out PlayerStatus localPlayerStatus))
            {
                PvpProjectileAugmentPayload payload = PvpProjectileAugmentSnapshotProvider.CreatePayloadFrom(localPlayerStatus);
                if (payload.HasAnyAugment)
                {
                    Debug.Log("[NetworkProjectileFireHandler] Created augment payload from LocalPlayerStatusContext.");
                    return payload;
                }
            }

            if (_playerStatus != null)
            {
                PvpProjectileAugmentPayload payload = PvpProjectileAugmentSnapshotProvider.CreatePayloadFrom(_playerStatus);
                if (payload.HasAnyAugment)
                {
                    Debug.Log("[NetworkProjectileFireHandler] Created augment payload from local PlayerStatus component.");
                    return payload;
                }
            }

            Debug.LogWarning("[NetworkProjectileFireHandler] Could not create augment payload. No active projectile behaviors found.");
            return PvpProjectileAugmentPayload.Empty;
        }

        private bool CanFireInCurrentMatchState()
        {
            if (_matchStateController == null)
            {
                _matchStateController = FindFirstObjectByType<PvpMatchStateController>();
            }

            return _matchStateController == null ||
                   _matchStateController.CurrentState == PvpMatchState.Fighting;
        }

        private static Vector3 ToWorldXZ(Vector3 direction)
        {
            Vector3 worldDirection = Mathf.Approximately(direction.z, 0f)
                ? new Vector3(direction.x, 0f, direction.y)
                : new Vector3(direction.x, 0f, direction.z);

            return worldDirection.sqrMagnitude > 0.0001f
                ? worldDirection.normalized
                : Vector3.zero;
        }
        public override void Spawned()
        {
            _behaviorRegistry = FindFirstObjectByType<ProjectileBehaviorRegistry>();
            _matchStateController = FindFirstObjectByType<PvpMatchStateController>();
        }
    }
}
