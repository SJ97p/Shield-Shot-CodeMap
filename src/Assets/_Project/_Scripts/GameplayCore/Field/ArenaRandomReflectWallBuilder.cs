using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shield_Shot.GameplayCore.Field
{
    public sealed class ArenaRandomReflectWallBuilder : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ArenaBoundaryBuilder _boundaryBuilder;
        [SerializeField] private ElementFieldGrid _fieldGrid;
        [SerializeField] private Transform _fieldSpace;
        [SerializeField] private Transform _wallRoot;
        [SerializeField] private GameObject _wallPrefab;

        [Header("Seed")]
        [SerializeField] private int _seed = 12345;
        [SerializeField] private bool _buildOnAwake;
        [SerializeField] private bool _randomizeSeedOnAwake;

        [Header("Wall Count")]
        [SerializeField, Min(0)] private int _minWallCount = 1;
        [SerializeField, Min(0)] private int _maxWallCount = 5;

        [Header("Cell Size")]
        [SerializeField, Min(1)] private int _minHorizontalCells = 4;
        [SerializeField, Min(1)] private int _maxHorizontalCells = 6;
        [SerializeField, Min(1)] private int _verticalCells = 2;

        [Header("Placement Rules")]
        [SerializeField, Min(0)] private int _edgePaddingCells = 2;
        [SerializeField, Min(0)] private int _reservedCellPadding = 3;
        [SerializeField, Min(0)] private int _wallSpacingCells = 1;
        [SerializeField, Min(1)] private int _maxPlacementAttemptsPerWall = 40;
        [SerializeField] private Vector2Int[] _reservedCells =
        {
            new Vector2Int(4, 2),
            new Vector2Int(13, 4),
            new Vector2Int(13, 43)
        };

        [Header("Wall Settings")]
        [SerializeField, Min(0.01f)] private float _wallHeight = 1.5f;
        [SerializeField] private bool _clearExistingGeneratedWalls = true;
        [SerializeField] private bool _overrideGeneratedWallLayer = true;
        [SerializeField] private string _generatedWallLayerName = "Wall";

        [Header("Material Tiling")]
        [SerializeField] private bool _fitGeneratedWallMaterialTiling = true;
        [SerializeField, Min(0.01f)] private float _wallMaterialWorldUnitsPerTile = 1f;

        private const string GeneratedWallPrefix = "GeneratedRandomReflectWall_";
        private readonly List<RectInt> _placedWallRects = new();

        private void Reset()
        {
            _boundaryBuilder = GetComponent<ArenaBoundaryBuilder>();
            _fieldGrid = FindFirstObjectByType<ElementFieldGrid>();
            _fieldSpace = _fieldGrid != null ? _fieldGrid.FieldSpace : transform;
            _wallRoot = _fieldSpace != null ? _fieldSpace : transform;
        }

        private void Awake()
        {
            if (_buildOnAwake)
            {
                int seed = _randomizeSeedOnAwake
                    ? UnityEngine.Random.Range(int.MinValue, int.MaxValue)
                    : _seed;

                BuildRandomWalls(seed);
            }
        }

        [ContextMenu("Build Random Reflect Walls")]
        public void BuildRandomWalls()
        {
            BuildRandomWalls(_seed);
        }

        public void BuildRandomWalls(int seed)
        {
            _seed = seed;
            ResolveReferences();

            if (_fieldGrid == null)
            {
                Debug.LogWarning("[ArenaRandomReflectWallBuilder] ElementFieldGrid is missing.");
                return;
            }

            if (_fieldSpace == null)
            {
                Debug.LogWarning("[ArenaRandomReflectWallBuilder] FieldSpace is missing.");
                return;
            }

            if (_wallPrefab == null)
            {
                Debug.LogWarning("[ArenaRandomReflectWallBuilder] Wall prefab is missing.");
                return;
            }

            if (_wallRoot == null)
            {
                _wallRoot = _fieldSpace;
            }

            if (_clearExistingGeneratedWalls)
            {
                ClearGeneratedWalls();
            }

            _placedWallRects.Clear();

            System.Random random = new System.Random(seed);
            int minCount = Mathf.Max(0, _minWallCount);
            int maxCount = Mathf.Max(minCount, _maxWallCount);
            int targetCount = random.Next(minCount, maxCount + 1);
            int generatedCount = 0;

            for (int i = 0; i < targetCount; i++)
            {
                if (!TryFindWallRect(random, out RectInt wallRect))
                {
                    continue;
                }

                CreateWall(generatedCount, wallRect);
                _placedWallRects.Add(wallRect);
                generatedCount++;
            }

            Debug.Log(
                $"[ArenaRandomReflectWallBuilder] Built random reflect walls. " +
                $"Seed: {seed}, Requested: {targetCount}, Generated: {generatedCount}");
        }

        public void ConfigureFromBoundaryBuilder(ArenaBoundaryBuilder boundaryBuilder)
        {
            if (boundaryBuilder == null)
            {
                return;
            }

            _boundaryBuilder = boundaryBuilder;
            ResolveReferences();
        }

        public void SetGeneratedWallLayerName(string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName))
            {
                return;
            }

            _generatedWallLayerName = layerName;
        }

        [ContextMenu("Clear Random Reflect Walls")]
        public void ClearGeneratedWalls()
        {
            ResolveReferences();

            if (_wallRoot == null)
            {
                return;
            }

            for (int i = _wallRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = _wallRoot.GetChild(i);
                if (!child.name.StartsWith(GeneratedWallPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            _placedWallRects.Clear();
        }

        private void ResolveReferences()
        {
            if (_boundaryBuilder == null)
            {
                _boundaryBuilder = GetComponent<ArenaBoundaryBuilder>();
            }

            if (_fieldGrid == null)
            {
                _fieldGrid = _boundaryBuilder != null && _boundaryBuilder.FieldGrid != null
                    ? _boundaryBuilder.FieldGrid
                    : GetComponent<ElementFieldGrid>();
            }

            if (_fieldGrid == null)
            {
                _fieldGrid = ElementFieldGrid.Instance != null
                    ? ElementFieldGrid.Instance
                    : FindFirstObjectByType<ElementFieldGrid>();
            }

            if (_fieldSpace == null && _fieldGrid != null)
            {
                _fieldSpace = _boundaryBuilder != null && _boundaryBuilder.FieldSpace != null
                    ? _boundaryBuilder.FieldSpace
                    : _fieldGrid.FieldSpace;
            }

            if (_wallRoot == null)
            {
                _wallRoot = _boundaryBuilder != null && _boundaryBuilder.WallRoot != null
                    ? _boundaryBuilder.WallRoot
                    : _fieldSpace != null ? _fieldSpace : transform;
            }

            if (_wallPrefab == null && _boundaryBuilder != null)
            {
                _wallPrefab = _boundaryBuilder.WallPrefab;
            }

            if (_boundaryBuilder != null && string.IsNullOrWhiteSpace(_generatedWallLayerName))
            {
                _generatedWallLayerName = _boundaryBuilder.GeneratedWallLayerName;
            }

            if (_boundaryBuilder != null)
            {
                _fitGeneratedWallMaterialTiling = _boundaryBuilder.FitGeneratedWallMaterialTiling;
                _wallMaterialWorldUnitsPerTile = _boundaryBuilder.WallMaterialWorldUnitsPerTile;
            }
        }

        private bool TryFindWallRect(System.Random random, out RectInt wallRect)
        {
            Vector2Int cellCount = _fieldGrid.CellCount;
            int horizontalCells = Mathf.Max(1, _maxHorizontalCells);
            int verticalCells = Mathf.Max(1, _verticalCells);
            int edgePadding = Mathf.Max(0, _edgePaddingCells);

            if (cellCount.x <= edgePadding * 2 + horizontalCells ||
                cellCount.y <= edgePadding * 2 + verticalCells)
            {
                wallRect = default;
                return false;
            }

            for (int attempt = 0; attempt < _maxPlacementAttemptsPerWall; attempt++)
            {
                int width = random.Next(
                    Mathf.Max(1, _minHorizontalCells),
                    Mathf.Max(_minHorizontalCells, _maxHorizontalCells) + 1);
                int height = verticalCells;
                int x = random.Next(edgePadding, cellCount.x - edgePadding - width + 1);
                int y = random.Next(edgePadding, cellCount.y - edgePadding - height + 1);

                RectInt candidate = new RectInt(x, y, width, height);
                if (!CanPlaceWall(candidate))
                {
                    continue;
                }

                wallRect = candidate;
                return true;
            }

            wallRect = default;
            return false;
        }

        private bool CanPlaceWall(RectInt candidate)
        {
            if (OverlapsReservedCells(candidate))
            {
                return false;
            }

            RectInt paddedCandidate = Expand(candidate, _wallSpacingCells);

            for (int i = 0; i < _placedWallRects.Count; i++)
            {
                if (Overlaps(paddedCandidate, _placedWallRects[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool OverlapsReservedCells(RectInt candidate)
        {
            RectInt paddedCandidate = Expand(candidate, _reservedCellPadding);

            if (_reservedCells == null)
            {
                return false;
            }

            for (int i = 0; i < _reservedCells.Length; i++)
            {
                Vector2Int reservedCell = _reservedCells[i];
                if (!_fieldGrid.IsValidCell(reservedCell))
                {
                    continue;
                }

                if (paddedCandidate.Contains(reservedCell))
                {
                    return true;
                }
            }

            return false;
        }

        private void CreateWall(int index, RectInt wallRect)
        {
            GameObject wall = Instantiate(_wallPrefab, _wallRoot);
            wall.name = $"{GeneratedWallPrefix}{index}_{wallRect.x}_{wallRect.y}_{wallRect.width}x{wallRect.height}";
            wall.transform.localRotation = Quaternion.identity;
            Vector3 localScale = GetWallLocalScale(wallRect);
            wall.transform.localPosition = GetWallLocalPosition(wallRect);
            wall.transform.localScale = localScale;
            ApplyGeneratedWallLayer(wall);
            ApplyGeneratedWallMaterialTiling(wall, localScale);
        }

        private void ApplyGeneratedWallMaterialTiling(GameObject wall, Vector3 localScale)
        {
            if (!_fitGeneratedWallMaterialTiling || wall == null)
            {
                return;
            }

            GeneratedWallMaterialTiling tiling = wall.GetComponent<GeneratedWallMaterialTiling>();
            if (tiling == null)
            {
                tiling = wall.AddComponent<GeneratedWallMaterialTiling>();
            }

            tiling.Apply(localScale, _wallMaterialWorldUnitsPerTile);
        }

        private Vector3 GetWallLocalPosition(RectInt wallRect)
        {
            Vector2 fieldSize = _fieldGrid.FieldWorldSize;
            Vector2 cellSize = _fieldGrid.CellWorldSize;

            float centerX = -fieldSize.x * 0.5f + (wallRect.x + wallRect.width * 0.5f) * cellSize.x;
            float centerZ = -fieldSize.y * 0.5f + (wallRect.y + wallRect.height * 0.5f) * cellSize.y;

            return new Vector3(centerX, _wallHeight * 0.5f, centerZ);
        }

        private Vector3 GetWallLocalScale(RectInt wallRect)
        {
            Vector2 cellSize = _fieldGrid.CellWorldSize;
            return new Vector3(
                wallRect.width * cellSize.x,
                _wallHeight,
                wallRect.height * cellSize.y
            );
        }

        private void ApplyGeneratedWallLayer(GameObject wall)
        {
            if (!_overrideGeneratedWallLayer || wall == null || string.IsNullOrWhiteSpace(_generatedWallLayerName))
            {
                return;
            }

            int layer = ResolveGeneratedWallLayer();
            if (layer < 0)
            {
                Debug.LogWarning($"[ArenaRandomReflectWallBuilder] Wall layer not found. Requested: {_generatedWallLayerName}");
                return;
            }

            SetLayerRecursively(wall.transform, layer);
        }

        private int ResolveGeneratedWallLayer()
        {
            int configuredLayer = LayerMask.NameToLayer(_generatedWallLayerName);
            if (configuredLayer >= 0)
            {
                return configuredLayer;
            }

            int pvpWallLayer = LayerMask.NameToLayer("PvpWall");
            if (pvpWallLayer >= 0)
            {
                return pvpWallLayer;
            }

            int pvpwallLayer = LayerMask.NameToLayer("Pvpwall");
            if (pvpwallLayer >= 0)
            {
                return pvpwallLayer;
            }

            return LayerMask.NameToLayer("Wall");
        }

        private static RectInt Expand(RectInt rect, int padding)
        {
            int safePadding = Mathf.Max(0, padding);
            return new RectInt(
                rect.x - safePadding,
                rect.y - safePadding,
                rect.width + safePadding * 2,
                rect.height + safePadding * 2
            );
        }

        private static bool Overlaps(RectInt a, RectInt b)
        {
            return a.xMin < b.xMax &&
                   a.xMax > b.xMin &&
                   a.yMin < b.yMax &&
                   a.yMax > b.yMin;
        }

        private static void SetLayerRecursively(Transform target, int layer)
        {
            target.gameObject.layer = layer;

            for (int i = 0; i < target.childCount; i++)
            {
                SetLayerRecursively(target.GetChild(i), layer);
            }
        }
    }
}
