using System;
using UnityEngine;

namespace Shield_Shot.Performance.InputComparison
{
    public static class LinearDragBenchmarkSequenceFactory
    {
        public static BenchmarkPointerSequence Create(
            string sequenceId,
            int pointerId,
            Vector2 startPosition,
            Vector2 endPosition,
            double durationSeconds,
            int samplesPerSecond)
        {
            ValidateArguments(
                startPosition,
                endPosition,
                durationSeconds,
                samplesPerSecond);

            int intervalCount =
                CalculateIntervalCount(
                    durationSeconds,
                    samplesPerSecond);

            var samples =
                new BenchmarkPointerSample[
                    intervalCount + 1];

            for (int index = 0;
                 index <= intervalCount;
                 index++)
            {
                float progress =
                    (float)index /
                    intervalCount;

                Vector2 position =
                    Vector2.Lerp(
                        startPosition,
                        endPosition,
                        progress);

                double timestamp =
                    durationSeconds *
                    index /
                    intervalCount;

                BenchmarkPointerPhase phase =
                    ResolvePhase(
                        index,
                        intervalCount);

                samples[index] =
                    new BenchmarkPointerSample(
                        pointerId,
                        phase,
                        position,
                        timestamp);
            }

            return new BenchmarkPointerSequence(
                sequenceId,
                samples);
        }

        private static BenchmarkPointerPhase ResolvePhase(
            int index,
            int intervalCount)
        {
            if (index == 0)
            {
                return BenchmarkPointerPhase.Began;
            }

            if (index == intervalCount)
            {
                return BenchmarkPointerPhase.Ended;
            }

            return BenchmarkPointerPhase.Moved;
        }

        private static int CalculateIntervalCount(
            double durationSeconds,
            int samplesPerSecond)
        {
            double calculatedCount =
                Math.Ceiling(
                    durationSeconds *
                    samplesPerSecond);

            if (calculatedCount >
                int.MaxValue - 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(durationSeconds),
                    "Requested sequence is too large.");
            }

            return Math.Max(
                1,
                (int)calculatedCount);
        }

        private static void ValidateArguments(
            Vector2 startPosition,
            Vector2 endPosition,
            double durationSeconds,
            int samplesPerSecond)
        {
            ValidateNormalizedPosition(
                startPosition,
                nameof(startPosition));

            ValidateNormalizedPosition(
                endPosition,
                nameof(endPosition));

            if (double.IsNaN(durationSeconds) ||
                double.IsInfinity(durationSeconds) ||
                durationSeconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(durationSeconds),
                    durationSeconds,
                    "Duration must be finite and positive.");
            }

            if (samplesPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(samplesPerSecond),
                    samplesPerSecond,
                    "Samples per second must be positive.");
            }
        }

        private static void ValidateNormalizedPosition(
            Vector2 position,
            string parameterName)
        {
            if (!IsFinite(position.x) ||
                !IsFinite(position.y) ||
                position.x < 0f ||
                position.x > 1f ||
                position.y < 0f ||
                position.y > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    position,
                    "Position must be finite and normalized.");
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