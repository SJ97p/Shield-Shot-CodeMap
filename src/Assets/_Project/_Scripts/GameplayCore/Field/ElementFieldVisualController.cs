using System.Collections.Generic;
using UnityEngine;

namespace Shield_Shot.GameplayCore.Field
{
    public class ElementFieldVisualController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ElementFieldGrid _fieldGrid;
        [SerializeField] private Transform _visualRoot;

        [Header("Tile")]
        [SerializeField] private Material _tileMaterial;
        [SerializeField, Min(0.001f)] private float _tileHeight = 0.02f;
        [SerializeField, Range(0.1f, 1f)] private float _tileFillRatio = 0.96f;
        [SerializeField] private float _yOffset = 0.01f;

        [Header("Element Materials")]
        [SerializeField] private Material _iceTileMaterial;
        [SerializeField] private Material _frozenTerrainMaterial;

        [Header("Colors")]
        [SerializeField] private Color _fireColor = new Color(1f, 0.12f, 0.04f, 0.55f);
        [SerializeField] private Color _iceColor = new Color(0.2f, 0.8f, 1f, 0.55f);
        [SerializeField] private Color _poisonColor = new Color(0.3f, 1f, 0.2f, 0.55f);
        [SerializeField] private Color _lightningColor = new Color(1f, 0.9f, 0.1f, 0.55f);
        [SerializeField] private Color _windColor = new Color(0.75f, 1f, 0.85f, 0.45f);
        [SerializeField] private Color _waterColor = new Color(0.1f, 0.35f, 1f, 0.45f);

