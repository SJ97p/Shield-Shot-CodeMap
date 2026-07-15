using System.Collections.Generic;
using Shield_Shot.GameplayCore.Monster.Status;
using Shield_Shot.GameplayCore.Render;
using UnityEngine;

namespace Shield_Shot.GameplayCore.Field
{
    public class ElementFieldEffectSystem : MonoBehaviour
    {
        private enum BoundsSampleMode
        {
            Auto,
            Center,
            FivePoints,
            NinePoints
        }

        private const string BurnStatusID = "Burn";
        private const string ShockStatusID = "Shock";
        private const string SlowStatusID = "Slow";
        private const string FrozenStatusID = "Frozen";
        private const string ToxicSmokeStatusID = "ToxicSmoke";

        [Header("References")]
        [SerializeField] private ElementFieldGrid _fieldGrid;

        [Header("Target Registration")]
        [SerializeField] private bool _autoFindTargets;
        [SerializeField, Min(0.05f)] private float _targetRefreshInterval = 1f;

        [Header("Target Sampling")]
        [SerializeField] private BoundsSampleMode _boundsSampleMode = BoundsSampleMode.Auto;
        [SerializeField, Min(0.1f)] private float _largeTargetCellSpan = 1.5f;
        [SerializeField] private bool _includeTriggerColliders = true;
        [SerializeField] private ElementType[] _elementPriority =
        {
            ElementType.Fire,
            ElementType.Lightning,
            ElementType.Poison,
            ElementType.Ice
        };

        [Header("Apply")]
        [SerializeField, Min(0.05f)] private float _applyInterval = 0.35f;

        [Header("Burn")]
        [SerializeField] private float _burnDuration = 1f;
        [SerializeField] private float _burnTickInterval = 0.5f;
        [SerializeField] private float _burnDamagePerTick = 2f;

        [Header("Shock")]
        [SerializeField] private float _shockDuration = 0.8f;
        [SerializeField] private float _shockTickInterval = 0.4f;
        [SerializeField] private float _shockDamagePerTick = 2f;

        [Header("Slow")]
        [SerializeField] private float _slowDuration = 1f;
        [SerializeField, Range(0f, 1f)] private float _slowMultiplier = 0.5f;

        [Header("Frozen")]
        [SerializeField] private float _frozenDuration = 1.5f;

        [Header("Toxic Smoke")]
        [SerializeField] private float _toxicDuration = 1.5f;
        [SerializeField] private float _toxicTickInterval = 0.5f;
        [SerializeField] private float _toxicDamagePerTick = 2f;

        [Header("VFX")]
        [SerializeField] private VFXType _tickVfxType = VFXType.Hit;
        [SerializeField] private float _vfxAutoReleaseTime = 1.5f;

        [Header("Debug")]
        [SerializeField] private int _debugTargetCount;

        private readonly List<StatusEffectController> _targets = new();
        private readonly Dictionary<int, float> _nextApplyTimeByTarget = new();
        private readonly Dictionary<int, Collider[]> _collidersByTarget = new();
        private readonly Vector3[] _samplePoints = new Vector3[9];

        private float _nextTargetRefreshTime;

        public int TargetCount => _targets.Count;

        private void Reset()
        {
            _fieldGrid = FindFirstObjectByType<ElementFieldGrid>();
        }

        private void Update()
        {
            ElementFieldGrid grid = ResolveFieldGrid();

            if (grid == null)
            {
                return;
            }

            if (_autoFindTargets && Time.time >= _nextTargetRefreshTime)
            {
                RefreshTargetsFromScene();
                _nextTargetRefreshTime = Time.time + _targetRefreshInterval;
            }
            _debugTargetCount = _targets.Count;

            ApplyFieldEffects(grid);
        }

        public void RegisterTarget(StatusEffectController target)
        {
            if (target == null)
            {
                return;
            }

            if (_targets.Contains(target))
            {
                return;
            }

            _targets.Add(target);
            CacheTargetColliders(target);
            _debugTargetCount = _targets.Count;
        }

        public void UnregisterTarget(StatusEffectController target)
        {
            if (target == null)
            {
                return;
            }

            _targets.Remove(target);
            int targetId = target.GetInstanceID();
            _nextApplyTimeByTarget.Remove(targetId);
            _collidersByTarget.Remove(targetId);
            _debugTargetCount = _targets.Count;
        }

