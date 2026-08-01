using System;
using System.Collections.Generic;
using Shield_Shot.InputSystemV2.Domain;
using UnityEngine;

namespace Shield_Shot.InputSystemV2.Application
{
    public sealed class PointerMovementThresholdFilter : IPointerFilter
    {
        private readonly float minimumDistanceSquared;
        private readonly Dictionary<PointerKey, Vector2>
            lastAcceptedPositions;

        public PointerMovementThresholdFilter(
            float minimumDistance,
            int initialPointerCapacity = 4)
        {
            if (minimumDistance < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumDistance));
            }

            if (initialPointerCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialPointerCapacity));
            }

            minimumDistanceSquared =
                minimumDistance * minimumDistance;

            lastAcceptedPositions =
                new Dictionary<PointerKey, Vector2>(
                    initialPointerCapacity);
        }

        public bool Accept(in PointerSample sample)
        {
            PointerKey key =
                PointerKey.From(in sample);

            switch (sample.Phase)
            {
                case PointerPhase.Began:
                    lastAcceptedPositions[key] =
                        sample.ScreenPosition;

                    return true;

                case PointerPhase.Moved:
                    return AcceptMovement(
                        key,
                        sample.ScreenPosition);

                case PointerPhase.Stationary:
                    return false;

                case PointerPhase.Ended:
                case PointerPhase.Canceled:
                    lastAcceptedPositions.Remove(key);
                    return true;

                default:
                    return false;
            }
        }

        private bool AcceptMovement(
            PointerKey key,
            Vector2 currentPosition)
        {
            if (!lastAcceptedPositions.TryGetValue(
                    key,
                    out Vector2 previousPosition))
            {
                lastAcceptedPositions[key] =
                    currentPosition;

                return true;
            }

            Vector2 delta =
                currentPosition - previousPosition;

            if (delta.sqrMagnitude <
                minimumDistanceSquared)
            {
                return false;
            }

            lastAcceptedPositions[key] =
                currentPosition;

            return true;
        }

        public void Reset()
        {
            lastAcceptedPositions.Clear();
        }
    }
}