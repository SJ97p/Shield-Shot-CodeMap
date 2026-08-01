using System;
using Shield_Shot.InputSystemV2.Domain;

namespace Shield_Shot.InputSystemV2.Application
{
    public sealed class FilteredPointerFrameSink
        : IPointerFrameSink
    {
        private readonly IPointerFilter filter;
        private readonly IPointerFrameSink next;

        public FilteredPointerFrameSink(
            IPointerFilter filter,
            IPointerFrameSink next)
        {
            this.filter = filter
                ?? throw new ArgumentNullException(
                    nameof(filter));

            this.next = next
                ?? throw new ArgumentNullException(
                    nameof(next));
        }

        public void Receive(in PointerSample sample)
        {
            if (!filter.Accept(in sample))
            {
                return;
            }

            next.Receive(in sample);
        }

        public void CompleteFrame()
        {
            next.CompleteFrame();
        }
    }
}