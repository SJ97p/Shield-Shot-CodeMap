using Fusion;
using Shield_Shot.GameplayCore.Weapon.Projectile;
using Shield_Shot.GameplayCore.Weapon.Shield;
using System.Collections.Generic;
using UnityEngine;

namespace Shield_Shot.GameplayCore.Network.Pvp
{
    [RequireComponent(typeof(Collider))]
    public sealed class NetworkShieldColliderDetector : MonoBehaviour, IShieldColliderDetector
    {
        [Header("Layer Settings")]
        [SerializeField] private LayerMask projectileLayerMask;

        [Header("Defense Angle Settings")]
        [SerializeField, Range(10f, 180f)] private float defenseAngle = 120f;

        [Header("Reflect Rules")]
        [Tooltip("true = 자기/상대 투사체 모두 반사 / false = 상대 투사체만 반사")]
        [SerializeField] private bool _reflectOwnProjectile = true;

        [Header("Reflect Cooldown")]
        [Tooltip("반사 후 같은 투사체를 무시할 시간 (초)")]
        [SerializeField] private float reflectCooldown = 0.5f;

        public event System.Action<ProjectileBase, Vector3> OnProjectileDetected;

        private NetworkShieldActor _networkShieldActor;
        private PvpWeaponActorIdentity _weaponActorIdentity;

        // 최근 반사한 투사체와 시간 기록
        private readonly Dictionary<ProjectileBase, float> _recentlyReflected = new();

        private void Awake()
        {
            IncludeProjectileLayer("Projectile");
            IncludeProjectileLayer("PvpProjectile");
            IncludeProjectileLayer("MonsterProjectile");
        }

        public void InjectNetworkShieldActor(NetworkShieldActor actor)
        {
            _networkShieldActor = actor;
            _weaponActorIdentity = actor != null ? actor.GetComponent<PvpWeaponActorIdentity>() : null;
        }

        private void OnTriggerEnter(Collider other)
        {
            ProjectileBase projectile = other.GetComponent<ProjectileBase>()
                ?? other.GetComponentInParent<ProjectileBase>()
                ?? other.GetComponentInChildren<ProjectileBase>();
            if (projectile == null) return;

            int mask = 1 << other.gameObject.layer;
            bool layerAllowed = (projectileLayerMask.value & mask) != 0;
            NetworkProjectileIdentity networkProjectile = other.GetComponent<NetworkProjectileIdentity>()
                ?? other.GetComponentInParent<NetworkProjectileIdentity>();
            if (!layerAllowed && networkProjectile == null) return;

            // 반사 직후 재충돌 방지
            if (IsOnCooldown(projectile)) return;

            if (!IsHitFromFront(projectile)) return;

            NetworkProjectileIdentity identity =
                other.GetComponent<NetworkProjectileIdentity>()
                ?? other.GetComponentInParent<NetworkProjectileIdentity>();

            if (!CanReflect(identity)) return;

            Vector3 hitNormal = GetHitNormal(other, projectile);

            // 쿨다운 등록
            _recentlyReflected[projectile] = Time.time;

            if (_networkShieldActor != null)
                _networkShieldActor.NotifyShieldHit(projectile, hitNormal, identity);
            else
                OnProjectileDetected?.Invoke(projectile, hitNormal);
        }

        private void Update()
        {
            // 만료된 쿨다운 정리
            var toRemove = new List<ProjectileBase>();
            foreach (var kvp in _recentlyReflected)
            {
                if (kvp.Key == null || Time.time - kvp.Value > reflectCooldown)
                    toRemove.Add(kvp.Key);
            }
            foreach (var key in toRemove)
                _recentlyReflected.Remove(key);
        }

        private bool IsOnCooldown(ProjectileBase projectile)
        {
            if (!_recentlyReflected.TryGetValue(projectile, out float lastTime)) return false;
            return Time.time - lastTime < reflectCooldown;
        }

        private bool CanReflect(NetworkProjectileIdentity identity)
        {
            if (_reflectOwnProjectile) return true;
            if (identity == null || _weaponActorIdentity == null) return true;
            return identity.Owner != _weaponActorIdentity.Owner;
        }

        private bool IsHitFromFront(ProjectileBase projectile)
        {
            Vector3 incoming = projectile.Velocity.sqrMagnitude > 0.001f
                ? projectile.Velocity.normalized
                : projectile.transform.forward;
            Vector3 shieldFwd = -transform.forward;

            if (Vector3.Dot(incoming, shieldFwd) >= 0f) return false;

            Vector3 toProj = (projectile.transform.position - transform.position).normalized;
            toProj.y = 0f;
            shieldFwd.y = 0f;
            return Vector3.Angle(shieldFwd.normalized, toProj.normalized) <= defenseAngle * 0.5f;
        }

        private Vector3 GetHitNormal(Collider other, ProjectileBase projectile)
        {
            Vector3 dir = projectile.Velocity.sqrMagnitude > 0.001f
                ? projectile.Velocity.normalized
                : projectile.transform.forward;
            Vector3 origin = projectile.transform.position - dir * 0.2f;

            if (Physics.SphereCast(origin, 0.1f, dir, out RaycastHit hit, 2f, 1 << gameObject.layer))
            {
                Vector3 n = hit.normal;
                return Vector3.Dot(n, -transform.forward) < 0f ? -n : n;
            }
            return -transform.forward;
        }

        private void IncludeProjectileLayer(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
            {
                return;
            }

            projectileLayerMask |= 1 << layer;
        }
    }
}
