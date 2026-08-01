using System;
using System.Collections.Generic;
using Shield_Shot.InputSystemV2.Domain;

namespace Shield_Shot.InputSystemV2.Application
{
    public sealed class PointerStartBlockFilter
        : IPointerFilter
    {
        private readonly IPointerStartBlockPolicy blockPolicy;

        private readonly Dictionary<PointerKey, bool>
            blockedStates;

        public PointerStartBlockFilter(
            IPointerStartBlockPolicy blockPolicy,
            int initialPointerCapacity = 4)
        {
            this.blockPolicy = blockPolicy
                ?? throw new ArgumentNullException(
                    nameof(blockPolicy));

            if (initialPointerCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialPointerCapacity));
            }

            blockedStates =
                new Dictionary<PointerKey, bool>(
                    initialPointerCapacity);
        }

        public bool Accept(in PointerSample sample)
        {
            PointerKey key =
                PointerKey.From(in sample);

            if (sample.Phase == PointerPhase.Began)
            {
                bool isBlocked =
                    blockPolicy.ShouldBlock(in sample);

                blockedStates[key] = isBlocked;

                return !isBlocked;
            }

            if (!blockedStates.TryGetValue(
                    key,
                    out bool isPointerBlocked))
            {
                // Began을 받지 못한 포인터는 안전하게 거부한다.
                return false;
            }

            if (sample.Phase == PointerPhase.Ended ||
                sample.Phase == PointerPhase.Canceled)
            {
                blockedStates.Remove(key);
            }

            return !isPointerBlocked;
        }

        public void Reset()
        {
            blockedStates.Clear();
        }
    }
}