using System;
using System.Collections.Generic;

namespace Shield_Shot.Performance.InputComparison
{
    public sealed class BenchmarkPointerSequence
    {
        private readonly BenchmarkPointerSample[] samples;

        public string SequenceId
        {
            get;
        }

        public int Count =>
            samples.Length;

        public double Duration =>
            samples.Length > 0
                ? samples[samples.Length - 1].TimestampOffset
                : 0d;

        public BenchmarkPointerSample this[int index] =>
            samples[index];

        public BenchmarkPointerSequence(
            string sequenceId,
            IReadOnlyList<BenchmarkPointerSample> samples)
        {
            if (string.IsNullOrWhiteSpace(sequenceId))
            {
                throw new ArgumentException(
                    "Sequence ID cannot be empty.",
                    nameof(sequenceId));
            }

            if (samples == null)
            {
                throw new ArgumentNullException(
                    nameof(samples));
            }

            if (samples.Count == 0)
            {
                throw new ArgumentException(
                    "Sequence must contain at least one sample.",
                    nameof(samples));
            }

            SequenceId = sequenceId;

            this.samples =
                new BenchmarkPointerSample[samples.Count];

            for (int index = 0;
                 index < samples.Count;
                 index++)
            {
                this.samples[index] =
                    samples[index];
            }

            Validate();
        }

        private void Validate()
        {
            var activePointers =
                new HashSet<int>();

            double previousTimestamp = 0d;

            for (int index = 0;
                 index < samples.Length;
                 index++)
            {
                BenchmarkPointerSample sample =
                    samples[index];

                if (sample.TimestampOffset <
                    previousTimestamp)
                {
                    throw new ArgumentException(
                        $"Sample timestamps must be ordered. " +
                        $"Index: {index}, " +
                        $"Previous: {previousTimestamp}, " +
                        $"Current: {sample.TimestampOffset}");
                }

                ValidatePhaseTransition(
                    in sample,
                    activePointers,
                    index);

                previousTimestamp =
                    sample.TimestampOffset;
            }

            if (activePointers.Count > 0)
            {
                throw new ArgumentException(
                    "All active pointers must end with " +
                    "Ended or Canceled.");
            }
        }

        private static void ValidatePhaseTransition(
            in BenchmarkPointerSample sample,
            HashSet<int> activePointers,
            int sampleIndex)
        {
            switch (sample.Phase)
            {
                case BenchmarkPointerPhase.Began:
                    if (!activePointers.Add(
                            sample.PointerId))
                    {
                        throw new ArgumentException(
                            $"Pointer {sample.PointerId} began twice. " +
                            $"Sample index: {sampleIndex}");
                    }

                    break;

                case BenchmarkPointerPhase.Moved:
                case BenchmarkPointerPhase.Stationary:
                    RequireActivePointer(
                        in sample,
                        activePointers,
                        sampleIndex);

                    break;

                case BenchmarkPointerPhase.Ended:
                case BenchmarkPointerPhase.Canceled:
                    RequireActivePointer(
                        in sample,
                        activePointers,
                        sampleIndex);

                    activePointers.Remove(
                        sample.PointerId);

                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(sample.Phase),
                        sample.Phase,
                        "Unsupported benchmark pointer phase.");
            }
        }

        private static void RequireActivePointer(
            in BenchmarkPointerSample sample,
            HashSet<int> activePointers,
            int sampleIndex)
        {
            if (activePointers.Contains(
                    sample.PointerId))
            {
                return;
            }

            throw new ArgumentException(
                $"Pointer {sample.PointerId} received " +
                $"{sample.Phase} before Began. " +
                $"Sample index: {sampleIndex}");
        }
    }
}