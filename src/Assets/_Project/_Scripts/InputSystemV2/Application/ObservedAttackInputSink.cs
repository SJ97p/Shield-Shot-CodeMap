using System;
using Shield_Shot.InputSystemV2.Combat.Domain;

namespace Shield_Shot.InputSystemV2.Combat.Application
{
    public sealed class ObservedAttackInputSink
        : IAttackInputSink
    {
        private readonly IAttackInputSink observer;
        private readonly IAttackInputSink next;

        public ObservedAttackInputSink(
            IAttackInputSink observer,
            IAttackInputSink next)
        {
            this.observer = observer
                ?? throw new ArgumentNullException(
                    nameof(observer));

            this.next = next
                ?? throw new ArgumentNullException(
                    nameof(next));
        }

        public void Receive(
            in AttackInputSample sample)
        {
            observer.Receive(in sample);
            next.Receive(in sample);
        }
    }
}