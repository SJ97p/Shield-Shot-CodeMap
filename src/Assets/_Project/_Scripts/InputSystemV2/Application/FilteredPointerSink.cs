using System;
using Shield_Shot.InputSystemV2.Domain;

namespace Shield_Shot.InputSystemV2.Application
{
    public sealed class FilteredPointerSink : IPointerSampleSink
    {
        private readonly IPointerFilter filter;
        private readonly IPointerSampleSink next;

        public FilteredPointerSink(
            IPointerFilter filter,
            IPointerSampleSink next)
        {
            this.filter = filter
                ?? throw new ArgumentNullException(nameof(filter));

            this.next = next
                ?? throw new ArgumentNullException(nameof(next));
        }

        public void Receive(in PointerSample sample)
        {
            if (!filter.Accept(in sample))
            {
                return;
            }

            next.Receive(in sample);
        }
    }
}