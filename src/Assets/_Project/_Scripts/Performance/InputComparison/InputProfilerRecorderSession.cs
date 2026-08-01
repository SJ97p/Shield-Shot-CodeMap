using System;
using System.Collections.Generic;
using Unity.Profiling;

namespace Shield_Shot.Performance.InputComparison
{
    public sealed class InputProfilerRecorderSession : IDisposable
    {
        private readonly List<MetricRecorder> recorders = new();

        public void Start(BenchmarkInputVersion version, int capacity)
        {
            Dispose();
            int safeCapacity = Math.Max(1, capacity);

            switch (version)
            {
                case BenchmarkInputVersion.V1:
                    AddRecorder("Input.V1.ProcessSamples", safeCapacity);
                    AddRecorder("Input.V1.GestureUpdate", safeCapacity);
                    break;
                case BenchmarkInputVersion.V2:
                    AddRecorder("Input.V2.ProcessSamples", safeCapacity);
                    AddRecorder("Input.V2.CompleteFrame", safeCapacity);
                    AddRecorder("Input.V2.AttackTick", safeCapacity);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(version),
                        version,
                        "Unsupported input version.");
            }
        }

        public IReadOnlyList<ProfilerMetricSummary> StopAndCollect()
        {
            var summaries =
                new List<ProfilerMetricSummary>(recorders.Count);

            foreach (MetricRecorder metricRecorder in recorders)
            {
                metricRecorder.Recorder.Stop();
                summaries.Add(
                    ProfilerMetricSummary.Create(
                        metricRecorder.MarkerName,
                        metricRecorder.Recorder));
            }

            Dispose();
            return summaries;
        }

        public void Dispose()
        {
            foreach (MetricRecorder metricRecorder in recorders)
            {
                metricRecorder.Recorder.Dispose();
            }

            recorders.Clear();
        }

        private void AddRecorder(string markerName, int capacity)
        {
            const ProfilerRecorderOptions options =
                ProfilerRecorderOptions.StartImmediately |
                ProfilerRecorderOptions.SumAllSamplesInFrame |
                ProfilerRecorderOptions.WrapAroundWhenCapacityReached;

            ProfilerRecorder recorder =
                ProfilerRecorder.StartNew(
                    ProfilerCategory.Scripts,
                    markerName,
                    capacity,
                    options);

            recorders.Add(new MetricRecorder(markerName, recorder));
        }

        private readonly struct MetricRecorder
        {
            public string MarkerName { get; }
            public ProfilerRecorder Recorder { get; }

            public MetricRecorder(
                string markerName,
                ProfilerRecorder recorder)
            {
                MarkerName = markerName;
                Recorder = recorder;
            }
        }
    }
}
