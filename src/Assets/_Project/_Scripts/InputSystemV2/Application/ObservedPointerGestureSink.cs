using System;
using Shield_Shot.InputSystemV2.Gestures.Domain;

namespace Shield_Shot.InputSystemV2.Gestures.Application
{
    public sealed class ObservedPointerGestureSink
        : IPointerGestureSink
    {
        private readonly IPointerGestureSink observer;
        private readonly IPointerGestureSink next;

        public ObservedPointerGestureSink(
            IPointerGestureSink observer,
            IPointerGestureSink next)
        {
            this.observer = observer
                ?? throw new ArgumentNullException(
                    nameof(observer));

            this.next = next
                ?? throw new ArgumentNullException(
                    nameof(next));
        }

        public void Receive(
            in PointerGestureSample gesture)
        {
            observer.Receive(in gesture);
            next.Receive(in gesture);
        }
    }
}