using System;
using System.Text;

namespace Shield_Shot.Performance.InputComparison
{
    public static class InputComparisonBatchReportBuilder
    {
        public static string Build(
            InputComparisonBatchStatistics v1,
            InputComparisonBatchStatistics v2,
            int warmupRuns)
        {
            var builder = new StringBuilder(2048);

            builder.Append("[Input Batch Comparison] Completed | Warmups: ")
                .Append(warmupRuns)
                .Append(" | Measured Runs Per Version: ")
                .Append(v1.RunCount);

            AppendVersion(builder, v1);
            AppendVersion(builder, v2);

            double changePercent =
                CalculateChangePercent(
                    v1.AverageMarkerTotalMilliseconds,
                    v2.AverageMarkerTotalMilliseconds);

            builder.AppendLine()
                .Append("=== V1 -> V2 Average Marker Total ===")
                .AppendLine()
                .Append("V1: ")
                .Append(v1.AverageMarkerTotalMilliseconds.ToString("F4"))
                .Append(" ms | V2: ")
                .Append(v2.AverageMarkerTotalMilliseconds.ToString("F4"))
                .Append(" ms | Change: ")
                .Append(changePercent.ToString("+0.00;-0.00;0.00"))
                .Append("%");

            return builder.ToString();
        }

        private static void AppendVersion(
            StringBuilder builder,
            InputComparisonBatchStatistics statistics)
        {
            builder.AppendLine()
                .Append("=== ")
                .Append(statistics.Version)
                .Append(" ===")
                .AppendLine()
                .Append("Marker Total per Run | Avg: ")
                .Append(
                    statistics.AverageMarkerTotalMilliseconds
                        .ToString("F4"))
                .Append(" ms | Median: ")
                .Append(
                    statistics.MedianMarkerTotalMilliseconds
                        .ToString("F4"))
                .Append(" ms | P95: ")
                .Append(
                    statistics.P95MarkerTotalMilliseconds
                        .ToString("F4"))
                .Append(" ms | Max: ")
                .Append(
                    statistics.MaximumMarkerTotalMilliseconds
                        .ToString("F4"))
                .Append(" ms");

            for (int index = 0;
                 index < statistics.Metrics.Count;
                 index++)
            {
                InputComparisonBatchStatistics
                    .InputComparisonMetricStatistics metric =
                        statistics.Metrics[index];

                builder.AppendLine()
                    .Append("  - ")
                    .Append(metric.MarkerName)
                    .Append(" | Calls: ")
                    .Append(metric.TotalCallCount)
                    .Append(" | Run Total Avg: ")
                    .Append(
                        metric.AverageTotalMilliseconds.ToString("F4"))
                    .Append(" ms | Median: ")
                    .Append(
                        metric.MedianTotalMilliseconds.ToString("F4"))
                    .Append(" ms | P95: ")
                    .Append(
                        metric.P95TotalMilliseconds.ToString("F4"))
                    .Append(" ms | Max: ")
                    .Append(
                        metric.MaximumTotalMilliseconds.ToString("F4"))
                    .Append(" ms");
            }
        }

        private static double CalculateChangePercent(
            double baseline,
            double candidate)
        {
            return baseline <= double.Epsilon
                ? 0d
                : ((candidate - baseline) / baseline) * 100d;
        }
    }
}
