using System;
using System.Collections.Generic;
using Shield_Shot.InputSystemV2.Application;
using Shield_Shot.InputSystemV2.Domain;
using Shield_Shot.InputSystemV2.Gestures.Domain;
using UnityEngine;

namespace Shield_Shot.InputSystemV2.Gestures.Application
{
    public sealed class PointerGestureTracker
        : IPointerSampleSink
    {
        private readonly IPointerGestureSink next;

        private readonly Dictionary<PointerKey, TrackedGesture>
            trackedGestures;

        public PointerGestureTracker(
            IPointerGestureSink next,
            int initialPointerCapacity = 4)
        {
            this.next = next
                ?? throw new ArgumentNullException(
                    nameof(next));

            if (initialPointerCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialPointerCapacity));
            }

            trackedGestures =
                new Dictionary<PointerKey, TrackedGesture>(
                    initialPointerCapacity);
        }

        public void Receive(in PointerSample sample)
        {
            PointerKey key =
                PointerKey.From(in sample);

            switch (sample.Phase)
            {
                case PointerPhase.Began:
                    Begin(key, in sample);
                    break;

                case PointerPhase.Moved:
                    Change(key, in sample);
                    break;

                case PointerPhase.Stationary:
                    break;

                case PointerPhase.Ended:
                    End(
                        key,
                        PointerGesturePhase.Completed,
                        in sample);
                    break;

                case PointerPhase.Canceled:
                    End(
                        key,
                        PointerGesturePhase.Canceled,
                        in sample);
                    break;
            }
        }

        public void Reset()
        {
            trackedGestures.Clear();
        }

        private void Begin(
            PointerKey key,
            in PointerSample sample)
        {
            TrackedGesture tracked =
                new TrackedGesture(
                    sample.ScreenPosition,
                    sample.Timestamp);

            trackedGestures[key] = tracked;

            PointerGestureSample gesture =
                new PointerGestureSample(
                    pointer: key,
                    phase: PointerGesturePhase.Began,
                    startPosition: tracked.StartPosition,
                    previousPosition: tracked.CurrentPosition,
                    currentPosition: tracked.CurrentPosition,
                    startTimestamp: tracked.StartTimestamp,
                    timestamp: sample.Timestamp);

            next.Receive(in gesture);
        }

        private void Change(
            PointerKey key,
            in PointerSample sample)
        {
            if (!trackedGestures.TryGetValue(
                    key,
                    out TrackedGesture tracked))
            {
                return;
            }

            PointerGestureSample gesture =
                new PointerGestureSample(
                    pointer: key,
                    phase: PointerGesturePhase.Changed,
                    startPosition: tracked.StartPosition,
                    previousPosition: tracked.CurrentPosition,
                    currentPosition: sample.ScreenPosition,
                    startTimestamp: tracked.StartTimestamp,
                    timestamp: sample.Timestamp);

            tracked.CurrentPosition =
                sample.ScreenPosition;

            trackedGestures[key] = tracked;

            next.Receive(in gesture);
        }

        private void End(
            PointerKey key,
            PointerGesturePhase finalPhase,
            in PointerSample sample)
        {
            if (!trackedGestures.TryGetValue(
                    key,
                    out TrackedGesture tracked))
            {
                return;
            }

            trackedGestures.Remove(key);

            PointerGestureSample gesture =
                new PointerGestureSample(
                    pointer: key,
                    phase: finalPhase,
                    startPosition: tracked.StartPosition,
                    previousPosition: tracked.CurrentPosition,
                    currentPosition: sample.ScreenPosition,
                    startTimestamp: tracked.StartTimestamp,
                    timestamp: sample.Timestamp);

            next.Receive(in gesture);
        }

        private struct TrackedGesture
        {
            public Vector2 StartPosition;
            public Vector2 CurrentPosition;
            public double StartTimestamp;

            public TrackedGesture(
                Vector2 startPosition,
                double startTimestamp)
            {
                StartPosition = startPosition;
                CurrentPosition = startPosition;
                StartTimestamp = startTimestamp;
            }
        }
    }
}