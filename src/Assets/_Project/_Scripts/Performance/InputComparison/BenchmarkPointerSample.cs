using System;
using UnityEngine;

namespace Shield_Shot.Performance.InputComparison
{
    [Serializable]
    public readonly struct BenchmarkPointerSample
    {
        public int PointerId
        {
            get;
        }

        public BenchmarkPointerPhase Phase
        {
            get;
        }

        public Vector2 NormalizedPosition
        {
            get;
        }

        public double TimestampOffset
        {
            get;
        }

        public BenchmarkPointerSample(
            int pointerId,
            BenchmarkPointerPhase phase,
            Vector2 normalizedPosition,
            double timestampOffset)
        {
            if (!IsFinite(normalizedPosition.x) ||
                !IsFinite(normalizedPosition.y))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(normalizedPosition),
                    normalizedPosition,
                    "Position must contain finite values.");
            }

            if (normalizedPosition.x < 0f ||
                normalizedPosition.x > 1f ||
                normalizedPosition.y < 0f ||
                normalizedPosition.y > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(normalizedPosition),
                    normalizedPosition,
                    "Normalized position must be between 0 and 1.");
            }

            if (double.IsNaN(timestampOffset) ||
                double.IsInfinity(timestampOffset) ||
                timestampOffset < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timestampOffset),
                    timestampOffset,
                    "Timestamp offset must be finite and non-negative.");
            }

            PointerId = pointerId;
            Phase = phase;
            NormalizedPosition = normalizedPosition;
            TimestampOffset = timestampOffset;
        }

        public Vector2 ToScreenPosition(
            Vector2 viewportSize)
        {
            if (viewportSize.x <= 0f ||
                viewportSize.y <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(viewportSize),
                    viewportSize,
                    "Viewport size must be positive.");
            }

            return new Vector2(
                NormalizedPosition.x * viewportSize.x,
                NormalizedPosition.y * viewportSize.y);
        }

        private static bool IsFinite(float value)
        {
            return
                !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }
    }
}