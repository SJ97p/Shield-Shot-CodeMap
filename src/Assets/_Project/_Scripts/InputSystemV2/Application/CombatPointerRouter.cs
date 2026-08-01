using System;
using System.Collections.Generic;
using Shield_Shot.InputSystemV2.Application;
using Shield_Shot.InputSystemV2.Combat.Domain;
using Shield_Shot.InputSystemV2.Domain;

namespace Shield_Shot.InputSystemV2.Combat.Application
{
    public sealed class CombatPointerRouter
        : IPointerSampleSink
    {
        private readonly ICombatInputChannelResolver
            channelResolver;

        private readonly IPointerSampleSink attackSink;
        private readonly IPointerSampleSink defenseSink;

        private readonly Dictionary<PointerKey, CombatInputChannel>
            activeChannels;

        public CombatPointerRouter(
            ICombatInputChannelResolver channelResolver,
            IPointerSampleSink attackSink,
            IPointerSampleSink defenseSink,
            int initialPointerCapacity = 4)
        {
            this.channelResolver = channelResolver
                ?? throw new ArgumentNullException(
                    nameof(channelResolver));

            this.attackSink = attackSink
                ?? throw new ArgumentNullException(
                    nameof(attackSink));

            this.defenseSink = defenseSink
                ?? throw new ArgumentNullException(
                    nameof(defenseSink));

            if (initialPointerCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialPointerCapacity));
            }

            activeChannels =
                new Dictionary<PointerKey, CombatInputChannel>(
                    initialPointerCapacity);
        }

        public void Receive(in PointerSample sample)
        {
            PointerKey key =
                PointerKey.From(in sample);

            if (sample.Phase == PointerPhase.Began)
            {
                CombatInputChannel channel =
                    channelResolver.Resolve(in sample);

                if (channel == CombatInputChannel.Unknown)
                {
                    return;
                }

                activeChannels[key] = channel;
                Route(channel, in sample);
                return;
            }

            if (!activeChannels.TryGetValue(
                    key,
                    out CombatInputChannel activeChannel))
            {
                return;
            }

            Route(activeChannel, in sample);

            if (sample.Phase == PointerPhase.Ended ||
                sample.Phase == PointerPhase.Canceled)
            {
                activeChannels.Remove(key);
            }
        }

        public void Reset()
        {
            activeChannels.Clear();
        }

        private void Route(
            CombatInputChannel channel,
            in PointerSample sample)
        {
            switch (channel)
            {
                case CombatInputChannel.Attack:
                    attackSink.Receive(in sample);
                    break;

                case CombatInputChannel.Defense:
                    defenseSink.Receive(in sample);
                    break;
            }
        }
    }
}