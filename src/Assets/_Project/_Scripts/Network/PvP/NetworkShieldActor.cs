using Fusion;
using Shield_Shot.GameplayCore.Render;
using Shield_Shot.GameplayCore.Weapon.Projectile;
using Shield_Shot.GameplayCore.Weapon.Shield;
using System.Collections.Generic;
using UnityEngine;

namespace Shield_Shot.GameplayCore.Network.Pvp
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(PvpWeaponActorIdentity))]
    public sealed class NetworkShieldActor : NetworkBehaviour
    {
        [Header("Smoothing")]
        [SerializeField] private float _remoteLerpSpeed = 15f;

        [Header("Reflect Rules")]
        [Tooltip("true = 자기/상대 투사체 모두 반사 / false = 상대 투사체만 반사")]
        [SerializeField] private bool _reflectOwnProjectile = true;

        [Header("Reflect Cooldown")]
        [SerializeField] private float _reflectCooldown = 0.5f;

        [Header("Reflect VFX")]
        [SerializeField] private VFXType _reflectVfxType = VFXType.Reflect;
        [SerializeField] private float _reflectVfxDuration = 0.5f;

        [Networked] private float NetworkedOrbitAngle { get; set; }

        private ShieldBase _localShieldBase;
        private ShieldOrbitController _localOrbitCtrl;
        private ShieldOrbitController _remoteOrbitCtrl;
        private PvpWeaponActorIdentity _identity;

        private float _smoothedRemoteAngle;
        private float _lastSentAngle;
        private const float AngleSyncThreshold = 0.5f;

        private bool _localInjected;
        private bool _forceSend;

        // StateAuthority에서 중복 반사 방지
        private readonly Dictionary<NetworkId, float> _recentlyReflected = new();

        private void Awake()
        {
            _identity = GetComponent<PvpWeaponActorIdentity>();
        }

        public override void Spawned()
        {
            _smoothedRemoteAngle = NetworkedOrbitAngle;
            _lastSentAngle = NetworkedOrbitAngle;
            _forceSend = true;

            Debug.Log($"[NetworkShieldActor] Spawned. " +
                      $"InputAuth={Object.HasInputAuthority}, StateAuth={Object.HasStateAuthority}");
        }

        public void InjectShieldReferences(ShieldBase shieldBase, ShieldOrbitController orbitCtrl)
        {
            _localShieldBase = shieldBase;
            _localOrbitCtrl = orbitCtrl;
            _localInjected = true;
            _forceSend = true;
            Debug.Log($"[NetworkShieldActor] 내 방패 참조 주입 완료. InitialAngle={orbitCtrl?.CurrentAngle}");
        }

        public void InjectRemoteShieldReferences(ShieldOrbitController orbitCtrl)
        {
            _remoteOrbitCtrl = orbitCtrl;
            Debug.Log("[NetworkShieldActor] 상대 방패 참조 주입 완료.");
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasInputAuthority) return;
            if (Runner.IsResimulation) return;
            if (!_localInjected || _localOrbitCtrl == null) return;

            float current = _localOrbitCtrl.CurrentAngle;
            float delta = Mathf.Abs(Mathf.DeltaAngle(current, _lastSentAngle));

            bool shouldSend = _forceSend || delta >= AngleSyncThreshold;
            if (!shouldSend) return;

            _forceSend = false;
            _lastSentAngle = current;

            if (Object.HasStateAuthority)
                NetworkedOrbitAngle = current;
            else
                RPC_SetOrbitAngle(current);
        }

        private void LateUpdate()
        {
            if (Object == null) return;
            if (Object.HasInputAuthority) return;
            if (_remoteOrbitCtrl == null) return;

            _smoothedRemoteAngle = Mathf.MoveTowardsAngle(
                _smoothedRemoteAngle,
                NetworkedOrbitAngle,
                _remoteLerpSpeed * 360f * Time.deltaTime);

            _remoteOrbitCtrl.SetOrbitAngleDirectly(_smoothedRemoteAngle);
        }

        public void NotifyShieldHit(ProjectileBase projectile, Vector3 hitNormal,
                                    NetworkProjectileIdentity projectileIdentity)
        {
            if (projectile == null) return;
            if (!CanReflect(projectileIdentity)) return;

            if (Object.HasStateAuthority)
            {
                // StateAuthority에서 중복 반사 방지
                if (projectile.TryGetComponent(out NetworkObject netObj))
                {
                    if (IsReflectOnCooldown(netObj.Id)) return;
                    RegisterReflectCooldown(netObj.Id);
                }
                ProcessReflect(projectile, hitNormal);
            }
            else if (projectile.TryGetComponent(out NetworkObject netObj))
            {
                RPC_RequestReflect(netObj.Id, hitNormal);
            }
        }

        private void Update()
        {
            // 만료된 반사 쿨다운 정리
            var toRemove = new List<NetworkId>();
            foreach (var kvp in _recentlyReflected)
                if (Time.time - kvp.Value > _reflectCooldown)
                    toRemove.Add(kvp.Key);
            foreach (var key in toRemove)
                _recentlyReflected.Remove(key);
        }

        private bool IsReflectOnCooldown(NetworkId id)
        {
            if (!_recentlyReflected.TryGetValue(id, out float t)) return false;
            return Time.time - t < _reflectCooldown;
        }

        private void RegisterReflectCooldown(NetworkId id)
        {
            _recentlyReflected[id] = Time.time;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_SetOrbitAngle(float angle)
        {
            NetworkedOrbitAngle = angle;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestReflect(NetworkId projectileId, Vector3 hitNormal)
        {
            if (!Runner.TryFindObject(projectileId, out NetworkObject netObj)) return;
            if (!netObj.TryGetComponent(out ProjectileBase projectile)) return;

            // StateAuthority에서도 중복 방지
            if (IsReflectOnCooldown(projectileId)) return;
            RegisterReflectCooldown(projectileId);

            ProcessReflect(projectile, hitNormal);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_BroadcastReflectVFX(Vector3 pos, Vector3 normal)
        {
            Debug.Log($"[NetworkShieldActor] 반사 VFX at {pos}");
            VFXPoolManager vfxPoolManager = VFXPoolManager.EnsureInstance();
            if (_reflectVfxType == VFXType.None || vfxPoolManager == null)
            {
                return;
            }

            Quaternion rotation = normal.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(normal)
                : Quaternion.identity;

            vfxPoolManager.SpawnVFX(_reflectVfxType, pos, rotation, _reflectVfxDuration);
        }

        private void ProcessReflect(ProjectileBase projectile, Vector3 hitNormal)
        {
            if (_localShieldBase != null)
                _localShieldBase.HandleNetworkProjectileHit(projectile, hitNormal);
            else
            {
                projectile.Velocity = Vector3.Reflect(projectile.Velocity, hitNormal);
                Debug.Log($"[NetworkShieldActor] 원격 방패 반사. 방향={projectile.Velocity}");
            }
            RPC_BroadcastReflectVFX(projectile.transform.position, hitNormal);
        }

        private bool CanReflect(NetworkProjectileIdentity identity)
        {
            if (_reflectOwnProjectile) return true;
            if (identity == null || _identity == null) return true;
            return identity.Owner != _identity.Owner;
        }
    }
}