        [ContextMenu("Refresh Targets From Scene")]
        public void RefreshTargetsFromScene()
        {
            _targets.Clear();
            _nextApplyTimeByTarget.Clear();
            _collidersByTarget.Clear();

            StatusEffectController[] foundTargets =
                FindObjectsByType<StatusEffectController>(FindObjectsSortMode.None);

            for (int i = 0; i < foundTargets.Length; i++)
            {
                StatusEffectController target = foundTargets[i];

                if (target != null && target.isActiveAndEnabled)
                {
                    RegisterTarget(target);
                }
            }

            Debug.Log($"[ElementFieldEffectSystem] Refreshed targets. Count: {_targets.Count}");
        }

        private ElementFieldGrid ResolveFieldGrid()
        {
            if (_fieldGrid == null)
            {
                _fieldGrid = ElementFieldGrid.Instance != null
                    ? ElementFieldGrid.Instance
                    : FindFirstObjectByType<ElementFieldGrid>();
            }

            return _fieldGrid;
        }

        private void ApplyFieldEffects(ElementFieldGrid grid)
        {
            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                StatusEffectController target = _targets[i];

                if (target == null || !target.isActiveAndEnabled)
                {
                    RemoveTargetAt(i);
                    continue;
                }

                int targetId = target.GetInstanceID();

                if (_nextApplyTimeByTarget.TryGetValue(targetId, out float nextApplyTime) &&
                    Time.time < nextApplyTime)
                {
                    continue;
                }

                if (!TryFindBestEffectCell(grid, target, out ElementFieldCellData cellData))
                {
                    continue;
                }

                if (!TryCreateStatusEffect(cellData, out StatusEffectData effect))
                {
                    continue;
                }

                target.ApplyOrRefresh(effect);
                _nextApplyTimeByTarget[targetId] = Time.time + _applyInterval;
            }
        }

        private bool TryFindBestEffectCell(
            ElementFieldGrid grid,
            StatusEffectController target,
            out ElementFieldCellData bestCellData)
        {
            int sampleCount = BuildSamplePoints(grid, target);
            if (TryFindBestActiveCell(grid, sampleCount, out bestCellData))
            {
                return true;
            }

            return TryFindFrozenTerrainCell(grid, sampleCount, out bestCellData);
        }

        private bool TryFindBestActiveCell(
            ElementFieldGrid grid,
            int sampleCount,
            out ElementFieldCellData bestCellData)
        {
            int bestPriority = int.MaxValue;
            bestCellData = default;

            for (int i = 0; i < sampleCount; i++)
            {
                if (!grid.TryGetCellData(_samplePoints[i], out ElementFieldCellData cellData) ||
                    !cellData.IsActive)
                {
                    continue;
                }

                int priority = GetElementPriority(cellData.CurrentElement);

                if (priority >= bestPriority)
                {
                    continue;
                }

                bestPriority = priority;
                bestCellData = cellData;

                if (bestPriority == 0)
                {
                    return true;
                }
            }

            return bestPriority < int.MaxValue;
        }

        private bool TryFindFrozenTerrainCell(
            ElementFieldGrid grid,
            int sampleCount,
            out ElementFieldCellData bestCellData)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                if (!grid.TryGetCellData(_samplePoints[i], out ElementFieldCellData cellData) ||
                    cellData.TerrainElement != TerrainElementType.Ice)
                {
                    continue;
                }

