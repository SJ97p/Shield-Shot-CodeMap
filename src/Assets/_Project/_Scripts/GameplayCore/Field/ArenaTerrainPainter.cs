using Shield_Shot.GameplayCore.Monster.Difficulty;
using Shield_Shot.GameplayCore.Monster.Stage;
using System.Collections.Generic;
using UnityEngine;

namespace Shield_Shot.GameplayCore.Field
{
    public class ArenaTerrainPainter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ElementFieldGrid _fieldGrid;
        [SerializeField] private Terrain _terrain;
        [SerializeField] private ArenaTerrainThemeSO _theme;
        [SerializeField] private List<ArenaTerrainThemeSO> _themeDB;

        [Header("Generate")]
        [SerializeField] private bool _fillGridWithBaseTerrain = true;
        [SerializeField] private bool _resizeTerrainToField = true;
        [SerializeField] private bool _alignSurfaceToFieldPlane = true;
        [SerializeField] private float _targetSurfaceYOffset;
        [SerializeField, Min(16)] private int _alphamapResolution = 256;

        [Header("Theme Generation")]
        [SerializeField] private int _seed = 12345;
        [SerializeField] private bool _randomizeSeed;

        [Header("Test Pond")]
        [SerializeField] private Vector2Int _pondCenterCell = new Vector2Int(-1, -1);
        [SerializeField, Min(1)] private int _pondRadiusCells = 3;
        [SerializeField, Min(0)] private int _pondBankWidthCells = 1;
        [SerializeField] private bool _usePondBankTerrain;
        [SerializeField] private TerrainElementType _pondBankTerrain = TerrainElementType.Mud;
        [SerializeField, Min(0f)] private float _pondDepth = 0.45f;
        [SerializeField, Range(0f, 1f)] private float _pondFloorBlend = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _pondEdgeNoise = 0.35f;
        [SerializeField] private bool _autoRaiseBaseHeightForPonds = true;
        [SerializeField, Range(0f, 1f)] private float _baseHeight = 0f;
        [SerializeField] private Transform _waterSurfaceRoot;

        [Header("Water Surface")]
        [SerializeField] private bool _useProceduralWaterSurface = true;
        [SerializeField, Range(8, 96)] private int _waterSurfaceSegments = 40;
        [SerializeField, Range(0f, 0.5f)] private float _waterSurfaceEdgeNoise = 0.18f;
        [SerializeField, Range(0f, 1f)] private float _waterFillDepthRatio = 0.55f;
        [SerializeField] private Material _iceWaterSurfaceMaterial;

        private readonly Dictionary<TerrainElementType, int> _layerIndexByTerrain = new();
        private readonly List<GameObject> _spawnedWaterSurfaces = new();
        private readonly List<Vector2Int> _generatedPondCenters = new();
        private readonly List<int> _generatedPondRadii = new();

        private void Reset()
        {
            _fieldGrid = FindFirstObjectByType<ElementFieldGrid>();
            _terrain = GetComponent<Terrain>();

            if (_terrain == null)
            {
                _terrain = FindFirstObjectByType<Terrain>();
            }
        }

        private void OnEnable()
        {
            ElementFieldGrid grid = ResolveGrid();
            if (grid != null)
            {
                grid.TerrainChanged += HandleTerrainChanged;
            }
        }

        private void OnDisable()
        {
            ElementFieldGrid grid = ResolveGrid();
            if (grid != null)
            {
                grid.TerrainChanged -= HandleTerrainChanged;
            }
        }

        [ContextMenu("Generate Theme Terrain")]
        public void GenerateThemeTerrain()
        {
            ElementFieldGrid grid = ResolveGrid();
            TerrainData terrainData = PrepareTerrainForGeneration(grid, resetGridTerrain: true);

            if (grid == null || terrainData == null)
            {
                return;
            }

            int seed = _randomizeSeed ? Random.Range(int.MinValue, int.MaxValue) : _seed;
            Random.InitState(seed);

            int pondCount = GeneratePondsFromTheme(grid);

            PaintAlphamap(grid, terrainData);
            SculptWaterCells(grid, terrainData);
            RebuildGeneratedWaterSurfaces(grid, terrainData);

            Debug.Log(
                $"[ArenaTerrainPainter] Generated theme terrain. " +
                $"Seed: {seed}, Ponds: {pondCount}"
            );
        }


