using System;

namespace Shield_Shot.InputSystemV2.Combat.Domain
{
    public readonly struct CombatInputLayout
    {
        public CombatSplitDirection SplitDirection { get; }
        public float SplitRatio { get; }
        public bool IsInverted { get; }

        public CombatInputLayout(
            CombatSplitDirection splitDirection,
            float splitRatio,
            bool isInverted)
        {
            if (splitDirection != CombatSplitDirection.LeftRight &&
                splitDirection != CombatSplitDirection.BottomTop)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(splitDirection));
            }

            if (float.IsNaN(splitRatio) ||
                float.IsInfinity(splitRatio) ||
                splitRatio <= 0f ||
                splitRatio >= 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(splitRatio),
                    splitRatio,
                    "Split ratio must be greater than 0 and less than 1.");
            }

            SplitDirection = splitDirection;
            SplitRatio = splitRatio;
            IsInverted = isInverted;
        }
    }
}