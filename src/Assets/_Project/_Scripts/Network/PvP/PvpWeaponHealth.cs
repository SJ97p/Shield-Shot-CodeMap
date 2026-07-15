using Fusion;
using Shield_Shot.GameplayCore.Common;
using Shield_Shot.GameplayCore.Monster.Status;
using Shield_Shot.GameplayCore.Network.Match;
using Shield_Shot.GameplayCore.Render;
using UnityEngine;

namespace Shield_Shot.GameplayCore.Network.Pvp
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(PvpWeaponActorIdentity))]
    [RequireComponent(typeof(StatusEffectController))]
    public sealed class PvpWeaponHealth : NetworkBehaviour, ITakeDamage
    {
        [Header("Health")]
        [SerializeField] private float _maxHealth = 100f;

        [Header("References")]
        [SerializeField] private PvpMatchStateController _matchStateController;

        [Networked] public float CurrentHealth { get; private set; }
        [Networked] public NetworkBool IsDead { get; private set; }

        public float HealthRatio => _maxHealth <= 0f
    ? 0f
    : Mathf.Clamp01(CurrentHealth / _maxHealth);

        private PvpWeaponActorIdentity _identity;

        private void Awake()
        {
            _identity = GetComponent<PvpWeaponActorIdentity>();

            if (GetComponent<StatusEffectController>() == null)
            {
                gameObject.AddComponent<StatusEffectController>();
            }

            if (_matchStateController == null)
            {
                _matchStateController = FindFirstObjectByType<PvpMatchStateController>();
            }
        }

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                CurrentHealth = _maxHealth;
                IsDead = false;
            }

            Debug.Log($"[PvpWeaponHealth] Spawned. Owner: {_identity.Owner}, Side: {_identity.Side}, HP: {CurrentHealth}");
        }

        public void ApplyDamage(float damage, NetworkProjectileIdentity sourceProjectile)
        {
            ApplyDamage(damage, sourceProjectile, transform.position, false);
        }

        public void TakeDamage(float damage)
        {
            ApplyDamage(damage, null, transform.position, false);
        }

        public void ApplyDamage(float damage, NetworkProjectileIdentity sourceProjectile, Vector3 hitPosition, bool isCritical)
        {
            if (!Object.HasStateAuthority || IsDead)
            {
                return;
            }

            if (damage <= 0f)
            {
                return;
            }

            if (_matchStateController == null)
            {
                _matchStateController = FindFirstObjectByType<PvpMatchStateController>();
            }

            if (_matchStateController != null &&
                _matchStateController.CurrentState != PvpMatchState.Fighting)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);

            Debug.Log($"[PvpWeaponHealth] Damaged. Target: {_identity.Owner}/{_identity.Side}, Damage: {damage}, Critical: {isCritical}, HP: {CurrentHealth}");
            RPC_ShowDamagePopup(hitPosition, damage, isCritical);

            if (CurrentHealth <= 0f)
            {
                Die(sourceProjectile);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ShowDamagePopup(Vector3 position, float damage, NetworkBool isCritical)
        {
            if (DamagePopupManager.Instance == null)
            {
                Debug.LogWarning("[PvpWeaponHealth] DamagePopupManager is missing.");
                return;
            }

            DamagePopupManager.Instance.Show(position, damage, isCritical);
        }

        private void Die(NetworkProjectileIdentity sourceProjectile)
        {
            if (IsDead)
            {
                return;
            }

            IsDead = true;

            PlayerSide killerSide = ResolveKillerSide(sourceProjectile);

            Debug.Log($"[PvpWeaponHealth] Dead. Target: {_identity.Owner}/{_identity.Side}, KillerSide: {killerSide}");

            if (_matchStateController == null)
            {
                _matchStateController = FindFirstObjectByType<PvpMatchStateController>();
            }

            if (_matchStateController != null)
            {
                _matchStateController.AddScore(killerSide);
            }
            else
            {
                Debug.LogWarning("[PvpWeaponHealth] MatchStateController is missing.");
            }
        }

        public void ResetHealth()
        {
            if (!Object.HasStateAuthority)
            {
                return;
            }

            CurrentHealth = _maxHealth;
            IsDead = false;

            Debug.Log($"[PvpWeaponHealth] Reset health. Owner: {_identity.Owner}, Side: {_identity.Side}, HP: {CurrentHealth}");
        }

        private PlayerSide ResolveKillerSide(NetworkProjectileIdentity sourceProjectile)
        {
            if (sourceProjectile != null)
            {
                return sourceProjectile.OwnerSide;
            }

            return _identity.Side == PlayerSide.Bottom
                ? PlayerSide.Top
                : PlayerSide.Bottom;
        }
    }
}