        public void ValidateTheme()
        {
            if (_themeDB == null || _themeDB.Count == 0)
            {
                return;
            }

            switch (StageManager.Instance.CurrentBiom)
            {
                case ChapterBiom.Grassland:
                    _theme = _themeDB.Find(theme => theme.ThemeName == "Grassland");
                    break;
                case ChapterBiom.Desert:
                    _theme = _themeDB.Find(theme => theme.ThemeName == "Desert");
                    break;
                case ChapterBiom.Swamp:
                    _theme = _themeDB.Find(theme => theme.ThemeName == "Swamp");
                    break;
            }

            GenerateThemeTerrain();
        }

        public void GenerateThemeTerrain(int seed)
        {
            _seed = seed;
            _randomizeSeed = false;
            GenerateThemeTerrain();
        }

        private int GeneratePondsFromTheme(ElementFieldGrid grid)
        {
            ArenaTerrainThemeSO.PondRule rule = _theme.Pond;
            _generatedPondCenters.Clear();
            _generatedPondRadii.Clear();

            if (!rule.Enabled)
            {
                return 0;
            }

            int minCount = Mathf.Max(0, rule.MinCount);
            int maxCount = Mathf.Max(minCount, rule.MaxCount);
            int count = Random.Range(minCount, maxCount + 1);

            List<Vector2Int> centers = new();
            int generatedCount = 0;

            for (int i = 0; i < count; i++)
            {
                int minRadius = Mathf.Max(1, rule.MinRadiusCells);
                int maxRadius = Mathf.Max(minRadius, rule.MaxRadiusCells);
                int radius = Random.Range(minRadius, maxRadius + 1);

                if (!TryFindPondCenter(grid, centers, rule, radius, out Vector2Int center))
                {
                    continue;
                }

                ApplyPondTerrainCells(grid, center, radius);
                centers.Add(center);
                _generatedPondCenters.Add(center);
                _generatedPondRadii.Add(radius);
                generatedCount++;
            }

            return generatedCount;
        }

