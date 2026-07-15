using System;
using System.Collections.Generic;
using Shield_Shot.GameplayCore.Weapon.Projectile;
using UnityEngine;
using Shield_Shot.GameplayCore.Network.Pvp;
using Shield_Shot.InputSystem.Data;

namespace Shield_Shot.GameplayCore.Weapon.Projectile
{
    public class ProjectileBase : MonoBehaviour
    {
        [Header("Collision Detection")]
        [SerializeField] private LayerMask _environmentLayer; // 벽이나 장애물 레이어
        [SerializeField] private LayerMask _targetLayer;      // 적 캐릭터 레이어
        [SerializeField] private float _projectileRadius = 0.18f; // 투사체 두께 (구체 레이캐스트용)

        [Header("Movement Settings")]
        public float BaseSpeed = 10f;
        [SerializeField] private bool _simulateInUpdate = true;
        private Vector3 _velocity;

        public Vector3 Direction
        {
            get
            {
                return _velocity.sqrMagnitude > 0.0001f
                    ? _velocity.normalized
                    : transform.forward;
            }
        }

        public float ProjectileRadius => _projectileRadius;
        public int CollisionBehaviorCount => _collisionBehaviors.Count;
        public bool IsCritical { get; set; }

        public ProjectileShooter Launcher { get; set; }
        public WeaponType SourceWeaponType { get; set; }
        public float ChargeRatio { get; set; }
        public float ProjectileDamage { get; set; }
        public event Action<ProjectileBase> ReleaseRequested;
        public event Action<ProjectileBase, RaycastHit> CollisionExecuted;
        public event Action<ProjectileBase, Collider> HitExecuted;
        public event Func<ProjectileBase, RaycastHit, bool> SplitRequested;

        private bool _useExternalRelease;
        private bool _isReleased;
        private bool _loggedNoCastHit;
        private readonly HashSet<int> _hitTargetIds = new HashSet<int>();
        private readonly Dictionary<Renderer, Material[]> _originalMaterialsByRenderer = new Dictionary<Renderer, Material[]>();
        private Renderer[] _cachedRenderers;
        private Material _currentOverrideMaterial;
        private bool _currentOverrideReplacesAllMaterialSlots;
        private bool _hasMaterialOverride;

        public Vector3 Velocity
        {
            get => _velocity;
            set => _velocity = value;
        }

        public void SetUpdateSimulationEnabled(bool isEnabled)
        {
            _simulateInUpdate = isEnabled;
        }

        public void SetExternalReleaseEnabled(bool isEnabled)
        {
            _useExternalRelease = isEnabled;
        }

        public bool TryRequestExternalSplit(RaycastHit hitInfo)
        {
            if (SplitRequested == null)
            {
                return false;
            }

            return SplitRequested.Invoke(this, hitInfo);
        }

        // 내부적으로 행동 인스턴스와 우선순위(Priority)를 함께 묶어서 관리하기 위한 구조체
        private struct BehaviorEntry<T>
        {
            public T Behavior;
            public int Priority;

            public BehaviorEntry(T behavior, int priority)
            {
                Behavior = behavior;
                Priority = priority;
            }
        }

        // 우선순위 정렬이 적용될 내부 리스트
        private readonly List<BehaviorEntry<IMovementBehavior>> _movementBehaviors = new List<BehaviorEntry<IMovementBehavior>>();
        private readonly List<BehaviorEntry<ICollisionBehavior>> _collisionBehaviors = new List<BehaviorEntry<ICollisionBehavior>>();
        private readonly List<BehaviorEntry<IHitBehavior>> _hitBehaviors = new List<BehaviorEntry<IHitBehavior>>();

        // 런타임에 리스트 요소가 조작되어도 에러가 나지 않도록 방어하는 실행용 버퍼 캐시
        private readonly List<ICollisionBehavior> _collisionBuffer = new List<ICollisionBehavior>();
        private readonly List<IHitBehavior> _hitBuffer = new List<IHitBehavior>();

        private void Start()
        {
            if (_velocity == Vector3.zero)
            {
                _velocity = transform.forward * BaseSpeed;
            }
        }

        private void Update()
        {
            if (!_simulateInUpdate)
            {
                return;
            }

            Simulate(Time.deltaTime);
        }