        private readonly Dictionary<Vector2Int, Renderer> _activeTiles = new();
        private readonly Stack<Renderer> _tilePool = new();
        private MaterialPropertyBlock _propertyBlock;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
        }

        private void Reset()
        {
            _fieldGrid = FindFirstObjectByType<ElementFieldGrid>();
            _visualRoot = transform;
        }

        private void OnEnable()
        {
            ElementFieldGrid grid = ResolveFieldGrid();

            if (grid == null)
            {
                return;
            }

            grid.CellChanged += HandleCellChanged;
            grid.CellCleared += HandleCellCleared;
            grid.TerrainChanged += HandleTerrainChanged;
        }

        private void OnDisable()
        {
            ElementFieldGrid grid = ResolveFieldGrid();

            if (grid == null)
            {
                return;
            }

            grid.CellChanged -= HandleCellChanged;
            grid.CellCleared -= HandleCellCleared;
            grid.TerrainChanged -= HandleTerrainChanged;
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

        private void HandleCellChanged(
            Vector2Int coord,
            ElementFieldCellData data,
            ElementReactionResult reaction)
        {
            if (!data.IsActive)
            {
                HideTile(coord);
                return;
            }

            if (reaction.ReactionType == ElementReactionType.Freeze ||
                (data.TerrainElement == TerrainElementType.Ice &&
                 data.LastReactionType == ElementReactionType.Freeze))
            {
                HideTile(coord);
                return;
            }

            Renderer tile = GetOrCreateTile(coord);
            ApplyTileTransform(tile.transform, coord);
            ApplyTileMaterial(tile, data.CurrentElement);
            ApplyTileColor(tile, data.CurrentElement);
            tile.gameObject.SetActive(true);
        }

        private void HandleCellCleared(Vector2Int coord, ElementFieldCellData data)
        {
            HideTile(coord);
        }

        private void HandleTerrainChanged(Vector2Int coord, ElementFieldCellData data)
        {
            if (!data.IsActive)
            {
                HideTile(coord);
            }
        }

        private void ShowTerrainTile(Vector2Int coord, TerrainElementType terrain)
        {
            Renderer tile = GetOrCreateTile(coord);
            ApplyTileTransform(tile.transform, coord);
            ApplyTerrainTileMaterial(tile, terrain);
            ApplyTileColor(tile, GetTerrainElementColor(terrain));
            tile.gameObject.SetActive(true);
        }

        private Renderer GetOrCreateTile(Vector2Int coord)
        {
            if (_activeTiles.TryGetValue(coord, out Renderer existingTile) &&
                existingTile != null)
            {
                return existingTile;
            }

            Renderer tile = _tilePool.Count > 0
                ? _tilePool.Pop()
                : CreateTile();

            tile.gameObject.name = $"ElementFieldVisual_{coord.x}_{coord.y}";
            _activeTiles[coord] = tile;
            return tile;
        }

        private Renderer CreateTile()
        {
            GameObject tileObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tileObject.transform.SetParent(_visualRoot != null ? _visualRoot : transform, false);

            if (tileObject.TryGetComponent(out Collider tileCollider))
            {
                Destroy(tileCollider);
            }

            Renderer renderer = tileObject.GetComponent<Renderer>();

            if (_tileMaterial != null)
            {
                renderer.sharedMaterial = _tileMaterial;
            }
            else
            {
                renderer.sharedMaterial = CreateFallbackMaterial();
            }

            tileObject.SetActive(false);
            return renderer;
        }

        private Material CreateFallbackMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            material.name = "Element Field Visual Runtime Material";
            return material;
        }

        private void ApplyTileTransform(Transform tileTransform, Vector2Int coord)
        {
            ElementFieldGrid grid = ResolveFieldGrid();

            if (grid == null)
            {
                return;
            }

            Vector2 cellSize = grid.CellWorldSize * _tileFillRatio;

            tileTransform.position =
                grid.CellToWorld(coord) +
                grid.FieldSpace.up * (_yOffset + _tileHeight * 0.5f);

            tileTransform.rotation = grid.FieldSpace.rotation;
            tileTransform.localScale = new Vector3(cellSize.x, _tileHeight, cellSize.y);
        }

        private void ApplyTileColor(Renderer tile, ElementType element)
        {
            if (tile == null)
            {
                return;
            }

            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            Color color = GetElementColor(element);

            ApplyTileColor(tile, color);
        }

        private void ApplyTileMaterial(Renderer tile, ElementType element)
        {
            if (tile == null)
            {
                return;
            }

            Material material = element switch
            {
                ElementType.Ice => _iceTileMaterial != null ? _iceTileMaterial : _tileMaterial,
                _ => _tileMaterial
            };

            if (material != null && tile.sharedMaterial != material)
            {
                tile.sharedMaterial = material;
            }
        }

        private void ApplyTerrainTileMaterial(Renderer tile, TerrainElementType terrain)
        {
            if (tile == null)
            {
                return;
            }

            Material material = terrain switch
            {
                TerrainElementType.Ice => _frozenTerrainMaterial != null
                    ? _frozenTerrainMaterial
                    : _iceTileMaterial != null
                        ? _iceTileMaterial
                        : _tileMaterial,
                _ => _tileMaterial
            };

            if (material != null && tile.sharedMaterial != material)
            {
                tile.sharedMaterial = material;
            }
        }

        private void ApplyTileColor(Renderer tile, Color color)
        {
            if (tile == null)
            {
                return;
            }

            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            tile.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorId, color);
            _propertyBlock.SetColor(ColorId, color);
            tile.SetPropertyBlock(_propertyBlock);
        }

        private void HideTile(Vector2Int coord)
        {
            if (!_activeTiles.TryGetValue(coord, out Renderer tile) ||
                tile == null)
            {
                return;
            }

            _activeTiles.Remove(coord);
            tile.gameObject.SetActive(false);
            _tilePool.Push(tile);
        }

        private Color GetElementColor(ElementType element)
        {
            return element switch
            {
                ElementType.Fire => _fireColor,
                ElementType.Ice => _iceColor,
                ElementType.Poison => _poisonColor,
                ElementType.Lightning => _lightningColor,
                ElementType.Wind => _windColor,
                ElementType.Water => _waterColor,
                _ => Color.clear
            };
        }

        private Color GetTerrainElementColor(TerrainElementType terrain)
        {
            return terrain switch
            {
                TerrainElementType.Ice => _iceColor,
                TerrainElementType.Water => _waterColor,
                _ => Color.clear
            };
        }
    }
}
