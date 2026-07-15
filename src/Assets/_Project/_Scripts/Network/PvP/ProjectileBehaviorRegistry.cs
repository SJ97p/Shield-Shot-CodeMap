using System.Collections.Generic;
using Shield_Shot.GameplayCore.Weapon.Projectile;
using UnityEngine;

namespace Shield_Shot.GameplayCore.Network.Pvp
{
    public sealed class ProjectileBehaviorRegistry : MonoBehaviour
    {
        [Header("Projectile Behaviors")]
        [SerializeField] private List<ProjectileBehaviorSO> _behaviors = new();

        private readonly Dictionary<int, ProjectileBehaviorSO> _behaviorByCode = new();

        private void Awake()
        {
            BuildRegistry();
        }

        public bool TryGet(int behaviorCode, out ProjectileBehaviorSO behaviorSO)
        {
            return _behaviorByCode.TryGetValue(behaviorCode, out behaviorSO);
        }

        public int ApplyPayload(ProjectileBase projectile, PvpProjectileAugmentPayload payload)
        {
            if (projectile == null)
            {
                return 0;
            }

            if (!payload.HasAnyAugment)
            {
                Debug.Log("[ProjectileBehaviorRegistry] Payload is empty.");
                return 0;
            }

            int appliedCount = 0;
            payload.ForEach(entry =>
            {
                if (!TryGet(entry.BehaviorCode, out ProjectileBehaviorSO behaviorSO))
                {
                    Debug.LogWarning($"[ProjectileBehaviorRegistry] Unknown behavior code: {entry.BehaviorCode}");
                    return;
                }

                behaviorSO.InjectBehavior(projectile, entry.Level);
                appliedCount++;
                Debug.Log($"[ProjectileBehaviorRegistry] Applied behavior: {behaviorSO.BehaviorName}({entry.BehaviorCode}) Lv.{entry.Level}");
            });

            return appliedCount;
        }

        private void BuildRegistry()
        {
            _behaviorByCode.Clear();

            for (int i = 0; i < _behaviors.Count; i++)
            {
                ProjectileBehaviorSO behaviorSO = _behaviors[i];
                if (behaviorSO == null)
                {
                    continue;
                }

                int code = PvpProjectileBehaviorCode.Resolve(behaviorSO);
                if (code == 0)
                {
                    Debug.LogWarning($"[ProjectileBehaviorRegistry] Behavior has invalid network code 0: {behaviorSO.name}");
                    continue;
                }

                if (_behaviorByCode.ContainsKey(code))
                {
                    Debug.LogError($"[ProjectileBehaviorRegistry] Duplicate behavior code {code}: {behaviorSO.name}");
                    continue;
                }

                _behaviorByCode.Add(code, behaviorSO);
            }

            Debug.Log($"[ProjectileBehaviorRegistry] Registered {_behaviorByCode.Count} projectile behaviors.");
        }
    }
}
