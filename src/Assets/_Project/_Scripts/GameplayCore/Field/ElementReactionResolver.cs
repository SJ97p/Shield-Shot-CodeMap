using UnityEngine;

namespace Shield_Shot.GameplayCore.Field
{
    public static class ElementReactionResolver
    {
        public static ElementReactionResult Resolve(
            TerrainElementType terrain,
            ElementType current,
            ElementType incoming)
        {
            return Resolve(
                terrain,
                current,
                new ElementPaintContext(incoming)
            );
        }

        public static ElementReactionResult Resolve(
            TerrainElementType terrain,
            ElementType current,
            ElementPaintContext paintContext)
        {
            ElementType incoming = paintContext.Element;

            if (incoming == ElementType.None)
            {
                return ElementReactionResult.None;
            }

            // Grass + Fire: fire spreads wider and lasts longer.
            if (terrain == TerrainElementType.Grass && incoming == ElementType.Fire)
            {
                int level = Mathf.Max(1, paintContext.ElementLevel);
                int spreadCellRadius = level >= 5
                    ? 3
                    : level >= 3
                        ? 2
                        : 1;

                float durationMultiplier = (1.2f + level * 0.15f) * paintContext.PowerMultiplier;
                float spreadDelay = Mathf.Max(0.03f, 0.1f - level * 0.01f);
                float spreadDurationMultiplier = Mathf.Min(1.2f, 0.65f + level * 0.05f);

                return new ElementReactionResult(
                    ElementReactionType.SpreadFire,
                    ElementType.Fire,
                    durationMultiplier: durationMultiplier,
                    spreadCellRadius: spreadCellRadius,
                    spreadDelay: spreadDelay,
                    spreadDurationMultiplier: spreadDurationMultiplier,
                    shouldApplyStatus: true
                );
            }

            // Sand + Wind: hot wind burns enemies standing in the cell.
            if (terrain == TerrainElementType.Sand && incoming == ElementType.Wind)
            {
                int level = Mathf.Max(1, paintContext.ElementLevel);
                int spreadCellRadius = level >= 5
                    ? 3
                    : level >= 3
                        ? 2
                        : 1;
                float durationMultiplier = (1f + level * 0.15f) * paintContext.PowerMultiplier;
                float spreadDurationMultiplier = Mathf.Min(1.25f, 0.65f + level * 0.08f);

                return new ElementReactionResult(
                    ElementReactionType.HotWind,
                    ElementType.Wind,
                    durationMultiplier: durationMultiplier,
                    spreadCellRadius: spreadCellRadius,
                    spreadElement: ElementType.Fire,
                    spreadDelay: 0.04f,
                    spreadDurationMultiplier: spreadDurationMultiplier,
                    shouldApplyStatus: true
                );
            }

            // Water + Ice: freeze the water terrain permanently.
            if (terrain == TerrainElementType.Water && incoming == ElementType.Ice)
            {
                int level = Mathf.Max(1, paintContext.ElementLevel);
                float durationMultiplier = (1.2f + level * 0.1f) * paintContext.PowerMultiplier;

                return new ElementReactionResult(
                    ElementReactionType.Freeze,
                    ElementType.Ice,
                    durationMultiplier: durationMultiplier,
                    shouldApplyStatus: true
                );
            }

            // Current Ice + Fire: melt ice into water.
            if (current == ElementType.Ice && incoming == ElementType.Fire)
            {
                return new ElementReactionResult(
                    ElementReactionType.Melt,
                    ElementType.Water,
                    durationMultiplier: 0.8f,
                    spreadRadius: 0f,
                    shouldApplyStatus: false
                );
            }

            // Current Fire + Ice: steam burst.
            if (current == ElementType.Fire && incoming == ElementType.Ice)
            {
                return new ElementReactionResult(
                    ElementReactionType.Steam,
                    ElementType.Water,
                    durationMultiplier: 0.6f,
                    spreadRadius: 0.5f,
                    shouldApplyStatus: false
                );
            }

            // Poison field + Fire: toxic smoke reaction.
            if (current == ElementType.Poison && incoming == ElementType.Fire)
            {
                return new ElementReactionResult(
                    ElementReactionType.ToxicSmoke,
                    ElementType.Poison,
                    durationMultiplier: 0.8f,
                    spreadRadius: 1f,
                    shouldApplyStatus: true
                );
            }

            // Default: incoming element overrides or activates the cell.
            return new ElementReactionResult(
                ElementReactionType.Ignite,
                incoming,
                durationMultiplier: 1f,
                spreadRadius: 0f,
                shouldApplyStatus: incoming != ElementType.None
            );
        }
    }
}