        public void Simulate(float deltaTime)
        {
            if (_isReleased)
            {
                return;
            }

            Vector3 currentVelocity = Velocity;

            // 1. 주입된 이동 특성들을 우선순위대로 순차 중첩 계산
            for (int i = 0; i < _movementBehaviors.Count; i++)
            {
                _movementBehaviors[i].Behavior.UpdateMovement(transform, ref currentVelocity, deltaTime);
            }
            Velocity = currentVelocity;

            // 2. 물리 충돌 및 피격 예측 감지 (Raycast)
            Vector3 moveDelta = Velocity * deltaTime;
            float moveDistance = moveDelta.magnitude;
            Vector3 moveDirection = moveDelta.normalized;

            if (moveDistance > 0f)
            {
                // [수정 핵심]: 환경과 적 레이어를 통합하여 한 번에 스캔한다.
                LayerMask combinedMask = _environmentLayer | _targetLayer;
                RaycastHit[] hits = Physics.SphereCastAll(
                    transform.position,
                    _projectileRadius,
                    moveDirection,
                    moveDistance,
                    combinedMask,
                    QueryTriggerInteraction.Collide);

                if (hits.Length == 0 && !_loggedNoCastHit)
                {
                    _loggedNoCastHit = true;
                    Debug.Log(
                        $"[ProjectileBase] No sphere cast hit. Projectile: {name}, Origin: {transform.position}, Direction: {moveDirection}, Distance: {moveDistance}, Radius: {_projectileRadius}, EnvMask: {_environmentLayer.value}, TargetMask: {_targetLayer.value}, CombinedMask: {combinedMask.value}");
                }

                // 스캔된 모든 오브젝트를 '거리가 가까운 순서(distance)'대로 오름차순 정렬한다.
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                for (int i = 0; i < hits.Length; i++)
                {
                    RaycastHit currentHit = hits[i];
                    int hitLayer = currentHit.collider.gameObject.layer;
                    int hitLayerMask = 1 << hitLayer;

                    // 1) 맞은 것이 벽(Environment)인 경우
                    if ((_environmentLayer.value & hitLayerMask) != 0)
                    {
                        Debug.Log(
                            $"[ProjectileBase] Sphere cast environment hit. Projectile: {name}, Hit: {currentHit.collider.name}, Layer: {LayerMask.LayerToName(hitLayer)}({hitLayer}), Distance: {currentHit.distance}, Point: {currentHit.point}");
                        ExecuteCollision(currentHit);
                        return; // 벽에 막혔으므로 이후 궤적 무시 및 이동 즉시 중단
                    }
                    // 2) 맞은 것이 적(Target)인 경우
                    else if ((_targetLayer.value & hitLayerMask) != 0)
                    {
                        // 피격 판정 실행. ExecuteHit가 true를 반환하면 투사체가 소멸/정지해야 함을 의미.
                        // (만약 관통 특성이 있어서 false를 반환하면 return하지 않고 다음 충돌체를 마저 탐색한다)
                        Debug.Log(
                            $"[ProjectileBase] Sphere cast target hit candidate. Projectile: {name}, Hit: {currentHit.collider.name}, Layer: {LayerMask.LayerToName(hitLayer)}({hitLayer}), Distance: {currentHit.distance}, Point: {currentHit.point}");
                        if (ExecuteHit(currentHit.collider))
                        {
                            return;
                        }
                    }
                }
            }

            // 3. 충돌이 없었다면 최종 연산된 물리량만큼 위치 이동 및 회전 정렬
            transform.position += moveDelta;
            if (Velocity != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(Velocity);
            }
        }

        #region 동적 주입 및 자동 정렬 시스템

        public void AddMovementBehavior(IMovementBehavior behavior, int priority)
        {
            if (behavior == null) return;
            _movementBehaviors.Add(new BehaviorEntry<IMovementBehavior>(behavior, priority));
            _movementBehaviors.Sort((x, y) => x.Priority.CompareTo(y.Priority)); // 오름차순 정렬
        }

        public void AddCollisionBehavior(ICollisionBehavior behavior, int priority)
        {
            if (behavior == null) return;
            _collisionBehaviors.Add(new BehaviorEntry<ICollisionBehavior>(behavior, priority));
            _collisionBehaviors.Sort((x, y) => x.Priority.CompareTo(y.Priority));
            Debug.Log($"[ProjectileBase] Added collision behavior: {behavior.GetType().Name}, Priority: {priority}, Count: {_collisionBehaviors.Count}");
        }

        public void AddHitBehavior(IHitBehavior behavior, int priority)
        {
            if (behavior == null) return;
            _hitBehaviors.Add(new BehaviorEntry<IHitBehavior>(behavior, priority));
            _hitBehaviors.Sort((x, y) => x.Priority.CompareTo(y.Priority));
        }

        public T FindHitBehavior<T>() where T : class, IHitBehavior
        {
            for (int i = 0; i < _hitBehaviors.Count; i++)
            {
                if (_hitBehaviors[i].Behavior is T match)
                    return match;
            }
            return null;
        }
        #endregion

        #region 안전한 이벤트 실행 구조 (Buffer 순회)

