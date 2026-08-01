using System;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace Shield_Shot.Performance.InputComparison
{
    public sealed class BenchmarkPointerSequencePlayer
        : MonoBehaviour
    {
        private BenchmarkPointerSequence sequence;
        private IBenchmarkPointerInputTarget target;

        private Vector2 viewportSize;

        private double startTimestamp;
        private int nextSampleIndex;

        public bool IsPlaying
        {
            get;
            private set;
        }

        public int ProcessedSampleCount =>
            nextSampleIndex;

        public int TotalSampleCount =>
            sequence != null
                ? sequence.Count
                : 0;

        public float Progress
        {
            get
            {
                if (sequence == null ||
                    sequence.Count == 0)
                {
                    return 0f;
                }

                return Mathf.Clamp01(
                    (float)nextSampleIndex /
                    sequence.Count);
            }
        }

        public event Action<BenchmarkPointerSequence>
            SequenceCompleted;

        public void Play(
            BenchmarkPointerSequence sequence,
            IBenchmarkPointerInputTarget target,
            Vector2 viewportSize)
        {
            if (IsPlaying)
            {
                throw new InvalidOperationException(
                    "A benchmark sequence is already playing.");
            }

            this.sequence =
                sequence
                ?? throw new ArgumentNullException(
                    nameof(sequence));

            this.target =
                target
                ?? throw new ArgumentNullException(
                    nameof(target));

            ValidateViewportSize(
                viewportSize);

            this.viewportSize =
                viewportSize;

            startTimestamp =
                InputState.currentTime;

            nextSampleIndex = 0;

            this.target.BeginSequence(
                viewportSize,
                startTimestamp);

            IsPlaying = true;
        }

        public void Stop()
        {
            if (!IsPlaying)
            {
                return;
            }

            EndPlayback(
                notifyCompletion: false);
        }

        private void Update()
        {
            if (!IsPlaying)
            {
                return;
            }

            try
            {
                ProcessCurrentFrame();
            }
            catch
            {
                EndPlayback(
                    notifyCompletion: false);

                throw;
            }
        }

        private void OnDisable()
        {
            Stop();
        }

        private void ProcessCurrentFrame()
        {
            double currentTimestamp =
                InputState.currentTime;

            double elapsed =
                Math.Max(
                    0d,
                    currentTimestamp -
                    startTimestamp);

            while (nextSampleIndex <
                   sequence.Count)
            {
                BenchmarkPointerSample sample =
                    sequence[nextSampleIndex];

                if (sample.TimestampOffset >
                    elapsed)
                {
                    break;
                }

                target.Receive(
                    in sample);

                nextSampleIndex++;
            }

            /*
             * 이번 Unity Frame에 들어온 모든 Sample을
             * 전달한 후 프레임 종료를 알린다.
             *
             * V1: 수행 작업 없음
             * V2: 같은 프레임의 Moved Sample 병합 후 전달
             */
            target.CompleteFrame();

            if (nextSampleIndex >=
                sequence.Count)
            {
                EndPlayback(
                    notifyCompletion: true);
            }
        }

        private void EndPlayback(
            bool notifyCompletion)
        {
            BenchmarkPointerSequence
                completedSequence =
                    sequence;

            try
            {
                target?.EndSequence();
            }
            finally
            {
                IsPlaying = false;

                sequence = null;
                target = null;

                viewportSize = Vector2.zero;
                startTimestamp = 0d;
                nextSampleIndex = 0;
            }

            if (notifyCompletion &&
                completedSequence != null)
            {
                SequenceCompleted?.Invoke(
                    completedSequence);
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