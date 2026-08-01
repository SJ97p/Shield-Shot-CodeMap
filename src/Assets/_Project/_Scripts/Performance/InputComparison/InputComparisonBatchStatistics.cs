using System;
using System.Collections.Generic;

namespace Shield_Shot.Performance.InputComparison
{
    public sealed class InputComparisonBatchStatistics
    {
        public BenchmarkInputVersion Version { get; }
        public int RunCount { get; }
        public IReadOnlyList<InputComparisonMetricStatistics> Metrics { get; }
        public double AverageMarkerTotalMilliseconds { get; }
        public double MedianMarkerTotalMilliseconds { get; }
        public double P95MarkerTotalMilliseconds { get; }
        public double MaximumMarkerTotalMilliseconds { get; }

        private InputComparisonBatchStatistics(
            BenchmarkInputVersion version,
            int runCount,
            IReadOnlyList<InputComparisonMetricStatistics> metrics,
            NumericStatistics markerTotals)
        {
            Version = version;
            RunCount = runCount;
            Metrics = metrics;
            AverageMarkerTotalMilliseconds = markerTotals.Average;
            MedianMarkerTotalMilliseconds = markerTotals.Median;
            P95MarkerTotalMilliseconds = markerTotals.P95;
            MaximumMarkerTotalMilliseconds = markerTotals.Maximum;
        }

        public static InputComparisonBatchStatistics Create(
            BenchmarkInputVersion version,
            IReadOnlyList<InputComparisonRunResult> runs)
        {
            var valuesByMarker =
                new Dictionary<string, List<double>>();
            var callsByMarker =
                new Dictionary<string, long>();
            var markerTotals = new List<double>(runs.Count);

            for (int runIndex = 0; runIndex < runs.Count; runIndex++)
            {
                InputComparisonRunResult run = runs[runIndex];

                if (run.Version != version)
                {
                    throw new ArgumentException(
                        "All runs must use the requested input version.",
                        nameof(runs));
                }

                markerTotals.Add(run.MarkerTotalMilliseconds);

                for (int metricIndex = 0;
                     metricIndex < run.Metrics.Count;
                     metricIndex++)
                {
                    ProfilerMetricSummary metric =
                        run.Metrics[metricIndex];

                    if (!valuesByMarker.TryGetValue(
                            metric.MarkerName,
                            out List<double> values))
                    {
                        values = new List<double>(runs.Count);
                        valuesByMarker.Add(metric.MarkerName, values);
                        callsByMarker.Add(metric.MarkerName, 0);
                    }

                    values.Add(metric.TotalMilliseconds);
                    callsByMarker[metric.MarkerName] +=
                        metric.TotalCallCount;
                }
            }

            var metricStatistics =
                new List<InputComparisonMetricStatistics>(
                    valuesByMarker.Count);

            foreach (
                KeyValuePair<string, List<double>> pair
                in valuesByMarker)
            {
                NumericStatistics values =
                    NumericStatistics.Create(pair.Value);

                metricStatistics.Add(
                    new InputComparisonMetricStatistics(
                        pair.Key,
                        callsByMarker[pair.Key],
                        values));
            }

            metricStatistics.Sort(
                (left, right) =>
                    string.CompareOrdinal(
                        left.MarkerName,
                        right.MarkerName));

            return new InputComparisonBatchStatistics(
                version,
                runs.Count,
                metricStatistics,
                NumericStatistics.Create(markerTotals));
        }

        public sealed class InputComparisonMetricStatistics
        {
            public string MarkerName { get; }
            public long TotalCallCount { get; }
            public double AverageTotalMilliseconds { get; }
            public double MedianTotalMilliseconds { get; }
            public double P95TotalMilliseconds { get; }
            public double MaximumTotalMilliseconds { get; }

            internal InputComparisonMetricStatistics(
                string markerName,
                long totalCallCount,
                NumericStatistics statistics)
            {
                MarkerName = markerName;
                TotalCallCount = totalCallCount;
                AverageTotalMilliseconds = statistics.Average;
                MedianTotalMilliseconds = statistics.Median;
                P95TotalMilliseconds = statistics.P95;
                MaximumTotalMilliseconds = statistics.Maximum;
            }
        }

        internal readonly struct NumericStatistics
        {
            public double Average { get; }
            public double Median { get; }
            public double P95 { get; }
            public double Maximum { get; }

            private NumericStatistics(
                double average,
                double median,
                double p95,
                double maximum)
            {
                Average = average;
                Median = median;
                P95 = p95;
                Maximum = maximum;
            }

            public static NumericStatistics Create(
                IReadOnlyList<double> source)
            {
                if (source.Count == 0)
                {
                    return new NumericStatistics(0d, 0d, 0d, 0d);
                }

                var sorted = new List<double>(source.Count);
                double total = 0d;

                for (int index = 0; index < source.Count; index++)
                {
                    double value = source[index];
                    sorted.Add(value);
                    total += value;
                }

                sorted.Sort();

                int middle = sorted.Count / 2;
                double median =
                    (sorted.Count & 1) == 1
                        ? sorted[middle]
                        : (sorted[middle - 1] + sorted[middle]) * 0.5d;

                double p95Position =
                    (sorted.Count - 1) * 0.95d;
                int p95LowerIndex =
                    (int)Math.Floor(p95Position);
                int p95UpperIndex =
                    (int)Math.Ceiling(p95Position);
                double p95Blend =
                    p95Position - p95LowerIndex;
                double p95 =
                    sorted[p95LowerIndex] +
                    (sorted[p95UpperIndex] -
                     sorted[p95LowerIndex]) *
                    p95Blend;

                return new NumericStatistics(
                    total / sorted.Count,
                    median,
                    p95,
                    sorted[sorted.Count - 1]);
            }
        }
    }
}
