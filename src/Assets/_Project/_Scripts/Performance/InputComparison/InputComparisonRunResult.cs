using System.Collections.Generic;

namespace Shield_Shot.Performance.InputComparison
{
    public sealed class InputComparisonRunResult
    {
        public BenchmarkInputVersion Version { get; }
        public IReadOnlyList<ProfilerMetricSummary> Metrics { get; }
        public double MarkerTotalMilliseconds { get; }

        public InputComparisonRunResult(
            BenchmarkInputVersion version,
            IReadOnlyList<ProfilerMetricSummary> metrics)
        {
            Version = version;
            Metrics = metrics;

            double markerTotalMilliseconds = 0d;

            for (int index = 0; index < metrics.Count; index++)
            {
                markerTotalMilliseconds +=
                    metrics[index].TotalMilliseconds;
            }

            MarkerTotalMilliseconds = markerTotalMilliseconds;
        }
    }
}
