using Shield_Shot.GameplayCore.Render;
using Shield_Shot.GameplayCore.Weapon.Aim;
using UnityEngine;

namespace Shield_Shot.GameplayCore.Weapon.Projectile
{
    public class ProjectileShooter : MonoBehaviour, IProjectileFireHandler, IProjectileAimPredictionProvider
    {
        [Header("References")]
        [SerializeField] private Transform _firePoint;
        [SerializeField] private InputSystem.Data.WeaponType _defaultWeaponType;
        [SerializeField] private bool _allowLocalFire = true;

        [Header("Full Charge Bonus")]
        [Tooltip("풀차징(차징 99% 이상) 발사 시 추가 관통 횟수 (0이면 비활성)")]
        [SerializeField] private int _fullChargePierceBonus = 1;

        [Tooltip("풀차징 판정 기준 (0~1)")]
        [SerializeField, Range(0.9f, 1f)] private float _fullChargeThreshold = 0.99f;

        [Header("Sniper Settings")]
        [Tooltip("스나이퍼가 기본으로 보유하는 관통 횟수 (항상 적용, 차징 조건 없음)")]
        [SerializeField, Min(0)] private int _sniperBasePierceCount = 1;

        private PlayerStatus _playerStatus;
        private StatCalculator _statCalculator;

        public StatCalculator StatCalculator => _statCalculator;

        public void Fire(Transform firePoint, Vector3 aimDirection, float chargeRatio, bool isCritical)
        {
            Fire(firePoint, aimDirection, chargeRatio, isCritical, _defaultWeaponType);
        }

        public void Fire(Transform firePoint, Vector3 aimDirection, float chargeRatio, bool isCritical, InputSystem.Data.WeaponType weaponType)
        {
            if (!_allowLocalFire)
            {
                Debug.Log("[ProjectileShooter] Local fire is disabled.");
                return;
            }

            Transform origin = firePoint != null ? firePoint : _firePoint;

            if (_playerStatus == null)
            {
                if (!LocalPlayerStatusContext.TryGet(out _playerStatus))
                    _playerStatus = GetComponentInParent<PlayerStatus>();
            }

            if (_statCalculator == null) _statCalculator = GetComponent<StatCalculator>();

            if (origin == null || _playerStatus == null || _statCalculator == null)
            {
                Debug.LogWarning($"[ProjectileShooter] 필수 참조가 누락됨. Origin: {origin != null}, Status: {_playerStatus != null}, Calc: {_statCalculator != null}");
                return;
            }

            Vector3 worldDirection = AimDirectionUtility.ToWorldXZ(aimDirection);
            if (worldDirection == Vector3.zero)
            {
                worldDirection = origin.forward;
                worldDirection.y = 0f;
                worldDirection = worldDirection.sqrMagnitude > 0.0001f ? worldDirection.normalized : Vector3.forward;
            }

            Quaternion launchRotation = Quaternion.LookRotation(worldDirection, Vector3.up);

            ProjectileBase projectile = SpawnProjectile(weaponType, origin.position, launchRotation);
            if (projectile == null)
            {
                Debug.LogWarning("[ProjectileShooter] Spawned object does not have a Projectile component.");
                return;
            }

            projectile.IsCritical = isCritical;
            projectile.Launcher = this;
            projectile.SourceWeaponType = weaponType;
            projectile.ChargeRatio = chargeRatio;
            projectile.SetUpdateSimulationEnabled(true);

            ProjectileStats finalStats = _statCalculator.Calculate(chargeRatio, isCritical);
            projectile.ProjectileDamage = finalStats.Damage;
            projectile.BaseSpeed = finalStats.Speed;
            projectile.Velocity = worldDirection * finalStats.Speed;

            for (int i = 0; i < _playerStatus.CurrentBehaviors.Count; i++)
            {
                ActiveBehavior activeBehavior = _playerStatus.CurrentBehaviors[i];
                if (activeBehavior.BehaviorSO == null) continue;
                activeBehavior.BehaviorSO.InjectBehavior(projectile, activeBehavior.Level);
            }

            _playerStatus.TryInjectAndConsumeNextShotBehavior(projectile);

            // 풀차징 보너스: 활(Bow) 전용 - 관통 횟수 추가
            if (weaponType == InputSystem.Data.WeaponType.Bow &&
    _fullChargePierceBonus > 0 &&
    chargeRatio >= _fullChargeThreshold)
            {
                PierceHitBehavior existingPierce = projectile.FindHitBehavior<PierceHitBehavior>();

                if (existingPierce != null)
                {
                    existingPierce.PierceCount += _fullChargePierceBonus;
                    Debug.Log($"[ProjectileShooter] 활 풀차징! 기존 관통에 +{_fullChargePierceBonus} 합산. 최종 관통: {existingPierce.PierceCount}");
                }
                else
                {
                    projectile.AddHitBehavior(new PierceHitBehavior(_fullChargePierceBonus), 90);   // ← -1 제거됨
                    Debug.Log($"[ProjectileShooter] 활 풀차징! 관통 보너스 +{_fullChargePierceBonus} 신규 적용.");
                }
            }

            // 스나이퍼 기본 관통 - 차징/조건 없이 항상 적용
            if (weaponType == InputSystem.Data.WeaponType.Sniper && _sniperBasePierceCount > 0)
            {
                PierceHitBehavior existingPierce = projectile.FindHitBehavior<PierceHitBehavior>();

                if (existingPierce != null)
                {
                    existingPierce.PierceCount += _sniperBasePierceCount;
                    Debug.Log($"[ProjectileShooter] 스나이퍼 기존 관통에 +{_sniperBasePierceCount} 합산. 최종 관통: {existingPierce.PierceCount}");
                }
                else
                {
                    projectile.AddHitBehavior(new PierceHitBehavior(_sniperBasePierceCount), 90);
                    Debug.Log($"[ProjectileShooter] 스나이퍼 기본 관통 +{_sniperBasePierceCount} 적용.");
                }
            }

            Debug.Log($"[ProjectileShooter] Fired projectile. Speed: {finalStats.Speed}, Damage: {finalStats.Damage}");
        }

        private ProjectileBase SpawnProjectile(InputSystem.Data.WeaponType weaponType, Vector3 position, Quaternion rotation)
        {
            if (ProjectileManager.Instance != null)
                return ProjectileManager.Instance.GetProjectile(weaponType, position, rotation);

            Debug.LogError("[ProjectileShooter] ProjectileManager Instance가 씬에 존재하지 않는다!");
            return null;
        }

        public Vector3 GetPredictedProjectileOrigin(Vector3 firePointPosition, Vector3 worldDirection)
        {
            return firePointPosition;
        }

        public bool TryGetProjectileCollisionRadius(InputSystem.Data.WeaponType weaponType, out float radius)
        {
            radius = 0f;

            return ProjectileManager.Instance != null &&
                   ProjectileManager.Instance.TryGetProjectileCollisionRadius(weaponType, out radius) &&
                   radius > 0f;
        }
    }
}
