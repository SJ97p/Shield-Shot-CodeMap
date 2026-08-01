using System;
using Shield_Shot.InputSystemV2.Application;
using Shield_Shot.InputSystemV2.Combat.Domain;
using Shield_Shot.InputSystemV2.Domain;
using UnityEngine;

namespace Shield_Shot.InputSystemV2.Combat.Application
{
    public sealed class SplitCombatInputChannelResolver
        : ICombatInputChannelResolver
    {
        private readonly ICombatInputLayoutProvider
            layoutProvider;

        private readonly IPointerViewportProvider
            viewportProvider;

        public SplitCombatInputChannelResolver(
            ICombatInputLayoutProvider layoutProvider,
            IPointerViewportProvider viewportProvider)
        {
            this.layoutProvider = layoutProvider
                ?? throw new ArgumentNullException(
                    nameof(layoutProvider));

            this.viewportProvider = viewportProvider
                ?? throw new ArgumentNullException(
                    nameof(viewportProvider));
        }

        public CombatInputChannel Resolve(
            in PointerSample beganSample)
        {
            if (beganSample.Phase != PointerPhase.Began)
            {
                throw new ArgumentException(
                    "Combat channel can only be resolved from a Began sample.",
                    nameof(beganSample));
            }

            CombatInputLayout layout =
                layoutProvider.CurrentLayout;

            Rect viewport =
                viewportProvider.CurrentViewport;

            if (viewport.width <= 0f ||
                viewport.height <= 0f)
            {
                throw new InvalidOperationException(
                    "The current pointer viewport is invalid.");
            }

            bool isFirstRegion =
                IsFirstRegion(
                    beganSample.ScreenPosition,
                    in viewport,
                    in layout);

            return ResolveChannel(
                isFirstRegion,
                layout.IsInverted);
        }

        private static bool IsFirstRegion(
            Vector2 screenPosition,
            in Rect viewport,
            in CombatInputLayout layout)
        {
            switch (layout.SplitDirection)
            {
                case CombatSplitDirection.LeftRight:
                    {
                        float boundary =
                            viewport.xMin +
                            viewport.width *
                            layout.SplitRatio;

                        return screenPosition.x < boundary;
                    }

                case CombatSplitDirection.BottomTop:
                    {
                        float boundary =
                            viewport.yMin +
                            viewport.height *
                            layout.SplitRatio;

                        return screenPosition.y < boundary;
                    }

                default:
                    throw new InvalidOperationException(
                        "Unsupported combat split direction.");
            }
        }

        private static CombatInputChannel ResolveChannel(
            bool isFirstRegion,
            bool isInverted)
        {
            if (isFirstRegion)
            {
                return isInverted
                    ? CombatInputChannel.Attack
                    : CombatInputChannel.Defense;
            }

            return isInverted
                ? CombatInputChannel.Defense
                : CombatInputChannel.Attack;
        }
    }
}