                bestCellData = cellData;
                bestCellData.CurrentElement = ElementType.Ice;
                bestCellData.RemainingTime = _slowDuration;
                bestCellData.LastReactionType = ElementReactionType.None;
                return true;
            }

            bestCellData = default;
            return false;
        }

        private int BuildSamplePoints(ElementFieldGrid grid, StatusEffectController target)
        {
            if (!TryGetTargetBounds(target, out Bounds bounds))
            {
                _samplePoints[0] = target.transform.position;
                return 1;
            }

            BoundsSampleMode sampleMode = ResolveSampleMode(grid, bounds);
            Vector3 center = bounds.center;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            _samplePoints[0] = center;

            if (sampleMode == BoundsSampleMode.Center)
            {
                return 1;
            }

            _samplePoints[1] = new Vector3(min.x, center.y, center.z);
            _samplePoints[2] = new Vector3(max.x, center.y, center.z);
            _samplePoints[3] = new Vector3(center.x, center.y, min.z);
            _samplePoints[4] = new Vector3(center.x, center.y, max.z);

            if (sampleMode == BoundsSampleMode.FivePoints)
            {
                return 5;
            }

            _samplePoints[5] = new Vector3(min.x, center.y, min.z);
            _samplePoints[6] = new Vector3(min.x, center.y, max.z);
            _samplePoints[7] = new Vector3(max.x, center.y, min.z);
            _samplePoints[8] = new Vector3(max.x, center.y, max.z);

            return 9;
        }

        private BoundsSampleMode ResolveSampleMode(ElementFieldGrid grid, Bounds bounds)
        {
            if (_boundsSampleMode != BoundsSampleMode.Auto)
            {
                return _boundsSampleMode;
            }

            Vector2 cellSize = grid.CellWorldSize;
            float targetWidthInCells = bounds.size.x / Mathf.Max(0.001f, cellSize.x);
            float targetDepthInCells = bounds.size.z / Mathf.Max(0.001f, cellSize.y);
            float maxCellSpan = Mathf.Max(targetWidthInCells, targetDepthInCells);

            return maxCellSpan >= _largeTargetCellSpan
                ? BoundsSampleMode.NinePoints
                : BoundsSampleMode.FivePoints;
        }

        private bool TryGetTargetBounds(StatusEffectController target, out Bounds bounds)
        {
            int targetId = target.GetInstanceID();

            if (!_collidersByTarget.TryGetValue(targetId, out Collider[] colliders) ||
                colliders == null ||
                colliders.Length == 0)
            {
                colliders = CacheTargetColliders(target);
            }

            bool hasBounds = false;
            bounds = default;

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider targetCollider = colliders[i];

                if (targetCollider == null ||
                    !targetCollider.enabled ||
                    (!_includeTriggerColliders && targetCollider.isTrigger))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = targetCollider.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(targetCollider.bounds);
            }

            return hasBounds;
        }

        private Collider[] CacheTargetColliders(StatusEffectController target)
        {
            Collider[] colliders = target.GetComponentsInChildren<Collider>();
            _collidersByTarget[target.GetInstanceID()] = colliders;
            return colliders;
        }

        private int GetElementPriority(ElementType element)
        {
            if (_elementPriority == null)
            {
                return int.MaxValue;
            }

            for (int i = 0; i < _elementPriority.Length; i++)
            {
                if (_elementPriority[i] == element)
                {
                    return i;
                }
            }

            return int.MaxValue - 1;
        }

        private void RemoveTargetAt(int index)
        {
            StatusEffectController target = _targets[index];

            if (target != null)
            {
                int targetId = target.GetInstanceID();
                _nextApplyTimeByTarget.Remove(targetId);
                _collidersByTarget.Remove(targetId);
            }

            _targets.RemoveAt(index);
            _debugTargetCount = _targets.Count;
        }

        private bool TryCreateStatusEffect(ElementFieldCellData cellData, out StatusEffectData effect)
        {
            switch (cellData.CurrentElement)
            {
                case ElementType.Fire:
                    effect = new StatusEffectData(
                        statusID: BurnStatusID,
                        type: StatusEffectType.Burn,
                        duration: _burnDuration,
                        tickInterval: _burnTickInterval,
                        damagePerTick: _burnDamagePerTick,
                        source: this,
                        showDamagePopup: true,
                        tickVfxType: _tickVfxType,
                        vfxAutoReleaseTime: _vfxAutoReleaseTime
                    );
                    return true;

                case ElementType.Lightning:
                    effect = new StatusEffectData(
                        statusID: ShockStatusID,
                        type: StatusEffectType.Shock,
                        duration: _shockDuration,
                        tickInterval: _shockTickInterval,
                        damagePerTick: _shockDamagePerTick,
                        source: this,
                        showDamagePopup: true,
                        tickVfxType: _tickVfxType,
                        vfxAutoReleaseTime: _vfxAutoReleaseTime
                    );
                    return true;

                case ElementType.Ice:
                    if (cellData.LastReactionType == ElementReactionType.Freeze)
                    {
                        effect = new StatusEffectData(
                            statusID: FrozenStatusID,
                            type: StatusEffectType.Frozen,
                            duration: _frozenDuration,
                            source: this,
                            showDamagePopup: false,
                            tickVfxType: VFXType.None
                        );
                        return true;
                    }

                    effect = new StatusEffectData(
                        statusID: SlowStatusID,
                        type: StatusEffectType.Slow,
                        duration: _slowDuration,
                        slowMultiplier: _slowMultiplier,
                        source: this,
                        showDamagePopup: false,
                        tickVfxType: VFXType.None
                    );
                    return true;

                case ElementType.Poison:
                    effect = new StatusEffectData(
                        statusID: ToxicSmokeStatusID,
                        type: StatusEffectType.Poison,
                        duration: _toxicDuration,
                        tickInterval: _toxicTickInterval,
                        damagePerTick: _toxicDamagePerTick,
                        source: this,
                        showDamagePopup: true,
                        tickVfxType: _tickVfxType,
                        vfxAutoReleaseTime: _vfxAutoReleaseTime
                    );
                    return true;
            }

            effect = default;
            return false;
        }
    }
}