        private bool TryFindPondCenter(
    ElementFieldGrid grid,
    List<Vector2Int> existingCenters,
    ArenaTerrainThemeSO.PondRule rule,
    int radiusCells,
    out Vector2Int center)
        {
            Vector2Int cellCount = grid.CellCount;
            int minDistance = Mathf.Max(0, rule.MinDistanceCells);
            int margin = Mathf.Max(0, radiusCells + (_usePondBankTerrain ? _pondBankWidthCells : 0));

            if (cellCount.x <= margin * 2 || cellCount.y <= margin * 2)
            {
                center = default;
                return false;
            }

            for (int attempt = 0; attempt < 50; attempt++)
            {
                Vector2Int candidate = new Vector2Int(
                    Random.Range(margin, cellCount.x - margin),
                    Random.Range(margin, cellCount.y - margin)
                );

                bool tooClose = false;

                for (int i = 0; i < existingCenters.Count; i++)
                {
                    if (Vector2Int.Distance(candidate, existingCenters[i]) < minDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose)
                {
                    continue;
                }

                center = candidate;
                return true;
            }

            center = default;
            return false;
        }

        private void ApplyPondTerrainCells(ElementFieldGrid grid, Vector2Int centerCell)
        {
            ApplyPondTerrainCells(grid, centerCell, _pondRadiusCells);
        }
        private void ApplyPondTerrainCells(
    ElementFieldGrid grid,
    Vector2Int centerCell,
    int radiusCells)
        {
            int pondRadius = Mathf.Max(1, radiusCells);
            int bankWidth = _usePondBankTerrain ? Mathf.Max(0, _pondBankWidthCells) : 0;
            int totalRadius = pondRadius + bankWidth;

            for (int x = -totalRadius; x <= totalRadius; x++)
            {
                for (int y = -totalRadius; y <= totalRadius; y++)
                {
                    Vector2Int coord = centerCell + new Vector2Int(x, y);

                    if (!grid.IsValidCell(coord))
                    {
                        continue;
                    }

                    float distance = new Vector2(x, y).magnitude;
                    float edgeNoise = (Hash01(centerCell.x, centerCell.y, coord.x, coord.y) - 0.5f) *
                                      _pondEdgeNoise * Mathf.Max(1f, pondRadius);
                    float waterRadius = pondRadius + edgeNoise;

                    if (distance <= waterRadius)
                    {
                        grid.SetTerrainCell(coord, TerrainElementType.Water);
                        continue;
                    }

                    if (_usePondBankTerrain && distance <= totalRadius)
                    {
                        grid.SetTerrainCell(coord, _pondBankTerrain);
                    }
                }
            }
        }

        [ContextMenu("Apply Theme To Terrain")]
        public void ApplyThemeToTerrain()
        {
            ElementFieldGrid grid = ResolveGrid();
            TerrainData terrainData = PrepareTerrainForGeneration(grid, resetGridTerrain: _fillGridWithBaseTerrain);

            if (terrainData == null)
            {
                return;
            }

            Debug.Log(
                $"[ArenaTerrainPainter] Applied terrain theme. Theme: {_theme.ThemeName}, " +
                $"BaseTerrain: {_theme.BaseTerrain}, Alphamap: {terrainData.alphamapWidth}x{terrainData.alphamapHeight}"
            );
        }

        [ContextMenu("Generate Test Pond")]
        public void GenerateTestPond()
        {
            ElementFieldGrid grid = ResolveGrid();
            TerrainData terrainData = PrepareTerrainForGeneration(grid, resetGridTerrain: true);

            if (grid == null || terrainData == null)
            {
                return;
            }

            Vector2Int centerCell = ResolvePondCenterCell(grid);
            ApplyPondTerrainCells(grid, centerCell);
            PaintAlphamap(grid, terrainData);
            SculptPond(grid, terrainData, centerCell);
            RebuildWaterSurface(grid, terrainData, centerCell);

            Debug.Log(
                $"[ArenaTerrainPainter] Generated test pond. Center: {centerCell}, " +
                $"RadiusCells: {_pondRadiusCells}, BankCells: {_pondBankWidthCells}, Depth: {_pondDepth:0.00}"
            );
        }

        [ContextMenu("Reset Arena Terrain")]
        public void ResetArenaTerrain()
        {
            ElementFieldGrid grid = ResolveGrid();
            TerrainData terrainData = PrepareTerrainForGeneration(grid, resetGridTerrain: true);

            if (terrainData == null)
            {
                return;
            }

            Debug.Log(
                $"[ArenaTerrainPainter] Reset arena terrain. Theme: {_theme.ThemeName}, " +
                $"BaseTerrain: {_theme.BaseTerrain}"
            );
        }

        [ContextMenu("Clear Generated Water Surfaces")]
        public void ClearGeneratedWaterSurfaces()
        {
            for (int i = _spawnedWaterSurfaces.Count - 1; i >= 0; i--)
            {
                GameObject surface = _spawnedWaterSurfaces[i];

                if (surface == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(surface);
                }
                else
                {
                    DestroyImmediate(surface);
                }
            }

            _spawnedWaterSurfaces.Clear();
        }

        private TerrainData PrepareTerrainForGeneration(ElementFieldGrid grid, bool resetGridTerrain)
        {
            if (grid == null)
            {
                Debug.LogWarning("[ArenaTerrainPainter] ElementFieldGrid is missing.");
                return null;
            }

            if (_terrain == null)
            {
                Debug.LogWarning("[ArenaTerrainPainter] Terrain is missing.");
                return null;
            }

            if (_theme == null)
            {
                Debug.LogWarning("[ArenaTerrainPainter] ArenaTerrainThemeSO is missing.");
                return null;
            }

            TerrainData terrainData = _terrain.terrainData;

            if (terrainData == null)
            {
                Debug.LogWarning("[ArenaTerrainPainter] TerrainData is missing.");
                return null;
            }

            ClearGeneratedWaterSurfaces();

            if (resetGridTerrain)
            {
                grid.FillTerrain(_theme.BaseTerrain);
            }

            ConfigureTerrainData(grid, terrainData);

            if (!ConfigureTerrainLayers(terrainData))
            {
                return null;
            }

            ResetTerrainHeights(terrainData);
            PaintAlphamap(grid, terrainData);

            return terrainData;
        }

        private ElementFieldGrid ResolveGrid()
        {
            if (_fieldGrid == null)
            {
                _fieldGrid = ElementFieldGrid.Instance != null
                    ? ElementFieldGrid.Instance
                    : FindFirstObjectByType<ElementFieldGrid>();
            }

            return _fieldGrid;
        }

        private void ConfigureTerrainData(ElementFieldGrid grid, TerrainData terrainData)
        {
            if (_resizeTerrainToField)
            {
                Vector2 fieldSize = grid.FieldWorldSize;
                Vector3 terrainSize = terrainData.size;
                terrainData.size = new Vector3(fieldSize.x, terrainSize.y, fieldSize.y);

                Vector3 fieldCenter = grid.FieldCenter;
                _terrain.transform.position = new Vector3(
                    fieldCenter.x - fieldSize.x * 0.5f,
                    _terrain.transform.position.y,
                    fieldCenter.z - fieldSize.y * 0.5f
                );
            }

            if (_alignSurfaceToFieldPlane)
            {
                Vector3 terrainPosition = _terrain.transform.position;
                float baseHeight = ResolvePondBaseHeight(terrainData);
                float targetSurfaceY = grid.FieldCenter.y + _targetSurfaceYOffset;
                terrainPosition.y = targetSurfaceY - baseHeight * terrainData.size.y;
                _terrain.transform.position = terrainPosition;
            }

            if (terrainData.alphamapResolution != _alphamapResolution)
            {
                terrainData.alphamapResolution = _alphamapResolution;
            }
        }

        private bool ConfigureTerrainLayers(TerrainData terrainData)
        {
            _layerIndexByTerrain.Clear();

            ArenaTerrainThemeSO.TerrainLayerEntry[] entries = _theme.TerrainLayers;

            if (entries == null || entries.Length == 0)
            {
                Debug.LogWarning("[ArenaTerrainPainter] Theme terrain layers are empty.");
                return false;
            }

            List<TerrainLayer> layers = new();

            for (int i = 0; i < entries.Length; i++)
            {
                ArenaTerrainThemeSO.TerrainLayerEntry entry = entries[i];

                if (entry.Layer == null || _layerIndexByTerrain.ContainsKey(entry.Terrain))
                {
                    continue;
                }

                _layerIndexByTerrain.Add(entry.Terrain, layers.Count);
                layers.Add(entry.Layer);
            }

            if (layers.Count == 0)
            {
                Debug.LogWarning("[ArenaTerrainPainter] No valid TerrainLayer entries found.");
                return false;
            }

            if (!_layerIndexByTerrain.ContainsKey(_theme.BaseTerrain))
            {
                Debug.LogWarning(
                    $"[ArenaTerrainPainter] Base terrain layer is missing. " +
                    $"BaseTerrain: {_theme.BaseTerrain}. Add a TerrainLayer entry for the base terrain."
                );
                return false;
            }

            terrainData.terrainLayers = layers.ToArray();
            return true;
        }

        private Vector2Int ResolvePondCenterCell(ElementFieldGrid grid)
        {
            if (grid.IsValidCell(_pondCenterCell))
            {
                return _pondCenterCell;
            }

            Vector2Int cellCount = grid.CellCount;
            return new Vector2Int(
                Mathf.Max(0, cellCount.x / 2),
                Mathf.Max(0, cellCount.y / 2)
            );
        }

        private void SculptPond(
            ElementFieldGrid grid,
            TerrainData terrainData,
            Vector2Int centerCell)
        {
            int heightmapResolution = terrainData.heightmapResolution;
            float[,] heights = terrainData.GetHeights(0, 0, heightmapResolution, heightmapResolution);
            Vector3 pondCenter = grid.CellToWorld(centerCell);
            float cellSize = grid.CellSize;
            float pondRadius = Mathf.Max(cellSize, _pondRadiusCells * cellSize);
            float bankWidth = Mathf.Max(cellSize * 0.25f, _pondBankWidthCells * cellSize);
            float totalRadius = pondRadius + bankWidth;
            float normalizedDepth = _pondDepth / Mathf.Max(0.001f, terrainData.size.y);

            for (int y = 0; y < heightmapResolution; y++)
            {
                for (int x = 0; x < heightmapResolution; x++)
                {
                    Vector3 worldPosition = HeightmapToWorldPosition(x, y, heightmapResolution, terrainData);
                    Vector2 flatDelta = new Vector2(
                        worldPosition.x - pondCenter.x,
                        worldPosition.z - pondCenter.z
                    );
                    float distance = flatDelta.magnitude;

                    if (distance > totalRadius)
                    {
                        continue;
                    }

                    float targetDepth = distance <= pondRadius
                        ? normalizedDepth
                        : normalizedDepth * Mathf.SmoothStep(1f, 0f, (distance - pondRadius) / bankWidth);

                    heights[y, x] = Mathf.Max(0f, heights[y, x] - targetDepth * Mathf.Lerp(1f, _pondFloorBlend, distance / totalRadius));
                }
            }

            terrainData.SetHeights(0, 0, heights);
        }
        private void SculptWaterCells(ElementFieldGrid grid, TerrainData terrainData)
        {
            if (_generatedPondCenters.Count == 0)
            {
                return;
            }

            int heightmapResolution = terrainData.heightmapResolution;
            float[,] heights = terrainData.GetHeights(0, 0, heightmapResolution, heightmapResolution);

            float cellSize = grid.CellSize;
            float bankWidth = Mathf.Max(cellSize * 0.25f, _pondBankWidthCells * cellSize);
            float normalizedDepth = _pondDepth / Mathf.Max(0.001f, terrainData.size.y);
            float baseHeight = ResolvePondBaseHeight(terrainData);

            for (int y = 0; y < heightmapResolution; y++)
            {
                for (int x = 0; x < heightmapResolution; x++)
                {
                    Vector3 worldPosition = HeightmapToWorldPosition(x, y, heightmapResolution, terrainData);
                    float lowestTargetHeight = baseHeight;

                    for (int i = 0; i < _generatedPondCenters.Count; i++)
                    {
                        Vector2Int centerCell = _generatedPondCenters[i];
                        int radiusCells = i < _generatedPondRadii.Count ? _generatedPondRadii[i] : _pondRadiusCells;
                        Vector3 pondCenter = grid.CellToWorld(centerCell);
                        float pondRadius = Mathf.Max(cellSize, radiusCells * cellSize);
                        float totalRadius = pondRadius + bankWidth;
                        Vector2 delta = new Vector2(
                            worldPosition.x - pondCenter.x,
                            worldPosition.z - pondCenter.z
                        );
                        float distance = delta.magnitude;

                        if (distance > totalRadius)
                        {
                            continue;
                        }

                        float depth01;

                        if (distance <= pondRadius)
                        {
                            depth01 = 1f;
                        }
                        else
                        {
                            float bankT = (distance - pondRadius) / Mathf.Max(0.001f, bankWidth);
                            depth01 = Mathf.SmoothStep(1f, 0f, bankT);
                        }

                        float floorBlend = Mathf.Lerp(1f, _pondFloorBlend, distance / Mathf.Max(0.001f, totalRadius));
                        float targetHeight = baseHeight - normalizedDepth * depth01 * floorBlend;
                        lowestTargetHeight = Mathf.Min(lowestTargetHeight, targetHeight);
                    }

                    heights[y, x] = Mathf.Min(heights[y, x], Mathf.Max(0f, lowestTargetHeight));
                }
            }

            terrainData.SetHeights(0, 0, heights);
        }
        private void ResetTerrainHeights(TerrainData terrainData)
        {
            int heightmapResolution = terrainData.heightmapResolution;
            float[,] heights = new float[heightmapResolution, heightmapResolution];
            float baseHeight = ResolvePondBaseHeight(terrainData);

            for (int y = 0; y < heightmapResolution; y++)
            {
                for (int x = 0; x < heightmapResolution; x++)
                {
                    heights[y, x] = baseHeight;
                }
            }

            terrainData.SetHeights(0, 0, heights);
        }

        private List<Vector3> CollectWaterCellWorldPositions(ElementFieldGrid grid)
        {
            List<Vector3> positions = new();
            Vector2Int cellCount = grid.CellCount;

            for (int x = 0; x < cellCount.x; x++)
            {
                for (int y = 0; y < cellCount.y; y++)
                {
                    Vector2Int coord = new Vector2Int(x, y);

                    if (!grid.TryGetCellData(coord, out ElementFieldCellData data) ||
                        data.TerrainElement != TerrainElementType.Water)
                    {
                        continue;
                    }

                    positions.Add(grid.CellToWorld(coord));
                }
            }

            return positions;
        }

        private float ResolvePondBaseHeight(TerrainData terrainData)
        {
            if (!_autoRaiseBaseHeightForPonds)
            {
                return _baseHeight;
            }

            float normalizedDepth = _pondDepth / Mathf.Max(0.001f, terrainData.size.y);
            return Mathf.Clamp01(Mathf.Max(_baseHeight, normalizedDepth + 0.02f));
        }

        private Vector3 HeightmapToWorldPosition(
            int x,
            int y,
            int resolution,
            TerrainData terrainData)
        {
            float normalizedX = resolution > 1 ? x / (float)(resolution - 1) : 0f;
            float normalizedZ = resolution > 1 ? y / (float)(resolution - 1) : 0f;
            Vector3 terrainPosition = _terrain.transform.position;
            Vector3 terrainSize = terrainData.size;

            return new Vector3(
                terrainPosition.x + normalizedX * terrainSize.x,
                terrainPosition.y,
                terrainPosition.z + normalizedZ * terrainSize.z
            );
        }

        private void RebuildWaterSurface(
            ElementFieldGrid grid,
            TerrainData terrainData,
            Vector2Int centerCell)
        {
            if (_theme.WaterSurfacePrefab == null)
            {
                return;
            }

            ClearGeneratedWaterSurfaces();

            Transform parent = _waterSurfaceRoot != null ? _waterSurfaceRoot : transform;
            Vector3 pondCenter = grid.CellToWorld(centerCell);
            float cellSize = grid.CellSize;
            float diameter = Mathf.Max(cellSize, _pondRadiusCells * cellSize * 2f);
            float terrainHeight = _terrain.SampleHeight(pondCenter) + _terrain.transform.position.y;
            Vector3 waterPosition = new Vector3(
                pondCenter.x,
                terrainHeight + _theme.WaterSurfaceYOffset,
                pondCenter.z
            );

            GameObject waterSurface = Instantiate(_theme.WaterSurfacePrefab, waterPosition, Quaternion.identity, parent);
            waterSurface.name = $"WaterSurface_Pond_{centerCell.x}_{centerCell.y}";
            FitWaterSurfaceToDiameter(waterSurface, diameter);
            _spawnedWaterSurfaces.Add(waterSurface);
            ApplyWaterSurfaceMaterialForTerrain(grid, waterSurface, centerCell);
        }

        private void RebuildGeneratedWaterSurfaces(ElementFieldGrid grid, TerrainData terrainData)
        {
            if (_theme.WaterSurfacePrefab == null)
            {
                return;
            }

            ClearGeneratedWaterSurfaces();

            for (int i = 0; i < _generatedPondCenters.Count; i++)
            {
                Vector2Int center = _generatedPondCenters[i];
                int radius = i < _generatedPondRadii.Count ? _generatedPondRadii[i] : _pondRadiusCells;
                SpawnWaterSurface(grid, terrainData, center, radius);
            }

            RefreshFrozenWaterSurfaceMaterials(grid);
        }

        private void SpawnWaterSurface(
            ElementFieldGrid grid,
            TerrainData terrainData,
            Vector2Int centerCell,
            int radiusCells)
        {
            Transform parent = _waterSurfaceRoot != null ? _waterSurfaceRoot : transform;
            Vector3 pondCenter = grid.CellToWorld(centerCell);
            float cellSize = grid.CellSize;
            float diameter = Mathf.Max(cellSize, radiusCells * cellSize * 2f);
            float terrainHeight = ResolveWaterSurfaceHeight(terrainData);
            Vector3 waterPosition = new Vector3(
                pondCenter.x,
                terrainHeight + _theme.WaterSurfaceYOffset,
                pondCenter.z
            );

            if (_useProceduralWaterSurface && TryGetWaterSurfaceMaterial(out Material waterMaterial))
            {
                GameObject proceduralSurface = CreateProceduralWaterSurface(
                    waterPosition,
                    diameter,
                    centerCell,
                    waterMaterial,
                    parent
                );
                _spawnedWaterSurfaces.Add(proceduralSurface);
                ApplyWaterSurfaceMaterialForTerrain(grid, proceduralSurface, centerCell);
                return;
            }

            GameObject waterSurface = Instantiate(_theme.WaterSurfacePrefab, waterPosition, Quaternion.identity, parent);
            waterSurface.name = $"WaterSurface_Pond_{centerCell.x}_{centerCell.y}";
            FitWaterSurfaceToDiameter(waterSurface, diameter);
            _spawnedWaterSurfaces.Add(waterSurface);
            ApplyWaterSurfaceMaterialForTerrain(grid, waterSurface, centerCell);
        }

        private bool TryGetWaterSurfaceMaterial(out Material material)
        {
            material = null;

            if (_theme.WaterSurfacePrefab == null)
            {
                return false;
            }

            Renderer renderer = _theme.WaterSurfacePrefab.GetComponentInChildren<Renderer>();

            if (renderer == null || renderer.sharedMaterial == null)
            {
                return false;
            }

            material = renderer.sharedMaterial;
            return true;
        }

        private GameObject CreateProceduralWaterSurface(
            Vector3 waterPosition,
            float diameter,
            Vector2Int centerCell,
            Material waterMaterial,
            Transform parent)
        {
            GameObject waterSurface = new GameObject($"WaterSurface_Pond_{centerCell.x}_{centerCell.y}");
            waterSurface.transform.SetParent(parent, false);
            waterSurface.transform.position = waterPosition;

            MeshFilter meshFilter = waterSurface.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = waterSurface.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = waterMaterial;

            int segments = Mathf.Max(8, _waterSurfaceSegments);
            Vector3[] vertices = new Vector3[segments + 1];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 3];
            float radius = diameter * 0.5f;

            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                float noise = Hash01(centerCell.x, centerCell.y, i, 97) - 0.5f;
                float edgeRadius = radius * (1f + noise * _waterSurfaceEdgeNoise);
                float x = Mathf.Cos(angle) * edgeRadius;
                float z = Mathf.Sin(angle) * edgeRadius;

                vertices[i + 1] = new Vector3(x, 0f, z);
                uvs[i + 1] = new Vector2(
                    0.5f + x / Mathf.Max(0.001f, diameter),
                    0.5f + z / Mathf.Max(0.001f, diameter)
                );

                int triangleIndex = i * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = i == segments - 1 ? 1 : i + 2;
                triangles[triangleIndex + 2] = i + 1;
            }

