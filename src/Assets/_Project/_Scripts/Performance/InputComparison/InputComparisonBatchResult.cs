using System;

namespace Shield_Shot.Performance.InputComparison
{
    public sealed class InputComparisonBatchResult
    {
        public InputComparisonBatchStatistics V1 { get; }
        public InputComparisonBatchStatistics V2 { get; }
        public int WarmupRuns { get; }
        public int MeasuredRuns { get; }
        public DateTime CompletedAtUtc { get; }

        public InputComparisonBatchResult(
            InputComparisonBatchStatistics v1,
            InputComparisonBatchStatistics v2,
            int warmupRuns,
            int measuredRuns)
        {
            V1 = v1 ?? throw new ArgumentNullException(nameof(v1));
            V2 = v2 ?? throw new ArgumentNullException(nameof(v2));
            WarmupRuns = warmupRuns;
            MeasuredRuns = measuredRuns;
            CompletedAtUtc = DateTime.UtcNow;
        }
    }
}
