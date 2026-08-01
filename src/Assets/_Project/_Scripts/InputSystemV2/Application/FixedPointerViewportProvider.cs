using System;
using UnityEngine;

namespace Shield_Shot.InputSystemV2.Application
{
    public sealed class FixedPointerViewportProvider
        : IPointerViewportProvider
    {
        public Rect CurrentViewport { get; }

        public FixedPointerViewportProvider(
            in Rect viewport)
        {
            if (!IsFinite(viewport.x) ||
                !IsFinite(viewport.y) ||
                !IsFinite(viewport.width) ||
                !IsFinite(viewport.height) ||
                viewport.width <= 0f ||
                viewport.height <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(viewport),
                    viewport,
                    "Viewport must have finite coordinates and positive size.");
            }

            CurrentViewport = viewport;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }
    }
}