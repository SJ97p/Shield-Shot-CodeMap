using System;
using Shield_Shot.InputSystemV2.Application;
using UnityEngine;

namespace Shield_Shot.InputSystemV2.Infrastructure
{
    public sealed class ScreenPointerViewportProvider
        : IPointerViewportProvider
    {
        private readonly Rect fallbackViewport;

        public Rect CurrentViewport
        {
            get
            {
                int width = Screen.width;
                int height = Screen.height;

                if (width <= 0 || height <= 0)
                {
                    return fallbackViewport;
                }

                return new Rect(
                    0f,
                    0f,
                    width,
                    height);
            }
        }

        public ScreenPointerViewportProvider(
            in Rect fallbackViewport)
        {
            ValidateViewport(
                in fallbackViewport);

            this.fallbackViewport =
                fallbackViewport;
        }

        private static void ValidateViewport(
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
                    "Fallback viewport must have " +
                    "finite coordinates and positive size.");
            }
        }

        private static bool IsFinite(float value)
        {
            return
                !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }
    }
}