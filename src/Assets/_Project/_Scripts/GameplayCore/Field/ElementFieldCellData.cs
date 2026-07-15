using UnityEngine;

namespace Shield_Shot.GameplayCore.Field
{
    public struct ElementFieldCellData
    {
        public Vector2Int Coord;
        public TerrainElementType TerrainElement;
        public ElementType CurrentElement;
        public int ElementLevel;
        public float RemainingTime;
        public ElementReactionType LastReactionType;

        public bool IsActive => CurrentElement != ElementType.None && RemainingTime > 0f;

        public ElementFieldCellData(Vector2Int coord, TerrainElementType terrainElement)
        {
            Coord = coord;
            TerrainElement = terrainElement;
            CurrentElement = ElementType.None;
            ElementLevel = 1;
            RemainingTime = 0f;
            LastReactionType = ElementReactionType.None;
        }

        public void Clear()
        {
            CurrentElement = ElementType.None;
            ElementLevel = 1;
            RemainingTime = 0f;
            LastReactionType = ElementReactionType.None;
        }
    }
}
