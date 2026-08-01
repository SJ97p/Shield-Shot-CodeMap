using System;
using Shield_Shot.InputSystem;
using UnityEngine;
using Unity.Profiling;

using LegacyTouchPhase =
    UnityEngine.InputSystem.TouchPhase;

namespace Shield_Shot.Performance.InputComparison
{
    public sealed class V1BenchmarkInputAdapter
        : MonoBehaviour,
          IBenchmarkPointerInputTarget
    {
        [SerializeField]
        private TouchRouter touchRouter;

        [SerializeField]
        private InputProvider liveInputProvider;

        private Vector2 viewportSize;
        private bool sequenceActive;

        private static readonly ProfilerMarker
    ProcessSamplesMarker =
        new ProfilerMarker(
            "Input.V1.ProcessSamples");

        public void BeginSequence(
            Vector2 viewportSize,
            double startTimestamp)
        {
            if (touchRouter == null)
            {
                throw new InvalidOperationException(
                    "V1 TouchRouter is not assigned.");
            }

            if (liveInputProvider == null)
            {
                throw new InvalidOperationException(
                    "V1 InputProvider is not assigned.");
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

            liveInputProvider.enabled = false;

            this.viewportSize =
                viewportSize;

            touchRouter.ResetRouting(
                notifyCancellation: true);

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

                LegacyTouchPhase phase =
                    ConvertPhase(
                        sample.Phase);

                touchRouter.ProcessTouch(
                    sample.PointerId,
                    screenPosition,
                    phase);
            }
            finally
            {
                ProcessSamplesMarker.End();
            }
        }

        public void CompleteFrame()
        {
            /*
             * V1에는 프레임 단위 Coalescing이 없으므로
             * 수행할 작업이 없다.
             */
        }

        public void EndSequence()
        {
            if (!sequenceActive)
            {
                return;
            }

            try
            {
                touchRouter?.ResetRouting(
                    notifyCancellation: true);
            }
            finally
            {
                sequenceActive = false;
                viewportSize = Vector2.zero;
            }
        }

        private static LegacyTouchPhase ConvertPhase(
            BenchmarkPointerPhase phase)
        {
            switch (phase)
            {
                case BenchmarkPointerPhase.Began:
                    return LegacyTouchPhase.Began;

                case BenchmarkPointerPhase.Moved:
                    return LegacyTouchPhase.Moved;

                case BenchmarkPointerPhase.Stationary:
                    return LegacyTouchPhase.Stationary;

                case BenchmarkPointerPhase.Ended:
                    return LegacyTouchPhase.Ended;

                case BenchmarkPointerPhase.Canceled:
                    return LegacyTouchPhase.Canceled;

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

        public void ResumeLiveInput()
        {
            if (sequenceActive)
            {
                EndSequence();
            }

            if (liveInputProvider != null)
            {
                liveInputProvider.enabled = true;
            }
        }
    }
}