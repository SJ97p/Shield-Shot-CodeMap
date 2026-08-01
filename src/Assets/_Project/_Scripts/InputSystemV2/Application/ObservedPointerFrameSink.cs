using System;
using Shield_Shot.InputSystemV2.Domain;

namespace Shield_Shot.InputSystemV2.Application
{
    public sealed class ObservedPointerFrameSink
        : IPointerFrameSink
    {
        private readonly IPointerSampleSink observer;
        private readonly IPointerFrameSink next;

        public ObservedPointerFrameSink(
            IPointerSampleSink observer,
            IPointerFrameSink next)
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

        public void CompleteFrame()
        {
            next.CompleteFrame();
        }
    }
}