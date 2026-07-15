using System;
using Fusion;
using Shield_Shot.GameplayCore.Weapon.Projectile;

namespace Shield_Shot.GameplayCore.Network.Pvp
{
    [Serializable]
    public struct PvpProjectileAugmentEntry : INetworkStruct
    {
        public int BehaviorCode;
        public int Level;

        public bool IsValid => BehaviorCode != 0 && Level > 0;

        public PvpProjectileAugmentEntry(int behaviorCode, int level)
        {
            BehaviorCode = behaviorCode;
            Level = level;
        }
    }

    public static class PvpProjectileBehaviorCode
    {
        // Code format: thousands digit = major category, hundreds digit = subcategory, last two digits = effect index.
        // 1000 Reflect, 2000 Pierce, 3000 Split/Clone, 4000 Targeting/Chain, 5000 Damage, 6000 Status, 9000 Visual/System.
        public const int StandardReflect = 1101;
        public const int RandomReflect = 1201;
        public const int Pierce = 2101;
        public const int Split = 3101;
        public const int ChainLightning = 4101;
        public const int DefaultDamage = 5101;
        public const int Poison = 6101;
        public const int ElementTrailFire = 6201;
        public const int ElementTrailWind = 6202;
        public const int WindFieldBoost = 6203;
        public const int ElementTrailIce = 6204;
        public const int FreezeHit = 6205;
        public const int HitImpactFX = 9101;
        public const int WallCollisionFX = 9201;

        public static int Resolve(ProjectileBehaviorSO behaviorSO)
        {
            if (behaviorSO == null)
            {
                return 0;
            }

            if (behaviorSO.NetworkCode != 0)
            {
                return behaviorSO.NetworkCode;
            }

            string id = string.IsNullOrWhiteSpace(behaviorSO.BehaviorID)
                ? string.Empty
                : behaviorSO.BehaviorID.Trim();
            string typeName = behaviorSO.GetType().Name;

            if (id == "1" ||
                string.Equals(id, "StandardReflect", StringComparison.OrdinalIgnoreCase) ||
                typeName.IndexOf("StandardReflect", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return StandardReflect;
            }

            if (string.Equals(id, "RandomReflect", StringComparison.OrdinalIgnoreCase) ||
                typeName.IndexOf("RandomReflect", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return RandomReflect;
            }

            if (string.Equals(id, "Pierce", StringComparison.OrdinalIgnoreCase) ||
                typeName.IndexOf("Pierce", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Pierce;
            }

            if (string.Equals(id, "Split", StringComparison.OrdinalIgnoreCase) ||
                typeName.IndexOf("Split", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Split;
            }

            if (string.Equals(id, "DefaultDamage", StringComparison.OrdinalIgnoreCase) ||
                typeName.IndexOf("DefaultDamage", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return DefaultDamage;
            }

            if (string.Equals(id, "ChainLightning", StringComparison.OrdinalIgnoreCase) ||
                typeName.IndexOf("ChainLightning", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ChainLightning;
            }

            if (string.Equals(id, "Poison", StringComparison.OrdinalIgnoreCase) ||
                typeName.IndexOf("Poison", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Poison;
            }

            if (string.Equals(id, "ElementTrail_Wind", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(id, "ElementTrailWind", StringComparison.OrdinalIgnoreCase))
            {
                return ElementTrailWind;
            }

            if (string.Equals(id, "ElementTrail_Ice", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(id, "ElementTrailIce", StringComparison.OrdinalIgnoreCase))
            {
                return ElementTrailIce;
            }

            if (string.Equals(id, "FreezeHit", StringComparison.OrdinalIgnoreCase) ||
                typeName.IndexOf("Freeze", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return FreezeHit;
            }

            if (string.Equals(id, "WindFieldBoost", StringComparison.OrdinalIgnoreCase) ||
                typeName.IndexOf("WindFieldBoost", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return WindFieldBoost;
            }

            if (string.Equals(id, "ElementTrail_Fire", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(id, "ElementTrailFire", StringComparison.OrdinalIgnoreCase) ||
                typeName.IndexOf("ElementTrail", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ElementTrailFire;
            }

            if (string.Equals(id, "HitImpactFX", StringComparison.OrdinalIgnoreCase) ||
                typeName.IndexOf("HitFX", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return HitImpactFX;
            }

            if (string.Equals(id, "WallCollisionFX", StringComparison.OrdinalIgnoreCase) ||
                typeName.IndexOf("CollisionFX", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return WallCollisionFX;
            }

            return 0;
        }
    }
}
