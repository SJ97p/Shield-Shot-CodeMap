using System.Collections.Generic;
using Unity.Profiling;

namespace Shield_Shot.Performance.InputComparison
{
    public sealed class ProfilerMetricSummary
    {
        private const double NanosecondsToMilliseconds = 0.000001d;

        public string MarkerName { get; }
        public int ActiveFrameCount { get; }
        public long TotalCallCount { get; }
        public double TotalMilliseconds { get; }
        public double AverageMilliseconds { get; }
        public double MedianMilliseconds { get; }
        public double MaximumMilliseconds { get; }

        private ProfilerMetricSummary(
            string markerName,
            int activeFrameCount,
            long totalCallCount,
            double totalMilliseconds,
            double averageMilliseconds,
            double medianMilliseconds,
            double maximumMilliseconds)
        {
            MarkerName = markerName;
            ActiveFrameCount = activeFrameCount;
            TotalCallCount = totalCallCount;
            TotalMilliseconds = totalMilliseconds;
            AverageMilliseconds = averageMilliseconds;
            MedianMilliseconds = medianMilliseconds;
            MaximumMilliseconds = maximumMilliseconds;
        }

        public static ProfilerMetricSummary Create(
            string markerName,
            ProfilerRecorder recorder)
        {
            var samples =
                new List<ProfilerRecorderSample>(recorder.Capacity);
            recorder.CopyTo(samples);

            var frameMilliseconds =
                new List<double>(samples.Count);
            long totalCallCount = 0;
            double totalMilliseconds = 0d;
            double maximumMilliseconds = 0d;

            foreach (ProfilerRecorderSample sample in samples)
            {
                if (sample.Count <= 0)
                {
                    continue;
                }

                double milliseconds =
                    sample.Value * NanosecondsToMilliseconds;

                frameMilliseconds.Add(milliseconds);
                totalCallCount += sample.Count;
                totalMilliseconds += milliseconds;
                maximumMilliseconds =
                    System.Math.Max(maximumMilliseconds, milliseconds);
            }

            frameMilliseconds.Sort();
            int activeFrameCount = frameMilliseconds.Count;
            double averageMilliseconds =
                activeFrameCount == 0
                    ? 0d
                    : totalMilliseconds / activeFrameCount;

            return new ProfilerMetricSummary(
                markerName,
                activeFrameCount,
                totalCallCount,
                totalMilliseconds,
                averageMilliseconds,
                CalculateMedian(frameMilliseconds),
                maximumMilliseconds);
        }

        private static double CalculateMedian(
            IReadOnlyList<double> sortedValues)
        {
            int count = sortedValues.Count;

            if (count == 0)
            {
                return 0d;
            }

            int middle = count / 2;

            return (count & 1) == 1
                ? sortedValues[middle]
                : (sortedValues[middle - 1] + sortedValues[middle])
                    * 0.5d;
        }
    }
}
