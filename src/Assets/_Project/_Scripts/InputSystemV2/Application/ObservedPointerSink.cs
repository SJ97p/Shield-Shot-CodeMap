using System;
using Shield_Shot.InputSystemV2.Domain;

namespace Shield_Shot.InputSystemV2.Application
{
    public sealed class ObservedPointerSink
        : IPointerSampleSink
    {
        private readonly IPointerSampleSink observer;
        private readonly IPointerSampleSink next;

        public ObservedPointerSink(
            IPointerSampleSink observer,
            IPointerSampleSink next)
        {
            this.observer = observer
                ?? throw new ArgumentNullException(
                    nameof(observer));

            this.next = next
                ?? throw new ArgumentNullException(
                    nameof(next));
        }

        public void Receive(in PointerSample sample)
        {
            observer.Receive(in sample);
            next.Receive(in sample);
        }
    }
}