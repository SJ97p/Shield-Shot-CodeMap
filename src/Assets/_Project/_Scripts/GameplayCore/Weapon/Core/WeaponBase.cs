using Shield_Shot.Audio;
using Shield_Shot.DataManagement.InventorySystem;
using Shield_Shot.GameplayCore.Render;
using Shield_Shot.GameplayCore.Weapon.Aim;
using Shield_Shot.GameplayCore.Weapon.Charge;
using Shield_Shot.GameplayCore.Weapon.Projectile;
using Shield_Shot.InputSystem.Data;
using UnityEngine;

namespace Shield_Shot.GameplayCore.Weapon.Core
{
    [RequireComponent(typeof(WeaponAimController))]
    [RequireComponent(typeof(WeaponRotationController))]
    [RequireComponent(typeof(WeaponChargeController))]
    public abstract class WeaponBase : MonoBehaviour, IWeaponStrategy
    {
        #region Inspector
        [Header("References")]
        [SerializeField] protected Transform firePoint;

        [Header("[Base] Attack Cooldown Settings")]
        [Tooltip("무기 발사 후 재입력/장전이 불가능한 기본 쿨타임 (초)")]
        [SerializeField] protected float attackCooldownDuration = 0.5f;

        [Header("[Base] Critical Settings")]
        [Tooltip("크리티컬 발생 시 데미지 배율")]
        [SerializeField] protected float criticalDamageMultiplier = 2.0f;

        [Header("[Base] Charge VFX Settings")]
        [Tooltip("차징 진행 중 일정 주기마다 재생할 VFX 타입")]
        [SerializeField] protected VFXType chargingVfxType = VFXType.Charging;
        [Tooltip("차징 이펙트 생성 주기 (초 단위)")]
        [SerializeField] protected float vfxSpawnInterval = 0.15f;
        [Tooltip("차징 VFX 자동 반환 시간 (초)")]
        [SerializeField] protected float chargingVfxDuration = 1f;
        [Tooltip("풀차징 달성 순간 딱 1번 재생할 VFX 타입")]
        [SerializeField] protected VFXType fullChargeVfxType = VFXType.FullCharging;
        [Tooltip("풀차징 VFX 자동 반환 시간 (초)")]
        [SerializeField] protected float fullChargeVfxDuration = 2f;

        [Header("[Base] Weapon Aim Visual Settings")]
        [SerializeField] private WeaponAimVisualData _aimVisualData;

        [Header("[Base] Projectile Fire")]
        [SerializeField] private MonoBehaviour _projectileFireHandlerOverride;

        [Header("[Base] Rotation")]
        [SerializeField] private bool _applyRotationLocally = true;

        [Header("[Base] Sound")]
        [Tooltip("이 무기로 교체/장착됐을 때 재생할 사운드 (활, 라이플 등 무기마다 다르게 설정)")]
        [SerializeField] protected AudioClip equipSfx;
        [SerializeField] private float _equipVolume = 1f;

        [Header("[Base] Active Skill")]
        [Tooltip("기본/테스트용 스킬 (WeaponItem 데이터가 없는 테스트 프리팹에서 쓰인다). " +
                 "실제 게임에서는 WeaponManager가 뽑기 결과(WeaponItem.SkillType)를 " +
                 "WeaponSkillDatabaseSO에서 찾아 SetSkill()로 덮어쓴다.")]
        [SerializeField] private ActiveWeaponSkillSO weaponSkill;
        #endregion

        private float _vfxTimer;
        private bool _isFullChargeVFXPlayed;
        private float _attackCooldownTimer = 0f;
        protected abstract Transform EffectSpawnPoint { get; }

        protected WeaponAimController aimController;
        protected WeaponRotationController rotationController;
        protected WeaponChargeController chargeController;
        protected AimLineRenderer aimLineRenderer;
        protected ProjectileShooter projectileLauncher;
        protected IProjectileFireHandler projectileFireHandler;

