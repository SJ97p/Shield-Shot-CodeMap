using System;
using Shield_Shot.InputSystemV2.Domain;
using Shield_Shot.InputSystemV2.Integration;
using UnityEngine;
using Unity.Profiling;

namespace Shield_Shot.Performance.InputComparison
{
    public sealed class V2BenchmarkInputAdapter
        : MonoBehaviour,
          IBenchmarkPointerInputTarget
    {
        [SerializeField]
        private InputSystemV2RuntimeBehaviour runtime;

        private Vector2 viewportSize;
        private double startTimestamp;
        private bool sequenceActive;

        private static readonly ProfilerMarker
    ProcessSamplesMarker =
        new ProfilerMarker(
            "Input.V2.ProcessSamples");

        private static readonly ProfilerMarker
            CompleteFrameMarker =
                new ProfilerMarker(
                    "Input.V2.CompleteFrame");

        public void BeginSequence(
            Vector2 viewportSize,
            double startTimestamp)
        {
            if (runtime == null)
            {
                throw new InvalidOperationException(
                    "Input System V2 Runtime is not assigned.");
            }

            ValidateViewportSize(
                viewportSize);

            if (double.IsNaN(startTimestamp) ||
                double.IsInfinity(startTimestamp) ||
                startTimestamp < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startTimestamp),
                    startTimestamp,
                    "Start timestamp must be finite " +
                    "and non-negative.");
            }

            if (sequenceActive)
            {
                throw new InvalidOperationException(
                    "A benchmark sequence is already active.");
            }

            this.viewportSize =
                viewportSize;

            this.startTimestamp =
                startTimestamp;

            runtime.BeginExternalInput();
            sequenceActive = true;
        }

        public void Receive(
    in BenchmarkPointerSample sample)
        {
            if (!sequenceActive)
            {
                throw new InvalidOperationException(
                    "BeginSequence must be called " +
                    "before receiving samples.");
            }

            ProcessSamplesMarker.Begin();

            try
            {
                Vector2 screenPosition =
                    sample.ToScreenPosition(
                        viewportSize);

                PointerSample pointerSample =
                    new PointerSample(
                        pointerId:
                            sample.PointerId,

                        deviceKind:
                            PointerDeviceKind.Touch,

                        phase:
                            ConvertPhase(
                                sample.Phase),

                        screenPosition:
                            screenPosition,

                        timestamp:
                            startTimestamp +
                            sample.TimestampOffset);

                runtime.SubmitExternalSample(
                    in pointerSample);
            }
            finally
            {
                ProcessSamplesMarker.End();
            }
        }

        public void CompleteFrame()
        {
            if (!sequenceActive)
            {
                return;
            }

            CompleteFrameMarker.Begin();

            try
            {
                runtime.CompleteExternalFrame();
            }
            finally
            {
                CompleteFrameMarker.End();
            }
        }

        public void EndSequence()
        {
            if (!sequenceActive)
            {
                return;
            }

            runtime.EndExternalInput();

            sequenceActive = false;
            viewportSize = Vector2.zero;
            startTimestamp = 0d;
        }

        public void ResumeLiveInput()
        {
            if (sequenceActive)
            {
                EndSequence();
            }

            runtime?.ResumeLiveInput();
        }

        private static PointerPhase ConvertPhase(
            BenchmarkPointerPhase phase)
        {
            switch (phase)
            {
                case BenchmarkPointerPhase.Began:
                    return PointerPhase.Began;

                case BenchmarkPointerPhase.Moved:
                    return PointerPhase.Moved;

                case BenchmarkPointerPhase.Stationary:
                    return PointerPhase.Stationary;

                case BenchmarkPointerPhase.Ended:
                    return PointerPhase.Ended;

                case BenchmarkPointerPhase.Canceled:
                    return PointerPhase.Canceled;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(phase),
                        phase,
                        "Unsupported benchmark phase.");
            }
        }

        private static void ValidateViewportSize(
            Vector2 size)
        {
            if (!IsFinite(size.x) ||
                !IsFinite(size.y) ||
                size.x <= 0f ||
                size.y <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(size),
                    size,
                    "Viewport size must be finite " +
                    "and positive.");
            }
        }

        private static bool IsFinite(float value)
        {
            return
                !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }
    }
}