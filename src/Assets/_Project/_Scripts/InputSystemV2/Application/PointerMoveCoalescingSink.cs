using System;
using System.Collections.Generic;
using Shield_Shot.InputSystemV2.Domain;

namespace Shield_Shot.InputSystemV2.Application
{
    public sealed class PointerMoveCoalescingSink
        : IPointerFrameSink
    {
        private readonly IPointerSampleSink next;

        private readonly Dictionary<PointerKey, PointerSample>
            pendingMovements;

        public PointerMoveCoalescingSink(
            IPointerSampleSink next,
            int initialPointerCapacity = 4)
        {
            this.next = next
                ?? throw new ArgumentNullException(nameof(next));

            if (initialPointerCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialPointerCapacity));
            }

            pendingMovements =
                new Dictionary<PointerKey, PointerSample>(
                    initialPointerCapacity);
        }

        public void Receive(in PointerSample sample)
        {
            PointerKey key =
                PointerKey.From(in sample);

            if (sample.Phase == PointerPhase.Moved)
            {
                pendingMovements[key] = sample;
                return;
            }

            FlushMovement(key);
            next.Receive(in sample);
        }

        public void CompleteFrame()
        {
            foreach (KeyValuePair<PointerKey, PointerSample> entry
                     in pendingMovements)
            {
                PointerSample sample = entry.Value;
                next.Receive(in sample);
            }

            pendingMovements.Clear();
        }

        public void Reset()
        {
            pendingMovements.Clear();
        }

        private void FlushMovement(PointerKey key)
        {
            if (!pendingMovements.TryGetValue(
                    key,
                    out PointerSample pendingSample))
            {
                return;
            }

            pendingMovements.Remove(key);
            next.Receive(in pendingSample);
        }
    }
}