        #region 공개 프로퍼티
        public abstract WeaponType Type { get; }
        public WeaponAimVisualData AimVisualData => _aimVisualData;
        public float ChargeRatio => chargeController != null ? chargeController.ChargeRatio : 0f;
        public Vector3 AimDirection => aimController != null ? aimController.AimDirection : Vector3.right;
        public Transform FirePoint => firePoint;
        public bool IsCooldownActive => _attackCooldownTimer > 0f;
        public float CriticalDamageMultiplier => criticalDamageMultiplier;
        public IProjectileFireHandler ProjectileFireHandler => projectileFireHandler;

        /// <summary>이 무기 프리팹에 지정된 능동 스킬. 없으면 null.</summary>
        public ActiveWeaponSkillSO Skill => weaponSkill;
        #endregion

        public void SetSkill(ActiveWeaponSkillSO skill)
        {
            weaponSkill = skill;
        }

        public void PlayEquipSound()
        {
            if (equipSfx == null) return;

            if (SoundManager.Instance != null)
                SoundManager.Instance.PlaySFX(equipSfx, _equipVolume);
        }

        public void SetLocalRotationEnabled(bool isEnabled) => _applyRotationLocally = isEnabled;

        public void SetProjectileFireHandler(IProjectileFireHandler fireHandler)
        {
            projectileFireHandler = fireHandler;
            _projectileFireHandlerOverride = fireHandler as MonoBehaviour;
        }

        protected virtual void Awake()
        {
            aimController = GetComponent<WeaponAimController>();
            rotationController = GetComponent<WeaponRotationController>();
            chargeController = GetComponent<WeaponChargeController>();
            aimLineRenderer = GetComponent<AimLineRenderer>();
            projectileLauncher = GetComponent<ProjectileShooter>();
            projectileFireHandler = ResolveProjectileFireHandler();
        }

        protected virtual void Update()
        {
            if (_attackCooldownTimer > 0f)
                _attackCooldownTimer -= Time.deltaTime;
        }

        public virtual void Initialize()
        {
            aimController = GetComponent<WeaponAimController>();
            rotationController = GetComponent<WeaponRotationController>();
            chargeController = GetComponent<WeaponChargeController>();
            aimLineRenderer = GetComponent<AimLineRenderer>();
            projectileLauncher = GetComponent<ProjectileShooter>();
            projectileFireHandler = ResolveProjectileFireHandler();

            chargeController?.Initialize();
            aimLineRenderer?.Hide();
            _attackCooldownTimer = 0f;

            Debug.Log($"[{gameObject.name}] 무기 컴포넌트 재바인딩 및 초기화 완료! Skill={(weaponSkill != null ? weaponSkill.BehaviorName : "없음")}");
        }

        public virtual void ApplyWeaponData(WeaponItem data)
        {
            if (data == null) return;

            attackCooldownDuration = data.FinalFireRate;
            criticalDamageMultiplier = data.FinalCriticalDamageMultiplier;

            Debug.Log($"[{gameObject.name}] WeaponData 적용 완료. " +
                      $"CoolDown={attackCooldownDuration}s, " +
                      $"CritMultiplier={criticalDamageMultiplier}");
        }

        protected void HandleChargeVFX(float currentChargeRatio)
        {
            if (EffectSpawnPoint == null) return;

            if (currentChargeRatio >= 0.99f)
            {
                if (!_isFullChargeVFXPlayed)
                {
                    SpawnVFX(fullChargeVfxType, fullChargeVfxDuration);
                    _isFullChargeVFXPlayed = true;
                    OnFullChargeAchieved();
                }
            }
            else
            {
                _vfxTimer += Time.deltaTime;
                if (_vfxTimer >= vfxSpawnInterval)
                {
                    if (currentChargeRatio > 0.05f)
                        SpawnVFX(chargingVfxType, chargingVfxDuration);
                    _vfxTimer = 0f;
                }
                _isFullChargeVFXPlayed = false;
            }
        }

