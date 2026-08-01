using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shield_Shot.Performance.InputComparison
{
    [Serializable]
    public sealed class InputComparisonResultDocument
    {
        public string schemaVersion;
        public string completedAtUtc;
        public InputComparisonDeviceInfo device;
        public InputComparisonTestSettings settings;
        public InputComparisonVersionResult v1;
        public InputComparisonVersionResult v2;
        public double v1ToV2AverageMarkerTotalChangePercent;
        [TextArea] public string summary;

        public static InputComparisonResultDocument Create(
            InputComparisonBatchResult result,
            InputComparisonSmokeTestBehaviour source,
            InputComparisonDeviceInfoProvider deviceInfoProvider)
        {
            return new InputComparisonResultDocument
            {
                schemaVersion = "1.0",
                completedAtUtc =
                    result.CompletedAtUtc.ToString("O"),
                device = deviceInfoProvider.Capture(),
                settings = new InputComparisonTestSettings
                {
                    warmupRuns = result.WarmupRuns,
                    measuredRuns = result.MeasuredRuns,
                    durationSeconds = source.DurationSeconds,
                    samplesPerSecond = source.SamplesPerSecond,
                    viewportWidth = source.ViewportSize.x,
                    viewportHeight = source.ViewportSize.y,
                    recorderCapacity = source.RecorderCapacity
                },
                v1 = InputComparisonVersionResult.Create(result.V1),
                v2 = InputComparisonVersionResult.Create(result.V2),
                v1ToV2AverageMarkerTotalChangePercent =
                    CalculateChangePercent(
                        result.V1.AverageMarkerTotalMilliseconds,
                        result.V2.AverageMarkerTotalMilliseconds),
                summary = InputComparisonBatchReportBuilder.Build(
                    result.V1,
                    result.V2,
                    result.WarmupRuns)
            };
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

    [Serializable]
    public sealed class InputComparisonDeviceInfo
    {
        public string deviceModel;
        public string deviceName;
        public string operatingSystem;
        public string processorType;
        public int processorCount;
        public int processorFrequencyMHz;
        public int systemMemoryMB;
        public string graphicsDeviceName;
        public int graphicsMemoryMB;
        public int screenWidth;
        public int screenHeight;
        public int targetFrameRate;
        public int vSyncCount;
        public string platform;
        public string unityVersion;
        public string applicationVersion;
    }

    [Serializable]
    public sealed class InputComparisonTestSettings
    {
        public int warmupRuns;
        public int measuredRuns;
        public double durationSeconds;
        public int samplesPerSecond;
        public float viewportWidth;
        public float viewportHeight;
        public int recorderCapacity;
    }

    [Serializable]
    public sealed class InputComparisonVersionResult
    {
        public string version;
        public int runCount;
        public double averageMarkerTotalMilliseconds;
        public double medianMarkerTotalMilliseconds;
        public double p95MarkerTotalMilliseconds;
        public double maximumMarkerTotalMilliseconds;
        public List<InputComparisonMetricResult> metrics;

        public static InputComparisonVersionResult Create(
            InputComparisonBatchStatistics statistics)
        {
            var metricResults =
                new List<InputComparisonMetricResult>(
                    statistics.Metrics.Count);

            for (int index = 0;
                 index < statistics.Metrics.Count;
                 index++)
            {
                InputComparisonBatchStatistics
                    .InputComparisonMetricStatistics metric =
                        statistics.Metrics[index];

                metricResults.Add(
                    new InputComparisonMetricResult
                    {
                        markerName = metric.MarkerName,
                        totalCallCount = metric.TotalCallCount,
                        averageRunTotalMilliseconds =
                            metric.AverageTotalMilliseconds,
                        medianRunTotalMilliseconds =
                            metric.MedianTotalMilliseconds,
                        p95RunTotalMilliseconds =
                            metric.P95TotalMilliseconds,
                        maximumRunTotalMilliseconds =
                            metric.MaximumTotalMilliseconds
                    });
            }

            return new InputComparisonVersionResult
            {
                version = statistics.Version.ToString(),
                runCount = statistics.RunCount,
                averageMarkerTotalMilliseconds =
                    statistics.AverageMarkerTotalMilliseconds,
                medianMarkerTotalMilliseconds =
                    statistics.MedianMarkerTotalMilliseconds,
                p95MarkerTotalMilliseconds =
                    statistics.P95MarkerTotalMilliseconds,
                maximumMarkerTotalMilliseconds =
                    statistics.MaximumMarkerTotalMilliseconds,
                metrics = metricResults
            };
        }
    }

    [Serializable]
    public sealed class InputComparisonMetricResult
    {
        public string markerName;
        public long totalCallCount;
        public double averageRunTotalMilliseconds;
        public double medianRunTotalMilliseconds;
        public double p95RunTotalMilliseconds;
        public double maximumRunTotalMilliseconds;
    }
}