        public void ExecuteCollision(RaycastHit hitInfo)
        {
            bool hasCollisionBehavior = _collisionBehaviors.Count > 0;
            Debug.Log($"[ProjectileBase] ExecuteCollision. Hit: {hitInfo.collider.name}, CollisionBehaviors: {_collisionBehaviors.Count}, Velocity: {Velocity}");

            _collisionBuffer.Clear();
            for (int i = 0; i < _collisionBehaviors.Count; i++)
            {
                _collisionBuffer.Add(_collisionBehaviors[i].Behavior);
            }

            // 버퍼 복사본을 돌기 때문에 이 도중에 부모가 파괴되거나 자식이 생성되어도 안전함
            for (int i = 0; i < _collisionBuffer.Count; i++)
            {
                _collisionBuffer[i].OnCollide(this, hitInfo);

                if (_isReleased)
                {
                    break;
                }
            }

            CollisionExecuted?.Invoke(this, hitInfo);

            if (!hasCollisionBehavior && !_isReleased)
            {
                ReleaseOrDestroy();
            }
        }

        public bool ExecuteHit(Collider targetInfo)
        {
            if (targetInfo != null)
            {
                PvpWeaponHitTarget hitTarget = targetInfo.GetComponentInParent<PvpWeaponHitTarget>();
                NetworkProjectileIdentity projectileIdentity = GetComponent<NetworkProjectileIdentity>();
                Debug.Log(
                    $"[ProjectileBase] ExecuteHit candidate. Projectile: {name}, TargetCollider: {targetInfo.name}, Layer: {LayerMask.LayerToName(targetInfo.gameObject.layer)}({targetInfo.gameObject.layer}), HasPvpHitTarget: {hitTarget != null}, ProjectileOwner: {projectileIdentity?.Owner}, ProjectileSide: {projectileIdentity?.OwnerSide}");

                if (hitTarget != null && !hitTarget.CanBeHitBy(projectileIdentity))
                {
                    Debug.Log(
                        $"[ProjectileBase] ExecuteHit rejected by PvpWeaponHitTarget.CanBeHitBy. Projectile: {name}, TargetCollider: {targetInfo.name}");
                    return false;
                }

                int targetId = targetInfo.GetInstanceID();
                if (!_hitTargetIds.Add(targetId))
                {
                    Debug.Log(
                        $"[ProjectileBase] ExecuteHit ignored duplicate target collider. Projectile: {name}, TargetCollider: {targetInfo.name}");
                    return false;
                }

                if (hitTarget != null)
                {
                    hitTarget.NotifyHit(projectileIdentity, this, targetInfo);
                }
            }

            bool canSurviveHit = HasHitSurvivalBehavior();
            _hitBuffer.Clear();
            for (int i = 0; i < _hitBehaviors.Count; i++)
            {
                _hitBuffer.Add(_hitBehaviors[i].Behavior);
            }

            for (int i = 0; i < _hitBuffer.Count; i++)
            {
                _hitBuffer[i].OnHit(this, targetInfo);
            }

            HitExecuted?.Invoke(this, targetInfo);

            if (!canSurviveHit && !_isReleased)
            {
                ReleaseOrDestroy();
            }

            return true;
        }