        private void SpawnVFX(VFXType vfxType, float duration)
        {
            if (vfxType == VFXType.None || EffectSpawnPoint == null) return;

            VFXPoolManager vfxPoolManager = VFXPoolManager.EnsureInstance();
            if (vfxPoolManager == null)
            {
                Debug.LogWarning($"[WeaponBase] VFXPoolManager 없음. {vfxType} VFX 스킵.");
                return;
            }

            vfxPoolManager.SpawnVFX(vfxType, EffectSpawnPoint.position, EffectSpawnPoint.rotation, duration);
        }

        protected void StartAttackCooldown() => _attackCooldownTimer = CalculateFinalCooldown(attackCooldownDuration);
        protected void StartAttackCooldown(float customDuration) => _attackCooldownTimer = CalculateFinalCooldown(customDuration);

        private float CalculateFinalCooldown(float baseCooldown)
        {
            float result = baseCooldown;

            if (LocalPlayerStatusContext.TryGet(out PlayerStatus playerStatus))
            {
                for (int i = 0; i < playerStatus.CurrentBehaviors.Count; i++)
                {
                    ActiveBehavior active = playerStatus.CurrentBehaviors[i];
                    if (active.BehaviorSO is AttackSpeedModifierSO attackSpeedSO)
                    {
                        result = attackSpeedSO.ApplyCooldown(result, active.Level);
                    }
                }
            }

            return Mathf.Max(0.01f, result);
        }

        protected void ResetChargeVFXStates()
        {
            _vfxTimer = 0f;
            _isFullChargeVFXPlayed = false;
        }

        protected virtual void OnFullChargeAchieved() { }

        #region IWeaponStrategy 구현
        public virtual void HandleInputStay(InputContext ctx)
        {
            if (IsCooldownActive)
            {
                chargeController?.Reset();
                aimLineRenderer?.Hide();
                return;
            }

            aimController?.UpdateAimDirection(ctx.dragVector);
            ApplyLocalRotation();

            if (ctx.state == GestureState.Charging || ctx.state == GestureState.ChargedComplete)
                chargeController?.Tick(Time.deltaTime);
            else
                chargeController?.Reset();

            if (firePoint != null)
            {
                aimLineRenderer?.Show(this);
                aimLineRenderer?.UpdateLine(AimDirection, ChargeRatio, firePoint.position);
            }
        }

        public abstract void HandleInputUp(InputContext ctx);

        public virtual void Deactivate()
        {
            chargeController?.Reset();
            aimLineRenderer?.Hide();
        }

        protected void ApplyLocalRotation()
        {
            if (!_applyRotationLocally) return;
            rotationController?.SyncRotation(AimDirection);
        }

        private IProjectileFireHandler ResolveProjectileFireHandler()
        {
            if (_projectileFireHandlerOverride is IProjectileFireHandler overrideHandler)
            {
                Debug.Log($"[{gameObject.name}] Projectile fire handler resolved by override: {_projectileFireHandlerOverride.GetType().Name}");
                return overrideHandler;
            }

            if (_projectileFireHandlerOverride != null)
                Debug.LogWarning($"[{gameObject.name}] Projectile fire handler override must implement IProjectileFireHandler.");

            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            IProjectileFireHandler fallbackHandler = null;

            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is not IProjectileFireHandler handler) continue;

                if (handler is not ProjectileShooter)
                {
                    Debug.Log($"[{gameObject.name}] Projectile fire handler resolved by component: {behaviours[i].GetType().Name}");
                    return handler;
                }

                fallbackHandler ??= handler;
            }

            if (fallbackHandler != null)
                Debug.Log($"[{gameObject.name}] Projectile fire handler resolved by fallback: {fallbackHandler.GetType().Name}");
            else
                Debug.LogWarning($"[{gameObject.name}] Projectile fire handler is missing.");

            return fallbackHandler;
        }
        #endregion
    }
}