using Shield_Shot.InputSystem.Data;
using UnityEngine;

namespace Shield_Shot.GameplayCore.Weapon.Projectile
{
    public interface IProjectileFireHandler
    {
        void Fire(Transform firePoint, Vector3 aimDirection, float chargeRatio, bool isCritical);
    }

    public interface IProjectileAimPredictionProvider
    {
        Vector3 GetPredictedProjectileOrigin(Vector3 firePointPosition, Vector3 worldDirection);
        bool TryGetProjectileCollisionRadius(WeaponType weaponType, out float radius);
    }
}
