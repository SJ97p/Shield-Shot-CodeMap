using System;
using Shield_Shot.InputSystemV2.Domain;
using UnityEngine;

namespace Shield_Shot.InputSystemV2.Application
{
    public sealed class ViewportPointerStartBlockPolicy
        : IPointerStartBlockPolicy
    {
        private readonly IPointerViewportProvider
            viewportProvider;

        public ViewportPointerStartBlockPolicy(
            IPointerViewportProvider viewportProvider)
        {
            this.viewportProvider = viewportProvider
                ?? throw new ArgumentNullException(
                    nameof(viewportProvider));
        }

        public bool ShouldBlock(
            in PointerSample beganSample)
        {
            Rect viewport =
                viewportProvider.CurrentViewport;

            if (viewport.width <= 0f ||
                viewport.height <= 0f)
            {
                return true;
            }

            return !viewport.Contains(
                beganSample.ScreenPosition);
        }
    }
}