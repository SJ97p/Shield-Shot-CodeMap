using Fusion;
using Shield_Shot.GameplayCore.Weapon.Projectile;
using UnityEngine;

namespace Shield_Shot.GameplayCore.Network.Pvp
{
    [RequireComponent(typeof(PvpWeaponActorIdentity))]
    public sealed class PvpWeaponHitTarget : NetworkBehaviour
    {
        [Header("Damage Rules")]
        [Tooltip("true = 자기 투사체도 데미지 / false = 상대 투사체만 데미지")]
        [SerializeField] private bool _allowSelfDamage = true;

        [Tooltip("발사 후 이 시간(초) 동안은 자기 투사체에 무적 (자해 방지)")]
        [SerializeField] private float _selfDamageGracePeriod = 0.5f;

        [SerializeField] private float _testDamage = 10f;

        private PvpWeaponHealth _health;
        private PvpWeaponActorIdentity _identity;

        private void Awake()
        {
            _health = GetComponent<PvpWeaponHealth>();
            _identity = GetComponent<PvpWeaponActorIdentity>();
        }

        public bool CanBeHitBy(NetworkProjectileIdentity projectileIdentity)
        {
            if (projectileIdentity == null || _identity == null)
                return false;

            bool isSelf = projectileIdentity.Owner == _identity.Owner;

            if (!isSelf) return true; // 상대 투사체는 항상 데미지

            if (!_allowSelfDamage) return false; // 자해 비활성화

            // 발사 직후 유예 시간 동안 자기 투사체 무적
            float elapsed = Time.time - projectileIdentity.SpawnTime;
            if (elapsed < _selfDamageGracePeriod) return false;

            return true;
        }

        public void NotifyHit(NetworkProjectileIdentity projectileIdentity, ProjectileBase projectile, Collider hitCollider)
        {
            if (!CanBeHitBy(projectileIdentity))
                return;

            float damage = projectile != null && projectile.ProjectileDamage > 0f
                ? projectile.ProjectileDamage
                : _testDamage;
            bool isCritical = projectile != null && projectile.IsCritical;
            Vector3 hitPosition = hitCollider != null
                ? hitCollider.bounds.center
                : transform.position;

            Debug.Log($"[PvpWeaponHitTarget] Hit. Target: {_identity.Owner}/{_identity.Side}, " +
                      $"ProjectileOwner: {projectileIdentity?.Owner}/{projectileIdentity?.OwnerSide}, " +
                      $"Damage: {damage}, Critical: {isCritical}");

            _health?.ApplyDamage(damage, projectileIdentity, hitPosition, isCritical);
        }
    }
}