        private bool HasHitSurvivalBehavior()
        {
            for (int i = 0; i < _hitBehaviors.Count; i++)
            {
                if (_hitBehaviors[i].Behavior is IProjectileHitSurvivalBehavior)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region 특성 복사 및 상속 헬퍼
        // 자신이 가진 모든 특성과 우선순위 데이터를 새로운 자식 투사체에게 그대로 이식한다.
        public void CopyBehaviorsTo(ProjectileBase targetProjectile, params System.Type[] excludedCollisionBehaviorTypes)
        {
            if (targetProjectile == null) return;

            for (int i = 0; i < _movementBehaviors.Count; i++)
                targetProjectile.AddMovementBehavior(_movementBehaviors[i].Behavior, _movementBehaviors[i].Priority);

            for (int i = 0; i < _collisionBehaviors.Count; i++)
            {
                ICollisionBehavior behavior = _collisionBehaviors[i].Behavior;
                if (ShouldExcludeCollisionBehavior(behavior, excludedCollisionBehaviorTypes))
                {
                    continue;
                }

                if (behavior is ICopyableCollisionBehavior copyableBehavior)
                {
                    behavior = copyableBehavior.CreateCopy();
                }

                targetProjectile.AddCollisionBehavior(behavior, _collisionBehaviors[i].Priority);
            }

            for (int i = 0; i < _hitBehaviors.Count; i++)
            {
                IHitBehavior behavior = _hitBehaviors[i].Behavior;
                if (behavior is ICopyableHitBehavior copyableBehavior)
                {
                    behavior = copyableBehavior.CreateCopy();
                }

                targetProjectile.AddHitBehavior(behavior, _hitBehaviors[i].Priority);
            }

            if (_currentOverrideMaterial != null)
            {
                targetProjectile.ApplyMaterialOverride(
                    _currentOverrideMaterial,
                    _currentOverrideReplacesAllMaterialSlots
                );
            }
        }

        private static bool ShouldExcludeCollisionBehavior(ICollisionBehavior behavior, System.Type[] excludedTypes)
        {
            if (behavior == null || excludedTypes == null)
            {
                return false;
            }

            System.Type behaviorType = behavior.GetType();
            for (int i = 0; i < excludedTypes.Length; i++)
            {
                System.Type excludedType = excludedTypes[i];
                if (excludedType != null && excludedType.IsAssignableFrom(behaviorType))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Object Pool Integration

        private Shield_Shot.GameplayCore.Weapon.Projectile.ProjectileObjectPool _associatedPool;

        // 생성 시 오브젝트 풀러가 이 투사체의 고향 풀을 등록해 준다.
        public void SetPool(Shield_Shot.GameplayCore.Weapon.Projectile.ProjectileObjectPool pool)
        {
            _associatedPool = pool;
        }

        // 재사용(Get)될 때 호출되어 이전 발사 때 쌓였던 모든 주입 특성과 내부 상태를 초기화한다.
        public void ResetProjectileState()
        {
            RestoreMaterialOverride();

            // 1. 주입된 모든 행동 컴포넌트 리스트 청소
            _movementBehaviors.Clear();
            _collisionBehaviors.Clear();
            _hitBehaviors.Clear();

            _collisionBuffer.Clear();
            _hitBuffer.Clear();

            // 2. 물리 및 생명주기 데이터 초기화
            _hitTargetIds.Clear();
            _isReleased = false;
            _loggedNoCastHit = false;
            Velocity = Vector3.zero;

            // 3. 다음 발사를 위한 실시간 데이터 초기화
            Launcher = null;
            ChargeRatio = 0f;
            transform.localScale = Vector3.one;
        }

        // 이제 시스템 내부에서 Destroy(gameObject)를 하던 모든 부분을 이 메서드로 대체한다.
        public void ApplyMaterialOverride(Material material, bool replaceAllMaterialSlots = true)
        {
            RestoreMaterialOverride();

            if (material == null)
            {
                return;
            }

            Renderer[] renderers = GetProjectileRenderers();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer targetRenderer = renderers[i];
                if (targetRenderer == null)
                {
                    continue;
                }

                Material[] originalMaterials = targetRenderer.sharedMaterials;
                if (originalMaterials == null || originalMaterials.Length == 0)
                {
                    continue;
                }

                _originalMaterialsByRenderer[targetRenderer] = originalMaterials;

                Material[] overrideMaterials = new Material[originalMaterials.Length];
                for (int materialIndex = 0; materialIndex < overrideMaterials.Length; materialIndex++)
                {
                    overrideMaterials[materialIndex] = replaceAllMaterialSlots
                        ? material
                        : originalMaterials[materialIndex];
                }

                if (!replaceAllMaterialSlots)
                {
                    overrideMaterials[0] = material;
                }

                targetRenderer.sharedMaterials = overrideMaterials;
            }

            _hasMaterialOverride = _originalMaterialsByRenderer.Count > 0;
            _currentOverrideMaterial = _hasMaterialOverride ? material : null;
            _currentOverrideReplacesAllMaterialSlots = replaceAllMaterialSlots;
        }

        public void RestoreMaterialOverride()
        {
            if (!_hasMaterialOverride)
            {
                return;
            }

            foreach (var pair in _originalMaterialsByRenderer)
            {
                Renderer targetRenderer = pair.Key;
                if (targetRenderer != null)
                {
                    targetRenderer.sharedMaterials = pair.Value;
                }
            }

            _originalMaterialsByRenderer.Clear();
            _currentOverrideMaterial = null;
            _hasMaterialOverride = false;
        }

        private Renderer[] GetProjectileRenderers()
        {
            if (_cachedRenderers == null || _cachedRenderers.Length == 0)
            {
                _cachedRenderers = GetComponentsInChildren<Renderer>(true);
            }

            return _cachedRenderers;
        }

        public void ReleaseOrDestroy()
        {
            if (_isReleased)
            {
                return;
            }

            _isReleased = true;
            RestoreMaterialOverride();

            if (_useExternalRelease)
            {
                ReleaseRequested?.Invoke(this);
                return;
            }

            if (_associatedPool != null)
            {
                _associatedPool.Return(this); // 풀이 있으면 풀로 안전하게 반납
            }
            else
            {
                Destroy(gameObject); // 풀 없이 단독 생성된 경우 물리적 파괴
            }
        }

        #endregion
    }
}
