using Fusion;
using Shield_Shot.GameplayCore.Field;
using Shield_Shot.GameplayCore.Render;
using Shield_Shot.GameplayCore.Weapon.Projectile;
using UnityEngine;

namespace Shield_Shot.GameplayCore.Network.Pvp
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(ProjectileBase))]
    public sealed class NetworkProjectileActor : NetworkBehaviour
    {
        [Header("Split")]
        [SerializeField] private NetworkObject _projectilePrefab;

        [Header("Projectile Visual")]
        [SerializeField] private Material _iceProjectileMaterial;
        [SerializeField] private bool _replaceAllProjectileMaterialSlots = true;

        [Networked] private int NetworkedVisualElement { get; set; }

        private const VFXType CollisionVfxType = VFXType.Reflect;
        private const float CollisionVfxDuration = 0.5f;
        private const VFXType HitVfxType = VFXType.Hit;
        private const float HitVfxDuration = 1.5f;

        private ProjectileBase _projectile;
        private bool _despawnRequested;
        private bool _isBound;
        private int _appliedVisualElement = -1;

        private void Awake()
        {
            _projectile = GetComponent<ProjectileBase>();
        }

        public override void Spawned()
        {
            if (_projectile == null)
            {
                _projectile = GetComponent<ProjectileBase>();
            }

            BindProjectileLifecycle();
            ApplyNetworkedVisualElement();
        }

        public override void Render()
        {
            ApplyNetworkedVisualElement();
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority || _projectile == null || _despawnRequested)
            {
                return;
            }

            _projectile.Simulate(Runner.DeltaTime);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (_projectile == null)
            {
                return;
            }

            _projectile.ReleaseRequested -= OnProjectileReleaseRequested;
            _projectile.CollisionExecuted -= OnProjectileCollisionExecuted;
            _projectile.HitExecuted -= OnProjectileHitExecuted;
            _projectile.SplitRequested -= OnProjectileSplitRequested;
            _isBound = false;
            _projectile.SetExternalReleaseEnabled(false);
            _projectile.SetUpdateSimulationEnabled(true);
            _projectile.RestoreMaterialOverride();
            _appliedVisualElement = -1;
        }

        public void SetElementVisual(ElementType element)
        {
            int elementValue = (int)element;

            if (Object != null && Object.HasStateAuthority)
            {
                NetworkedVisualElement = elementValue;
            }

            ApplyVisualElement(elementValue);
        }

        public void BindProjectileLifecycle()
        {
            if (_isBound)
            {
                return;
            }

            if (_projectile == null)
            {
                _projectile = GetComponent<ProjectileBase>();
            }

            if (_projectile == null)
            {
                return;
            }

            _projectile.SetUpdateSimulationEnabled(false);
            _projectile.SetExternalReleaseEnabled(true);
            _projectile.ReleaseRequested += OnProjectileReleaseRequested;
            _projectile.CollisionExecuted += OnProjectileCollisionExecuted;
            _projectile.HitExecuted += OnProjectileHitExecuted;
            _projectile.SplitRequested += OnProjectileSplitRequested;
            _isBound = true;
        }

        private void OnProjectileCollisionExecuted(ProjectileBase projectile, RaycastHit hitInfo)
        {
            if (!Object.HasStateAuthority)
            {
                return;
            }

            RPC_PlayCollisionVfx(hitInfo.point, hitInfo.normal);
        }

        private void OnProjectileHitExecuted(ProjectileBase projectile, Collider targetInfo)
        {
            if (!Object.HasStateAuthority || projectile == null)
            {
                return;
            }

            Vector3 position = projectile.transform.position;
            if (targetInfo != null)
            {
                position = targetInfo.ClosestPoint(projectile.transform.position);
            }

            RPC_PlayHitVfx(position, projectile.transform.rotation);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlayCollisionVfx(Vector3 position, Vector3 normal)
        {
            VFXPoolManager vfxPoolManager = VFXPoolManager.EnsureInstance();
            if (CollisionVfxType == VFXType.None || vfxPoolManager == null)
            {
                return;
            }

            Quaternion rotation = normal.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(normal)
                : Quaternion.identity;

            vfxPoolManager.SpawnVFX(CollisionVfxType, position, rotation, CollisionVfxDuration);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlayHitVfx(Vector3 position, Quaternion rotation)
        {
            VFXPoolManager vfxPoolManager = VFXPoolManager.EnsureInstance();
            if (HitVfxType == VFXType.None || vfxPoolManager == null)
            {
                return;
            }

            vfxPoolManager.SpawnVFX(HitVfxType, position, rotation, HitVfxDuration);
        }

        private void OnProjectileReleaseRequested(ProjectileBase projectile)
        {
            if (!Object.HasStateAuthority || _despawnRequested)
            {
                return;
            }

            _despawnRequested = true;
            Runner.Despawn(Object);
        }

        private bool OnProjectileSplitRequested(ProjectileBase projectile, RaycastHit hitInfo)
        {
            if (!Object.HasStateAuthority || _despawnRequested || projectile == null)
            {
                return false;
            }

            NetworkObject prefab = _projectilePrefab != null ? _projectilePrefab : Object;
            Vector3 baseDirection = projectile.Velocity.sqrMagnitude > 0.0001f
                ? projectile.Velocity.normalized
                : projectile.transform.forward;
            float speed = projectile.Velocity.magnitude > 0.0001f
                ? projectile.Velocity.magnitude
                : projectile.BaseSpeed;
            Vector3 spawnPosition = SplitCollisionBehavior.GetChildSpawnPosition(hitInfo, projectile.ProjectileRadius);

            for (int i = 0; i < SplitCollisionBehavior.ChildCount; i++)
            {
                Vector3 childIncomingDirection = SplitCollisionBehavior.GetChildIncomingDirection(baseDirection, i);
                Quaternion rotation = Quaternion.LookRotation(childIncomingDirection, Vector3.up);

                Runner.Spawn(
                    prefab,
                    spawnPosition,
                    rotation,
                    Object.InputAuthority,
                    (_, spawnedObject) =>
                    {
                        if (!spawnedObject.TryGetComponent(out ProjectileBase childProjectile))
                        {
                            return;
                        }

                        childProjectile.ResetProjectileState();
                        childProjectile.SetUpdateSimulationEnabled(false);
                        childProjectile.SetExternalReleaseEnabled(true);
                        childProjectile.BaseSpeed = projectile.BaseSpeed;
                        childProjectile.ChargeRatio = projectile.ChargeRatio;
                        childProjectile.Velocity = childIncomingDirection * speed;
                        projectile.CopyBehaviorsTo(childProjectile, typeof(SplitCollisionBehavior));

                        if (spawnedObject.TryGetComponent(out NetworkProjectileActor childActor))
                        {
                            childActor.SetElementVisual((ElementType)NetworkedVisualElement);
                            childActor.BindProjectileLifecycle();
                        }

                        if (spawnedObject.TryGetComponent(out NetworkProjectileIdentity childIdentity) &&
                            Object.TryGetComponent(out NetworkProjectileIdentity parentIdentity))
                        {
                            childIdentity.Initialize(parentIdentity.Owner, parentIdentity.OwnerSide);
                        }

                        childProjectile.ExecuteCollision(hitInfo);
                    });
            }

            return true;
        }

        private void ApplyNetworkedVisualElement()
        {
            ApplyVisualElement(NetworkedVisualElement);
        }

        private void ApplyVisualElement(int elementValue)
        {
            if (_projectile == null)
            {
                _projectile = GetComponent<ProjectileBase>();
            }

            if (_projectile == null || _appliedVisualElement == elementValue)
            {
                return;
            }

            _appliedVisualElement = elementValue;

            if ((ElementType)elementValue == ElementType.Ice && _iceProjectileMaterial != null)
            {
                _projectile.ApplyMaterialOverride(_iceProjectileMaterial, _replaceAllProjectileMaterialSlots);
                return;
            }

            _projectile.RestoreMaterialOverride();
        }
    }
}