            Mesh mesh = new Mesh
            {
                name = waterSurface.name + "_Mesh",
                vertices = vertices,
                uv = uvs,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            meshFilter.sharedMesh = mesh;

            return waterSurface;
        }

        private float ResolveWaterSurfaceHeight(TerrainData terrainData)
        {
            float baseWorldHeight = _terrain.transform.position.y + ResolvePondBaseHeight(terrainData) * terrainData.size.y;
            float fillDepth = _pondDepth * Mathf.Clamp01(_waterFillDepthRatio);
            return baseWorldHeight - fillDepth;
        }

        private void FitWaterSurfaceToDiameter(GameObject waterSurface, float diameter)
        {
            if (waterSurface == null)
            {
                return;
            }

            if (!TryGetRendererBounds(waterSurface, out Bounds bounds))
            {
                waterSurface.transform.localScale = new Vector3(diameter, 1f, diameter);
                return;
            }

            float currentWidth = Mathf.Max(0.001f, bounds.size.x);
            float currentDepth = Mathf.Max(0.001f, bounds.size.z);
            Vector3 scale = waterSurface.transform.localScale;

            scale.x *= diameter / currentWidth;
            scale.z *= diameter / currentDepth;
            waterSurface.transform.localScale = scale;
        }

        private static bool TryGetRendererBounds(GameObject target, out Bounds bounds)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            bounds = default;
            bool hasBounds = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];

                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return hasBounds;
        }

        private void HandleTerrainChanged(Vector2Int coord, ElementFieldCellData data)
        {
            if (data.TerrainElement != TerrainElementType.Ice)
            {
                return;
            }

            ElementFieldGrid grid = ResolveGrid();
            if (grid != null)
            {
                RefreshFrozenWaterSurfaceMaterials(grid);
            }
        }

        private void RefreshFrozenWaterSurfaceMaterials(ElementFieldGrid grid)
        {
            if (grid == null || _iceWaterSurfaceMaterial == null)
            {
                return;
            }

            RefreshWaterSurfaceListFromHierarchy();

            for (int i = 0; i < _spawnedWaterSurfaces.Count; i++)
            {
                GameObject surface = _spawnedWaterSurfaces[i];
                if (surface == null)
                {
                    continue;
                }

                if (!DoesSurfaceTouchFrozenTerrain(grid, surface))
                {
                    continue;
                }

                ApplyWaterSurfaceMaterial(surface, _iceWaterSurfaceMaterial);
            }
        }

        private void ApplyWaterSurfaceMaterialForTerrain(
            ElementFieldGrid grid,
            GameObject waterSurface,
            Vector2Int centerCell)
        {
            if (grid == null ||
                waterSurface == null ||
                _iceWaterSurfaceMaterial == null ||
                !grid.TryGetCellData(centerCell, out ElementFieldCellData data) ||
                data.TerrainElement != TerrainElementType.Ice)
            {
                return;
            }

            ApplyWaterSurfaceMaterial(waterSurface, _iceWaterSurfaceMaterial);
        }

        private void RefreshWaterSurfaceListFromHierarchy()
        {
            Transform root = _waterSurfaceRoot != null ? _waterSurfaceRoot : transform;
            AddWaterSurfacesFromTransforms(root.GetComponentsInChildren<Transform>(true));

            Renderer[] sceneRenderers = FindObjectsByType<Renderer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            for (int i = 0; i < sceneRenderers.Length; i++)
            {
                Renderer sceneRenderer = sceneRenderers[i];
                if (sceneRenderer == null)
                {
                    continue;
                }

                Transform surfaceTransform = FindWaterSurfaceRoot(sceneRenderer.transform);
                if (surfaceTransform == null)
                {
                    continue;
                }

                GameObject surface = surfaceTransform.gameObject;
                if (!_spawnedWaterSurfaces.Contains(surface))
                {
                    _spawnedWaterSurfaces.Add(surface);
                }
            }
        }

        private void AddWaterSurfacesFromTransforms(Transform[] transforms)
        {
            if (transforms == null)
            {
                return;
            }

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform child = transforms[i];
                if (child == null ||
                    !child.name.StartsWith("WaterSurface_Pond_", System.StringComparison.Ordinal))
                {
                    continue;
                }

                GameObject surface = child.gameObject;
                if (!_spawnedWaterSurfaces.Contains(surface))
                {
                    _spawnedWaterSurfaces.Add(surface);
                }
            }
        }

        private static Transform FindWaterSurfaceRoot(Transform transform)
        {
            Transform current = transform;
            while (current != null)
            {
                if (current.name.StartsWith("WaterSurface_Pond_", System.StringComparison.Ordinal))
                {
                    return current;
                }

                current = current.parent;
            }

            return null;
        }

        private bool DoesSurfaceTouchFrozenTerrain(ElementFieldGrid grid, GameObject surface)
        {
            if (grid == null || surface == null)
            {
                return false;
            }

            if (!TryGetRendererBounds(surface, out Bounds bounds))
            {
                return grid.TryGetCellData(surface.transform.position, out ElementFieldCellData centerData) &&
                       centerData.TerrainElement == TerrainElementType.Ice;
            }

            Bounds xzBounds = FlattenBoundsToFieldPlane(bounds);
            Vector2Int cellCount = grid.CellCount;
            for (int x = 0; x < cellCount.x; x++)
            {
                for (int y = 0; y < cellCount.y; y++)
                {
                    Vector2Int coord = new Vector2Int(x, y);
                    if (!grid.TryGetCellData(coord, out ElementFieldCellData data) ||
                        data.TerrainElement != TerrainElementType.Ice)
                    {
                        continue;
                    }

                    Vector3 cellPosition = grid.CellToWorld(coord);
                    if (xzBounds.Contains(cellPosition))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static Bounds FlattenBoundsToFieldPlane(Bounds bounds)
        {
            Vector3 center = bounds.center;
            Vector3 size = bounds.size;

            center.y = 0f;
            size.y = 10000f;

            return new Bounds(center, size);
        }

        private static void ApplyWaterSurfaceMaterial(GameObject waterSurface, Material material)
        {
            if (waterSurface == null || material == null)
            {
                return;
            }

            Renderer[] renderers = waterSurface.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer targetRenderer = renderers[i];
                if (targetRenderer != null)
                {
                    targetRenderer.sharedMaterial = material;
                }
            }
        }

        private void PaintAlphamap(ElementFieldGrid grid, TerrainData terrainData)
        {
            int width = terrainData.alphamapWidth;
            int height = terrainData.alphamapHeight;
            int layerCount = terrainData.alphamapLayers;
            float[,,] alphamaps = new float[height, width, layerCount];
            int fallbackLayer = ResolveFallbackLayer();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector3 worldPosition = AlphamapToWorldPosition(x, y, width, height, terrainData);
                    TerrainElementType terrain = ResolveTerrainAtWorldPosition(grid, worldPosition);
                    int layerIndex = ResolveLayerIndex(terrain, fallbackLayer);

                    alphamaps[y, x, layerIndex] = 1f;
                }
            }

            terrainData.SetAlphamaps(0, 0, alphamaps);
        }

        private Vector3 AlphamapToWorldPosition(
            int x,
            int y,
            int width,
            int height,
            TerrainData terrainData)
        {
            float normalizedX = width > 1 ? x / (float)(width - 1) : 0f;
            float normalizedZ = height > 1 ? y / (float)(height - 1) : 0f;
            Vector3 terrainPosition = _terrain.transform.position;
            Vector3 terrainSize = terrainData.size;

            return new Vector3(
                terrainPosition.x + normalizedX * terrainSize.x,
                terrainPosition.y,
                terrainPosition.z + normalizedZ * terrainSize.z
            );
        }

        private TerrainElementType ResolveTerrainAtWorldPosition(
            ElementFieldGrid grid,
            Vector3 worldPosition)
        {
            if (grid.TryGetCellData(worldPosition, out ElementFieldCellData data))
            {
                return data.TerrainElement;
            }

            return _theme.BaseTerrain;
        }

        private int ResolveFallbackLayer()
        {
            if (_layerIndexByTerrain.TryGetValue(_theme.BaseTerrain, out int baseLayer))
            {
                return baseLayer;
            }

            return 0;
        }

        private int ResolveLayerIndex(TerrainElementType terrain, int fallbackLayer)
        {
            if (_layerIndexByTerrain.TryGetValue(terrain, out int layerIndex))
            {
                return layerIndex;
            }

            return fallbackLayer;
        }

        private static float Hash01(int a, int b, int c, int d)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + a;
                hash = hash * 31 + b;
                hash = hash * 31 + c;
                hash = hash * 31 + d;
                hash ^= hash << 13;
                hash ^= hash >> 17;
                hash ^= hash << 5;
                return Mathf.Abs(hash % 10000) / 9999f;
            }
        }
    }
}
