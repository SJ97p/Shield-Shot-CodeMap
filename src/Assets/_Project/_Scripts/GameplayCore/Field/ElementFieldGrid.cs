using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shield_Shot.GameplayCore.Field
{
    public class ElementFieldGrid : MonoBehaviour
    {
        public static ElementFieldGrid Instance { get; private set; }

        public event Action<Vector2Int, ElementFieldCellData, ElementReactionResult> CellChanged;
        public event Action<Vector2Int, ElementFieldCellData> CellCleared;
        public event Action<Vector2Int, ElementFieldCellData> TerrainChanged;

        [Header("Design Space")]
        [SerializeField] private Vector2Int _designResolution = new Vector2Int(1080, 1920);
        [SerializeField] private Vector2Int _cellCount = new Vector2Int(27, 48);
        [SerializeField, Min(1f)] private float _pixelsPerWorldUnit = 100f;
        [SerializeField] private Transform _fieldSpace;

        [Header("Cell")]
        [SerializeField] private ElementFieldCell _cellPrefab;
        [SerializeField] private bool _generateCellObjects = false;
        [SerializeField] private bool _syncDebugCellObjects = true;

        [Header("Generated Cells")]
        [SerializeField] private Transform _cellRoot;

        [Header("Terrain Provider")]
        [SerializeField] private ElementFieldTerrainProvider _terrainProvider;

        [Header("Terrain Rules")]
        [SerializeField] private bool _useTerrainRules = true;
        [SerializeField, Range(0f, 0.5f)] private float _sandEdgeBand = 0.08f;
        [SerializeField] private Rect _waterRect = new Rect(0.4f, 0.45f, 0.2f, 0.1f);
        [SerializeField] private Rect _grassRect = new Rect(0.15f, 0.15f, 0.2f, 0.2f);

        [Header("Reaction Debug")]
        [SerializeField] private bool _logReactionPaint = true;

        [Header("Ongoing Reactions")]
        [SerializeField] private bool _refreshHotWindFireWhileActive = true;
        [SerializeField, Min(0.05f)] private float _hotWindFireRefreshInterval = 0.35f;
        [SerializeField, Min(0.05f)] private float _hotWindFireBaseDuration = 0.9f;

        private readonly Dictionary<Vector2Int, ElementFieldCell> _cells = new();
        private readonly Dictionary<Vector2Int, float> _nextHotWindFireRefreshTimeByCell = new();
        private readonly HashSet<Vector2Int> _activeCells = new();
        private readonly List<Vector2Int> _activeCellBuffer = new();
        private readonly List<Vector2Int> _spreadCellBuffer = new();
        private readonly Queue<Vector2Int> _terrainFloodQueue = new();
        private readonly HashSet<Vector2Int> _terrainFloodVisited = new();
        private ElementFieldCellData[,] _cellData;

        public Vector2 FieldWorldSize => new Vector2(
            _designResolution.x / _pixelsPerWorldUnit,
            _designResolution.y / _pixelsPerWorldUnit
        );

        public Vector2Int CellCount => _cellCount;
        public Transform FieldSpace => _fieldSpace != null ? _fieldSpace : transform;
        public Vector3 FieldCenter => FieldSpace.TransformPoint(Vector3.zero);

        public Vector2 CellWorldSize => new Vector2(
            FieldWorldSize.x / Mathf.Max(1, _cellCount.x),
            FieldWorldSize.y / Mathf.Max(1, _cellCount.y)
        );

        public float CellSize => Mathf.Min(CellWorldSize.x, CellWorldSize.y);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (_fieldSpace == null)
            {
                _fieldSpace = transform;
            }

            if (_cellRoot == null)
            {
                _cellRoot = transform;
            }

            if (_terrainProvider == null)
            {
                _terrainProvider = FindFirstObjectByType<ElementFieldTerrainProvider>();
            }

            BuildGrid();
        }

        private void Update()
        {
            TickActiveCells(Time.deltaTime);
        }
        private void Reset()
        {
            _terrainProvider = FindFirstObjectByType<ElementFieldTerrainProvider>();
        }
        private void BuildGrid()
        {
            _cells.Clear();
            _activeCells.Clear();
            _activeCellBuffer.Clear();
            _terrainFloodQueue.Clear();
            _terrainFloodVisited.Clear();
            _nextHotWindFireRefreshTimeByCell.Clear();
            RefreshTerrainProviderAreas();
            _cellData = new ElementFieldCellData[
                Mathf.Max(1, _cellCount.x),
                Mathf.Max(1, _cellCount.y)
            ];

            for (int x = 0; x < _cellData.GetLength(0); x++)
            {
                for (int y = 0; y < _cellData.GetLength(1); y++)
                {
                    Vector2Int coord = new Vector2Int(x, y);
                    _cellData[x, y] = new ElementFieldCellData(coord, ResolveTerrain(coord));
                }
            }

            if (!_generateCellObjects)
            {
                return;
            }

            if (_cellPrefab == null)
            {
                Debug.LogWarning("[ElementFieldGrid] Cell prefab is missing.");
                return;
            }

            for (int x = 0; x < _cellCount.x; x++)
            {
                for (int y = 0; y < _cellCount.y; y++)
                {
                    Vector2Int coord = new Vector2Int(x, y);
                    CreateDebugCellObject(coord);
                }
            }
        }

        public void Paint(Vector3 worldPosition, ElementType element, float duration)
        {
            Paint(worldPosition, new ElementPaintContext(element), duration);
        }

        public void Paint(Vector3 worldPosition, ElementPaintContext paintContext, float duration)
        {
            Vector2Int coord = WorldToCell(worldPosition);

            if (_logReactionPaint)
            {
                string terrainText = TryGetCellData(coord, out ElementFieldCellData data)
                    ? data.TerrainElement.ToString()
                    : "Invalid";

                Debug.Log(
                    $"[ElementFieldGrid] Paint request. World: {worldPosition}, Cell: {coord}, " +
                    $"Terrain: {terrainText}, Element: {paintContext.Element}, " +
                    $"Level: {paintContext.ElementLevel}, Duration: {duration:0.00}"
                );
            }

            PaintCell(coord, paintContext, duration);
        }

        public void PaintCircle(Vector3 worldPosition, ElementType element, float duration, float radius)
        {
            PaintCircle(worldPosition, new ElementPaintContext(element), duration, radius);
        }
        public void SetTerrainCell(Vector2Int coord, TerrainElementType terrain)
        {
            if (!IsValidCell(coord) || _cellData == null)
            {
                return;
            }

            ElementFieldCellData data = _cellData[coord.x, coord.y];
            data.TerrainElement = terrain;
            _cellData[coord.x, coord.y] = data;

            if (_syncDebugCellObjects && _cells.TryGetValue(coord, out ElementFieldCell cell))
            {
                cell.SetTerrainElement(terrain);
            }

            TerrainChanged?.Invoke(coord, data);
        }
        public void FillTerrain(TerrainElementType terrain)
        {
            if (_cellData == null)
            {
                return;
            }

            for (int x = 0; x < _cellData.GetLength(0); x++)
            {
                for (int y = 0; y < _cellData.GetLength(1); y++)
                {
                    SetTerrainCell(new Vector2Int(x, y), terrain);
                }
            }
        }

        public void PaintCircle(Vector3 worldPosition, ElementPaintContext paintContext, float duration, float radius)
        {
            float cellSize = Mathf.Min(CellWorldSize.x, CellWorldSize.y);
            int cellRadius = Mathf.CeilToInt(radius / cellSize);
            Vector2Int center = WorldToCell(worldPosition);
            int candidateCells = 0;
            int validCells = 0;
            int grassCells = 0;

            for (int x = -cellRadius; x <= cellRadius; x++)
            {
                for (int y = -cellRadius; y <= cellRadius; y++)
                {
                    Vector2Int coord = center + new Vector2Int(x, y);
                    Vector3 cellPosition = CellToWorld(coord);

                    Vector3 localCellPosition = FieldSpace.InverseTransformPoint(cellPosition);
                    Vector3 localPaintPosition = FieldSpace.InverseTransformPoint(worldPosition);
                    Vector2 flatDelta = new Vector2(
                        localCellPosition.x - localPaintPosition.x,
                        localCellPosition.z - localPaintPosition.z
                    );

                    if (flatDelta.sqrMagnitude > radius * radius)
                    {
                        continue;
                    }

                    candidateCells++;

                    if (TryGetCellData(coord, out ElementFieldCellData data))
                    {
                        validCells++;

                        if (data.TerrainElement == TerrainElementType.Grass)
                        {
                            grassCells++;
                        }
                    }

                    PaintCell(coord, paintContext, duration);
                }
            }

            if (_logReactionPaint)
            {
                Debug.Log(
                    $"[ElementFieldGrid] PaintCircle request. World: {worldPosition}, CenterCell: {center}, " +
                    $"Radius: {radius:0.00}, CellRadius: {cellRadius}, CandidateCells: {candidateCells}, " +
                    $"ValidCells: {validCells}, GrassCells: {grassCells}, Element: {paintContext.Element}, " +
                    $"Level: {paintContext.ElementLevel}, Duration: {duration:0.00}"
                );
            }
        }

        public bool TryGetCellData(Vector2Int coord, out ElementFieldCellData data)
        {
            if (!IsValidCell(coord) || _cellData == null)
            {
                data = default;
                return false;
            }

            data = _cellData[coord.x, coord.y];
            return true;
        }

        public bool TryGetCellData(Vector3 worldPosition, out ElementFieldCellData data)
        {
            return TryGetCellData(WorldToCell(worldPosition), out data);
        }

        public bool TryGetCell(Vector3 worldPosition, out ElementFieldCell cell)
        {
            Vector2Int coord = WorldToCell(worldPosition);
            return _cells.TryGetValue(coord, out cell);
        }
        public void Clear(Vector3 worldPosition)
        {
            ClearCell(WorldToCell(worldPosition));
        }

        public void ClearCellAt(Vector2Int coord)
        {
            ClearCell(coord);
        }

        public void ClearCircle(Vector3 worldPosition, float radius)
        {
            float cellSize = Mathf.Min(CellWorldSize.x, CellWorldSize.y);
            int cellRadius = Mathf.CeilToInt(radius / cellSize);
            Vector2Int center = WorldToCell(worldPosition);

            for (int x = -cellRadius; x <= cellRadius; x++)
            {
                for (int y = -cellRadius; y <= cellRadius; y++)
                {
                    Vector2Int coord = center + new Vector2Int(x, y);
                    Vector3 cellPosition = CellToWorld(coord);

                    Vector3 localCellPosition = FieldSpace.InverseTransformPoint(cellPosition);
                    Vector3 localClearPosition = FieldSpace.InverseTransformPoint(worldPosition);
                    Vector2 flatDelta = new Vector2(
                        localCellPosition.x - localClearPosition.x,
                        localCellPosition.z - localClearPosition.z
                    );

                    if (flatDelta.sqrMagnitude > radius * radius)
                    {
                        continue;
                    }

                    ClearCell(coord);
                }
            }
        }

        public void ClearAll()
        {
            if (_cellData == null)
            {
                return;
            }

            _activeCellBuffer.Clear();
            _activeCellBuffer.AddRange(_activeCells);

            for (int i = 0; i < _activeCellBuffer.Count; i++)
            {
                ClearCell(_activeCellBuffer[i]);
            }

            _activeCells.Clear();
            _activeCellBuffer.Clear();
        }

        public bool IsValidCell(Vector2Int coord)
        {
            return coord.x >= 0 &&
                   coord.x < _cellCount.x &&
                   coord.y >= 0 &&
                   coord.y < _cellCount.y;
        }

        public Vector2Int ClampCell(Vector2Int coord)
        {
            return new Vector2Int(
                Mathf.Clamp(coord.x, 0, Mathf.Max(0, _cellCount.x - 1)),
                Mathf.Clamp(coord.y, 0, Mathf.Max(0, _cellCount.y - 1))
            );
        }

        public bool TryCellToWorld(Vector2Int coord, out Vector3 worldPosition)
        {
            if (!IsValidCell(coord))
            {
                worldPosition = default;
                return false;
            }

            worldPosition = CellToWorld(coord);
            return true;
        }

        public Vector2Int WorldToCell(Vector3 worldPosition)
        {
            Vector3 local = FieldSpace.InverseTransformPoint(worldPosition);
            Vector2 fieldSize = FieldWorldSize;

            float normalizedX = (local.x / fieldSize.x) + 0.5f;
            float normalizedY = (local.z / fieldSize.y) + 0.5f;

            int x = Mathf.FloorToInt(normalizedX * _cellCount.x);
            int y = Mathf.FloorToInt(normalizedY * _cellCount.y);

            return new Vector2Int(x, y);
        }

        public Vector3 CellToWorld(Vector2Int coord)
        {
            Vector2 fieldSize = FieldWorldSize;
            Vector2 cellSize = CellWorldSize;

            Vector3 localPosition = new Vector3(
                -fieldSize.x * 0.5f + (coord.x + 0.5f) * cellSize.x,
                0f,
                -fieldSize.y * 0.5f + (coord.y + 0.5f) * cellSize.y
            );

            return FieldSpace.TransformPoint(localPosition);
        }

        public Vector2 CellToDesignPosition(Vector2Int coord)
        {
            return new Vector2(
                (coord.x + 0.5f) * (_designResolution.x / Mathf.Max(1f, _cellCount.x)),
                (coord.y + 0.5f) * (_designResolution.y / Mathf.Max(1f, _cellCount.y))
            );
        }

        public TerrainElementType ResolveTerrain(Vector2Int coord)
        {
            if (_terrainProvider != null && TryCellToWorld(coord, out Vector3 worldPosition))
            {
                return _terrainProvider.GetTerrain(worldPosition);
            }

            if (!_useTerrainRules)
            {
                return TerrainElementType.None;
            }

            Vector2 normalized = new Vector2(
                (coord.x + 0.5f) / Mathf.Max(1f, _cellCount.x),
                (coord.y + 0.5f) / Mathf.Max(1f, _cellCount.y)
            );

            if (IsInsideRect(normalized, _waterRect))
            {
                return TerrainElementType.Water;
            }

            if (IsInsideRect(normalized, _grassRect))
            {
                return TerrainElementType.Grass;
            }

            if (normalized.x < _sandEdgeBand ||
                normalized.x > 1f - _sandEdgeBand ||
                normalized.y < _sandEdgeBand ||
                normalized.y > 1f - _sandEdgeBand)
            {
                return TerrainElementType.Sand;
            }

            return TerrainElementType.None;
        }

        private static bool IsInsideRect(Vector2 point, Rect rect)
        {
            return point.x >= rect.xMin &&
                   point.x <= rect.xMax &&
                   point.y >= rect.yMin &&
                   point.y <= rect.yMax;
        }

        private void PaintCell(Vector2Int coord, ElementType incomingElement, float duration)
        {
            PaintCell(coord, new ElementPaintContext(incomingElement), duration, true);
        }

        private void PaintCell(Vector2Int coord, ElementPaintContext paintContext, float duration)
        {
            PaintCell(coord, paintContext, duration, true);
        }

        private void PaintCell(Vector2Int coord, ElementPaintContext paintContext, float duration, bool allowSpread)
        {
            if (!IsValidCell(coord) || _cellData == null || paintContext.Element == ElementType.None)
            {
                return;
            }

            ElementFieldCellData data = _cellData[coord.x, coord.y];
            ElementReactionResult reaction = ElementReactionResolver.Resolve(
                data.TerrainElement,
                data.CurrentElement,
                paintContext
            );

            ElementType resultElement = reaction.HasReaction
                ? reaction.ResultElement
                : paintContext.Element;

            if (resultElement == ElementType.None)
            {
                ClearCell(coord);
                return;
            }

            if (reaction.ReactionType == ElementReactionType.Freeze &&
                data.TerrainElement == TerrainElementType.Water)
            {
                FreezeConnectedWaterTerrain(coord, paintContext, duration, reaction);
                return;
            }

            ElementType previousElement = data.CurrentElement;
            data.CurrentElement = resultElement;
            data.ElementLevel = previousElement == resultElement
                ? Mathf.Max(data.ElementLevel, paintContext.ElementLevel)
                : paintContext.ElementLevel;
            data.RemainingTime = Mathf.Max(data.RemainingTime, duration * Mathf.Max(0f, reaction.DurationMultiplier));
            data.LastReactionType = reaction.ReactionType;
            _cellData[coord.x, coord.y] = data;
            _activeCells.Add(coord);

            if (_syncDebugCellObjects && _cells.TryGetValue(coord, out ElementFieldCell cell))
            {
                cell.ApplyElement(paintContext.Element, duration);
            }

            CellChanged?.Invoke(coord, data, reaction);

            if (_logReactionPaint && reaction.ReactionType != ElementReactionType.Ignite)
            {
                Debug.Log(
                    $"[ElementFieldGrid] Paint reaction {reaction.ReactionType} at {coord}. " +
                    $"Terrain: {data.TerrainElement}, Incoming: {paintContext.Element}, " +
                    $"Level: {paintContext.ElementLevel}, Result: {resultElement}, Duration: {data.RemainingTime:0.00}, " +
                    $"SpreadCellRadius: {reaction.SpreadCellRadius}, " +
                    $"SpreadDelay: {reaction.SpreadDelay:0.00}"
                );
            }

            if (allowSpread && reaction.SpreadCellRadius > 0)
            {
                ElementType spreadElement = reaction.SpreadElement != ElementType.None
                    ? reaction.SpreadElement
                    : resultElement;
                PaintReactionSpread(coord, paintContext, spreadElement, duration, reaction);
            }
        }

        private void RefreshTerrainProviderAreas()
        {
            if (_terrainProvider == null)
            {
                return;
            }

            _terrainProvider.RefreshAreas();
        }

        private void PaintReactionSpread(
            Vector2Int origin,
            ElementPaintContext sourceContext,
            ElementType element,
            float sourceDuration,
            ElementReactionResult reaction)
        {
            int cellRadius = Mathf.Max(0, reaction.SpreadCellRadius);
            float spreadDuration = sourceDuration * Mathf.Max(0f, reaction.SpreadDurationMultiplier);
            int spreadCount = 0;
            _spreadCellBuffer.Clear();

            for (int x = -cellRadius; x <= cellRadius; x++)
            {
                for (int y = -cellRadius; y <= cellRadius; y++)
                {
                    Vector2Int coord = origin + new Vector2Int(x, y);

                    if (coord == origin ||
                        !IsValidCell(coord) ||
                        !CanSpreadToCell(reaction, coord))
                    {
                        continue;
                    }

                    _spreadCellBuffer.Add(coord);
                    spreadCount++;
                }
            }

            _spreadCellBuffer.Sort((left, right) =>
            {
                int leftDistance = Mathf.Abs(left.x - origin.x) + Mathf.Abs(left.y - origin.y);
                int rightDistance = Mathf.Abs(right.x - origin.x) + Mathf.Abs(right.y - origin.y);
                return leftDistance.CompareTo(rightDistance);
            });

            if (_spreadCellBuffer.Count > 0)
            {
                StartCoroutine(PaintReactionSpreadRoutine(
                    _spreadCellBuffer.ToArray(),
                    new ElementPaintContext(
                        element,
                        sourceContext.ElementLevel,
                        sourceContext.PowerMultiplier,
                        sourceContext.Source
                    ),
                    spreadDuration,
                    reaction.SpreadDelay,
                    reaction
                ));
            }

            if (_logReactionPaint)
            {
                Debug.Log(
                    $"[ElementFieldGrid] Spread {reaction.ReactionType} from {origin}. " +
                    $"CellRadius: {cellRadius}, PaintedCells: {spreadCount}"
                );
            }
        }

        private IEnumerator PaintReactionSpreadRoutine(
            Vector2Int[] spreadCells,
            ElementPaintContext paintContext,
            float spreadDuration,
            float spreadDelay,
            ElementReactionResult reaction)
        {
            for (int i = 0; i < spreadCells.Length; i++)
            {
                if (spreadDelay > 0f)
                {
                    yield return new WaitForSeconds(spreadDelay);
                }

                if (!CanSpreadToCell(reaction, spreadCells[i]))
                {
                    continue;
                }

                PaintCell(spreadCells[i], paintContext, spreadDuration, false);
            }
        }

        private bool CanSpreadToCell(ElementReactionResult reaction, Vector2Int coord)
        {
            if (!TryGetCellData(coord, out ElementFieldCellData data))
            {
                return false;
            }

            if (reaction.ReactionType == ElementReactionType.SpreadFire)
            {
                return data.TerrainElement == TerrainElementType.Grass;
            }

            if (reaction.ReactionType == ElementReactionType.HotWind)
            {
                return data.CurrentElement != ElementType.Wind;
            }

            return true;
        }

        private void ClearCell(Vector2Int coord)
        {
            if (!IsValidCell(coord) || _cellData == null)
            {
                return;
            }

            ElementFieldCellData data = _cellData[coord.x, coord.y];
            data.Clear();
            _cellData[coord.x, coord.y] = data;
            _activeCells.Remove(coord);
            _nextHotWindFireRefreshTimeByCell.Remove(coord);

            if (_syncDebugCellObjects && _cells.TryGetValue(coord, out ElementFieldCell cell))
            {
                cell.ClearElement();
            }

            CellCleared?.Invoke(coord, data);
        }

        private void TickActiveCells(float deltaTime)
        {
            if (_activeCells.Count == 0 || _cellData == null)
            {
                return;
            }

            _activeCellBuffer.Clear();
            _activeCellBuffer.AddRange(_activeCells);

            for (int i = 0; i < _activeCellBuffer.Count; i++)
            {
                Vector2Int coord = _activeCellBuffer[i];

                if (!IsValidCell(coord))
                {
                    _activeCells.Remove(coord);
                    continue;
                }

                ElementFieldCellData data = _cellData[coord.x, coord.y];

                if (!data.IsActive)
                {
                    ClearCell(coord);
                    continue;
                }

                data.RemainingTime -= deltaTime;

                if (data.RemainingTime <= 0f)
                {
                    ClearCell(coord);
                    continue;
                }

                _cellData[coord.x, coord.y] = data;
                TickOngoingHotWindFire(coord, data);
            }
        }

        private void TickOngoingHotWindFire(Vector2Int origin, ElementFieldCellData data)
        {
            if (!_refreshHotWindFireWhileActive ||
                data.CurrentElement != ElementType.Wind ||
                data.LastReactionType != ElementReactionType.HotWind)
            {
                return;
            }

            if (_nextHotWindFireRefreshTimeByCell.TryGetValue(origin, out float nextRefreshTime) &&
                Time.time < nextRefreshTime)
            {
                return;
            }

            ElementPaintContext windContext = new ElementPaintContext(
                ElementType.Wind,
                data.ElementLevel
            );
            ElementReactionResult hotWindReaction = ElementReactionResolver.Resolve(
                data.TerrainElement,
                data.CurrentElement,
                windContext
            );

            if (hotWindReaction.ReactionType != ElementReactionType.HotWind ||
                hotWindReaction.SpreadCellRadius <= 0)
            {
                return;
            }

            _nextHotWindFireRefreshTimeByCell[origin] = Time.time + _hotWindFireRefreshInterval;

            float fireDuration = _hotWindFireBaseDuration *
                Mathf.Max(0f, hotWindReaction.SpreadDurationMultiplier);
            ElementPaintContext fireContext = new ElementPaintContext(
                ElementType.Fire,
                data.ElementLevel
            );

            int cellRadius = Mathf.Max(0, hotWindReaction.SpreadCellRadius);
            for (int x = -cellRadius; x <= cellRadius; x++)
            {
                for (int y = -cellRadius; y <= cellRadius; y++)
                {
                    Vector2Int coord = origin + new Vector2Int(x, y);

                    if (coord == origin || !CanSpreadToCell(hotWindReaction, coord))
                    {
                        continue;
                    }

                    PaintCell(coord, fireContext, fireDuration, false);
                }
            }
        }

        private void FreezeConnectedWaterTerrain(
            Vector2Int origin,
            ElementPaintContext paintContext,
            float duration,
            ElementReactionResult reaction)
        {
            _terrainFloodQueue.Clear();
            _terrainFloodVisited.Clear();

            _terrainFloodQueue.Enqueue(origin);
            _terrainFloodVisited.Add(origin);

            float freezeDuration = duration * Mathf.Max(0f, reaction.DurationMultiplier);

            while (_terrainFloodQueue.Count > 0)
            {
                Vector2Int coord = _terrainFloodQueue.Dequeue();

                if (!TryGetCellData(coord, out ElementFieldCellData data) ||
                    data.TerrainElement != TerrainElementType.Water)
                {
                    continue;
                }

                ElementType previousElement = data.CurrentElement;
                data.TerrainElement = TerrainElementType.Ice;
                data.CurrentElement = ElementType.Ice;
                data.ElementLevel = previousElement == ElementType.Ice
                    ? Mathf.Max(data.ElementLevel, paintContext.ElementLevel)
                    : paintContext.ElementLevel;
                data.RemainingTime = Mathf.Max(data.RemainingTime, freezeDuration);
                data.LastReactionType = ElementReactionType.Freeze;

                _cellData[coord.x, coord.y] = data;
                _activeCells.Add(coord);

                if (_syncDebugCellObjects && _cells.TryGetValue(coord, out ElementFieldCell cell))
                {
                    cell.SetTerrainElement(TerrainElementType.Ice);
                    cell.ApplyElement(ElementType.Ice, freezeDuration);
                }

                TerrainChanged?.Invoke(coord, data);
                CellChanged?.Invoke(coord, data, reaction);

                EnqueueWaterNeighbor(coord + Vector2Int.right);
                EnqueueWaterNeighbor(coord + Vector2Int.left);
                EnqueueWaterNeighbor(coord + Vector2Int.up);
                EnqueueWaterNeighbor(coord + Vector2Int.down);
            }
        }

        private void EnqueueWaterNeighbor(Vector2Int coord)
        {
            if (_terrainFloodVisited.Contains(coord) ||
                !TryGetCellData(coord, out ElementFieldCellData data) ||
                data.TerrainElement != TerrainElementType.Water)
            {
                return;
            }

            _terrainFloodVisited.Add(coord);
            _terrainFloodQueue.Enqueue(coord);
        }

        private void CreateDebugCellObject(Vector2Int coord)
        {
            Vector3 worldPosition = CellToWorld(coord);

            ElementFieldCell cell = Instantiate(
                _cellPrefab,
                worldPosition,
                _fieldSpace.rotation,
                _cellRoot
            );

            cell.name = $"ElementCell_{coord.x}_{coord.y}";
            cell.Initialize(coord, ResolveTerrain(coord), CellWorldSize);
            _cells.Add(coord, cell);
        }

        [ContextMenu("Refresh Terrain Data")]
        public void RefreshTerrainData()
        {
            if (_terrainProvider != null)
            {
                _terrainProvider.RefreshAreas();
            }

            if (_cellData == null)
            {
                return;
            }

            for (int x = 0; x < _cellData.GetLength(0); x++)
            {
                for (int y = 0; y < _cellData.GetLength(1); y++)
                {
                    Vector2Int coord = new Vector2Int(x, y);
                    ElementFieldCellData data = _cellData[x, y];

                    data.TerrainElement = ResolveTerrain(coord);
                    _cellData[x, y] = data;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Transform space = _fieldSpace != null ? _fieldSpace : transform;
            Vector2 fieldSize = new Vector2(
                _designResolution.x / Mathf.Max(1f, _pixelsPerWorldUnit),
                _designResolution.y / Mathf.Max(1f, _pixelsPerWorldUnit)
            );

            Vector3 bottomLeft = space.TransformPoint(new Vector3(-fieldSize.x * 0.5f, 0f, -fieldSize.y * 0.5f));
            Vector3 bottomRight = space.TransformPoint(new Vector3(fieldSize.x * 0.5f, 0f, -fieldSize.y * 0.5f));
            Vector3 topRight = space.TransformPoint(new Vector3(fieldSize.x * 0.5f, 0f, fieldSize.y * 0.5f));
            Vector3 topLeft = space.TransformPoint(new Vector3(-fieldSize.x * 0.5f, 0f, fieldSize.y * 0.5f));

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(bottomLeft, bottomRight);
            Gizmos.DrawLine(bottomRight, topRight);
            Gizmos.DrawLine(topRight, topLeft);
            Gizmos.DrawLine(topLeft, bottomLeft);
        }
    }
}
