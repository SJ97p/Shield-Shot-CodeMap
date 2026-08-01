using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Shield_Shot.Performance.InputComparison
{
    public sealed class InputComparisonSmokeTestBehaviour : MonoBehaviour
    {
        [Header("Control")]
        [SerializeField] private BenchmarkInputModeController modeController;
        [SerializeField] private BenchmarkPointerSequencePlayer sequencePlayer;

        [Header("Input Targets")]
        [SerializeField] private V1BenchmarkInputAdapter v1Adapter;
        [SerializeField] private V2BenchmarkInputAdapter v2Adapter;

        [Header("Viewport")]
        [SerializeField] private Vector2 viewportSize =
            new Vector2(1080f, 1920f);

        [Header("Linear Drag Scenario")]
        [SerializeField] private Vector2 startPosition =
            new Vector2(0.75f, 0.25f);
        [SerializeField] private Vector2 endPosition =
            new Vector2(0.75f, 0.75f);
        [SerializeField, Min(0.01f)] private double durationSeconds = 1d;
        [SerializeField, Min(1)] private int samplesPerSecond = 120;

        [Header("Completion")]
        [SerializeField] private bool resumeLiveInputAfterTest = true;

        [Header("Profiler Recorder")]
        [SerializeField] private bool recordProfilerMetrics = true;
        [SerializeField, Min(1)] private int recorderCapacity = 2048;

        [Header("Batch Comparison")]
        [SerializeField, Min(0)] private int batchWarmupRuns = 1;
        [SerializeField, Min(1)] private int batchMeasuredRuns = 10;
        [SerializeField, Min(0)] private int batchDelayFrames = 1;

        private readonly InputProfilerRecorderSession recorderSession = new();
        private Coroutine finalizeMeasurementCoroutine;
        private Coroutine batchComparisonCoroutine;
        private BenchmarkInputVersion activeMeasuredVersion;
        private bool batchIsRunning;
        private bool emitActiveBatchLogs;

        public bool IsBatchRunning => batchIsRunning;
        public double DurationSeconds => durationSeconds;
        public int SamplesPerSecond => samplesPerSecond;
        public Vector2 ViewportSize => ResolveViewportSize();
        public int RecorderCapacity => recorderCapacity;
        public int BatchWarmupRuns => batchWarmupRuns;
        public int BatchMeasuredRuns => batchMeasuredRuns;

        public event Action<InputComparisonBatchResult>
            BatchComparisonCompleted;

        private void OnEnable()
        {
            if (sequencePlayer != null)
            {
                sequencePlayer.SequenceCompleted += HandleSequenceCompleted;
            }
        }

        private void OnDisable()
        {
            if (sequencePlayer != null)
            {
                sequencePlayer.SequenceCompleted -= HandleSequenceCompleted;
            }

            if (finalizeMeasurementCoroutine != null)
            {
                StopCoroutine(finalizeMeasurementCoroutine);
                finalizeMeasurementCoroutine = null;
            }

            if (batchComparisonCoroutine != null)
            {
                StopCoroutine(batchComparisonCoroutine);
                batchComparisonCoroutine = null;
            }

            batchIsRunning = false;
            sequencePlayer?.Stop();
            recorderSession.Dispose();
        }

        [ContextMenu("Run Selected Input Smoke Test")]
        public void RunSelectedInputSmokeTest()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "Smoke Test can only run in Play Mode.",
                    this);
                return;
            }

            if (!ValidateReferences())
            {
                return;
            }

            if (sequencePlayer.IsPlaying ||
                finalizeMeasurementCoroutine != null ||
                batchIsRunning)
            {
                Debug.LogWarning(
                    "A benchmark sequence is already playing.",
                    this);
                return;
            }

            modeController.ApplySelectedVersion();

            BenchmarkInputVersion version =
                modeController.SelectedVersion;
            IBenchmarkPointerInputTarget target =
                ResolveTarget(version);
            BenchmarkPointerSequence sequence =
                LinearDragBenchmarkSequenceFactory.Create(
                    "INPUT_LINEAR_DRAG_SMOKE",
                    0,
                    startPosition,
                    endPosition,
                    durationSeconds,
                    samplesPerSecond);

            Debug.Log(
                $"[Input Smoke Test] Started | Version: {version} | " +
                $"Samples: {sequence.Count} | " +
                $"Duration: {sequence.Duration:F3}s",
                this);

            activeMeasuredVersion = version;

            try
            {
                sequencePlayer.Play(
                    sequence,
                    target,
                    ResolveViewportSize());

                if (recordProfilerMetrics)
                {
                    recorderSession.Start(version, recorderCapacity);
                }
            }
            catch (Exception exception)
            {
                sequencePlayer.Stop();
                recorderSession.Dispose();
                Debug.LogException(exception, this);
            }
        }

        [ContextMenu("Run V1 V2 Batch Comparison")]
        public void RunBatchComparison()
        {
            TryRunBatchComparison(emitProgressLogs: true);
        }

        public bool TryRunBatchComparison(bool emitProgressLogs)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "Batch Comparison can only run in Play Mode.",
                    this);
                return false;
            }

            if (!ValidateReferences())
            {
                return false;
            }

            if (sequencePlayer.IsPlaying ||
                finalizeMeasurementCoroutine != null ||
                batchIsRunning)
            {
                Debug.LogWarning(
                    "An input benchmark is already running.",
                    this);
                return false;
            }

            if (!recordProfilerMetrics)
            {
                Debug.LogError(
                    "Record Profiler Metrics must be enabled " +
                    "for a batch comparison.",
                    this);
                return false;
            }

            emitActiveBatchLogs = emitProgressLogs;
            batchComparisonCoroutine =
                StartCoroutine(RunBatchComparisonCoroutine());

            return true;
        }

        private IEnumerator RunBatchComparisonCoroutine()
        {
            batchIsRunning = true;

            BenchmarkInputVersion originalVersion =
                modeController.SelectedVersion;
            var v1Runs = new List<InputComparisonRunResult>(
                batchMeasuredRuns);
            var v2Runs = new List<InputComparisonRunResult>(
                batchMeasuredRuns);

            if (emitActiveBatchLogs)
            {
                Debug.Log(
                    $"[Input Batch Comparison] Started | " +
                    $"Warmups: {batchWarmupRuns} | " +
                    $"Measured Runs Per Version: {batchMeasuredRuns}",
                    this);
            }

            yield return RunVersionBatch(
                BenchmarkInputVersion.V1,
                v1Runs);
            yield return RunVersionBatch(
                BenchmarkInputVersion.V2,
                v2Runs);

            InputComparisonBatchStatistics v1Statistics =
                InputComparisonBatchStatistics.Create(
                    BenchmarkInputVersion.V1,
                    v1Runs);
            InputComparisonBatchStatistics v2Statistics =
                InputComparisonBatchStatistics.Create(
                    BenchmarkInputVersion.V2,
                    v2Runs);

            var result = new InputComparisonBatchResult(
                v1Statistics,
                v2Statistics,
                batchWarmupRuns,
                batchMeasuredRuns);

            if (emitActiveBatchLogs)
            {
                Debug.Log(
                    InputComparisonBatchReportBuilder.Build(
                        v1Statistics,
                        v2Statistics,
                        batchWarmupRuns),
                    this);
            }

            modeController.SelectVersion(originalVersion);
            ResumeLiveInput(originalVersion);

            batchIsRunning = false;
            batchComparisonCoroutine = null;
            BatchComparisonCompleted?.Invoke(result);
        }

        private IEnumerator RunVersionBatch(
            BenchmarkInputVersion version,
            ICollection<InputComparisonRunResult> measuredResults)
        {
            int totalRuns = batchWarmupRuns + batchMeasuredRuns;

            for (int runIndex = 0; runIndex < totalRuns; runIndex++)
            {
                bool isWarmup = runIndex < batchWarmupRuns;
                int displayIndex =
                    isWarmup
                        ? runIndex + 1
                        : runIndex - batchWarmupRuns + 1;
                int displayTotal =
                    isWarmup
                        ? batchWarmupRuns
                        : batchMeasuredRuns;

                modeController.SelectVersion(version);

                // Allow enable/disable transitions to settle.
                yield return null;

                BenchmarkPointerSequence sequence =
                    CreateBenchmarkSequence();
                IBenchmarkPointerInputTarget target =
                    ResolveTarget(version);

                sequencePlayer.Play(
                    sequence,
                    target,
                    ResolveViewportSize());
                recorderSession.Start(version, recorderCapacity);

                while (sequencePlayer.IsPlaying)
                {
                    yield return null;
                }

                // Commit the final frame into ProfilerRecorder.
                yield return null;

                IReadOnlyList<ProfilerMetricSummary> summaries =
                    recorderSession.StopAndCollect();

                if (!isWarmup)
                {
                    measuredResults.Add(
                        new InputComparisonRunResult(
                            version,
                            summaries));
                }

                if (emitActiveBatchLogs)
                {
                    Debug.Log(
                        $"[Input Batch Comparison] {version} | " +
                        $"{(isWarmup ? "Warmup" : "Measured")} " +
                        $"{displayIndex}/{displayTotal} completed",
                        this);
                }

                for (int delay = 0;
                     delay < batchDelayFrames;
                     delay++)
                {
                    yield return null;
                }
            }
        }

        private BenchmarkPointerSequence CreateBenchmarkSequence()
        {
            return LinearDragBenchmarkSequenceFactory.Create(
                "INPUT_LINEAR_DRAG_SMOKE",
                0,
                startPosition,
                endPosition,
                durationSeconds,
                samplesPerSecond);
        }

        private Vector2 ResolveViewportSize()
        {
            int width = Screen.width;
            int height = Screen.height;

            return width > 0 && height > 0
                ? new Vector2(width, height)
                : viewportSize;
        }

        private IBenchmarkPointerInputTarget ResolveTarget(
            BenchmarkInputVersion version)
        {
            return version switch
            {
                BenchmarkInputVersion.V1 => v1Adapter,
                BenchmarkInputVersion.V2 => v2Adapter,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(version),
                    version,
                    "Unsupported input version.")
            };
        }

        private void HandleSequenceCompleted(
            BenchmarkPointerSequence sequence)
        {
            if (batchIsRunning)
            {
                return;
            }

            if (finalizeMeasurementCoroutine != null)
            {
                return;
            }

            finalizeMeasurementCoroutine =
                StartCoroutine(FinalizeMeasurementNextFrame(sequence));
        }

        private IEnumerator FinalizeMeasurementNextFrame(
            BenchmarkPointerSequence sequence)
        {
            // Let the final frame's profiler samples commit first.
            yield return null;

            IReadOnlyList<ProfilerMetricSummary> summaries =
                recordProfilerMetrics
                    ? recorderSession.StopAndCollect()
                    : Array.Empty<ProfilerMetricSummary>();

            Debug.Log(
                BuildCompletionReport(
                    activeMeasuredVersion,
                    sequence,
                    summaries),
                this);

            ResumeLiveInput(activeMeasuredVersion);
            finalizeMeasurementCoroutine = null;
        }

        private static string BuildCompletionReport(
            BenchmarkInputVersion version,
            BenchmarkPointerSequence sequence,
            IReadOnlyList<ProfilerMetricSummary> summaries)
        {
            var builder = new StringBuilder(512);

            builder.Append("[Input Smoke Test] Completed | Version: ")
                .Append(version)
                .Append(" | Sequence: ")
                .Append(sequence.SequenceId)
                .Append(" | Samples: ")
                .Append(sequence.Count);

            double totalMarkerMilliseconds = 0d;

            for (int index = 0; index < summaries.Count; index++)
            {
                ProfilerMetricSummary summary = summaries[index];
                totalMarkerMilliseconds += summary.TotalMilliseconds;

                builder.AppendLine()
                    .Append("  - ")
                    .Append(summary.MarkerName)
                    .Append(" | Frames: ")
                    .Append(summary.ActiveFrameCount)
                    .Append(" | Calls: ")
                    .Append(summary.TotalCallCount)
                    .Append(" | Total: ")
                    .Append(summary.TotalMilliseconds.ToString("F4"))
                    .Append(" ms | Avg/ActiveFrame: ")
                    .Append(summary.AverageMilliseconds.ToString("F4"))
                    .Append(" ms | Median: ")
                    .Append(summary.MedianMilliseconds.ToString("F4"))
                    .Append(" ms | Max: ")
                    .Append(summary.MaximumMilliseconds.ToString("F4"))
                    .Append(" ms");
            }

            if (summaries.Count > 0)
            {
                builder.AppendLine()
                    .Append("  = Marker Total: ")
                    .Append(totalMarkerMilliseconds.ToString("F4"))
                    .Append(" ms");
            }

            return builder.ToString();
        }

        private void ResumeLiveInput(BenchmarkInputVersion version)
        {
            if (!resumeLiveInputAfterTest)
            {
                return;
            }

            switch (version)
            {
                case BenchmarkInputVersion.V1:
                    v1Adapter.ResumeLiveInput();
                    break;
                case BenchmarkInputVersion.V2:
                    v2Adapter.ResumeLiveInput();
                    break;
            }
        }

        private bool ValidateReferences()
        {
            if (modeController == null)
            {
                Debug.LogError(
                    "BenchmarkInputModeController is not assigned.",
                    this);
                return false;
            }

            if (sequencePlayer == null)
            {
                Debug.LogError(
                    "BenchmarkPointerSequencePlayer is not assigned.",
                    this);
                return false;
            }

            if (v1Adapter == null)
            {
                Debug.LogError(
                    "V1BenchmarkInputAdapter is not assigned.",
                    this);
                return false;
            }

            if (v2Adapter == null)
            {
                Debug.LogError(
                    "V2BenchmarkInputAdapter is not assigned.",
                    this);
                return false;
            }

            return true;
        }
    }
}
