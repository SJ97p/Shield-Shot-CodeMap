using System;
using System.Collections;
using UnityEngine;

namespace Shield_Shot.Performance.InputComparison
{
    public sealed class InputComparisonBuildRunner : MonoBehaviour
    {
        [SerializeField]
        private InputComparisonSmokeTestBehaviour smokeTest;

        [Header("Automatic Run")]
        [SerializeField]
        private bool runOnlyInDevelopmentBuild = true;

        [SerializeField]
        private bool autoRunInEditor;

        [SerializeField, Min(0f)]
        private float startupDelaySeconds = 3f;

        [Header("Benchmark Frame Pacing")]
        [SerializeField, Min(1)]
        private int benchmarkTargetFrameRate = 60;

        [SerializeField]
        private bool disableVSyncDuringBenchmark = true;

        private readonly InputComparisonDeviceInfoProvider
            deviceInfoProvider = new();

        private IInputComparisonResultWriter resultWriter;
        private Coroutine startupCoroutine;
        private bool hasStarted;
        private bool framePacingApplied;
        private int previousTargetFrameRate;
        private int previousVSyncCount;

        private void Awake()
        {
            resultWriter = new JsonInputComparisonResultWriter();
        }

        private void OnEnable()
        {
            if (smokeTest != null)
            {
                smokeTest.BatchComparisonCompleted +=
                    HandleBatchComparisonCompleted;
            }

            startupCoroutine =
                StartCoroutine(StartBenchmarkWhenReady());
        }

        private void OnDisable()
        {
            if (smokeTest != null)
            {
                smokeTest.BatchComparisonCompleted -=
                    HandleBatchComparisonCompleted;
            }

            if (startupCoroutine != null)
            {
                StopCoroutine(startupCoroutine);
                startupCoroutine = null;
            }

            RestoreFramePacing();
        }

        private IEnumerator StartBenchmarkWhenReady()
        {
#if UNITY_EDITOR
            if (!autoRunInEditor)
            {
                yield break;
            }
#endif

            if (runOnlyInDevelopmentBuild &&
                !Debug.isDebugBuild)
            {
                Debug.LogWarning(
                    "[Input Build Benchmark] Skipped | " +
                    "This player is not a Development Build. " +
                    "Enable Development Build in Build Profiles " +
                    "or disable Run Only In Development Build.",
                    this);
                yield break;
            }

            if (hasStarted)
            {
                yield break;
            }

            if (smokeTest == null)
            {
                Debug.LogError(
                    "InputComparisonSmokeTestBehaviour is not assigned.",
                    this);
                yield break;
            }

            if (startupDelaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(
                    startupDelaySeconds);
            }

            ApplyBenchmarkFramePacing();

            double estimatedSeconds =
                smokeTest.DurationSeconds *
                (smokeTest.BatchWarmupRuns +
                 smokeTest.BatchMeasuredRuns) *
                2d;

            Debug.Log(
                $"[Input Build Benchmark] Starting | " +
                $"Estimated Duration: {estimatedSeconds:F0}s | " +
                $"The benchmark runs without progress logs.",
                this);

            hasStarted = smokeTest.TryRunBatchComparison(
                emitProgressLogs: false);

            if (!hasStarted)
            {
                RestoreFramePacing();

                Debug.LogError(
                    "Input comparison benchmark could not start.",
                    this);
            }

            startupCoroutine = null;
        }

        private void HandleBatchComparisonCompleted(
            InputComparisonBatchResult result)
        {
            try
            {
                InputComparisonResultDocument document =
                    InputComparisonResultDocument.Create(
                        result,
                        smokeTest,
                        deviceInfoProvider);
                string filePath = resultWriter.Write(document);

                /*
                 * This single post-measurement log does not contaminate
                 * the recorded benchmark interval.
                 */
                Debug.Log(
                    $"[Input Build Benchmark] Completed | " +
                    $"Result: {filePath}",
                    this);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                RestoreFramePacing();
            }
        }

        private void ApplyBenchmarkFramePacing()
        {
            if (framePacingApplied)
            {
                return;
            }

            previousTargetFrameRate =
                Application.targetFrameRate;
            previousVSyncCount =
                QualitySettings.vSyncCount;

            if (disableVSyncDuringBenchmark)
            {
                QualitySettings.vSyncCount = 0;
            }

            Application.targetFrameRate =
                Mathf.Max(1, benchmarkTargetFrameRate);

            framePacingApplied = true;
        }

        private void RestoreFramePacing()
        {
            if (!framePacingApplied)
            {
                return;
            }

            Application.targetFrameRate =
                previousTargetFrameRate;
            QualitySettings.vSyncCount =
                previousVSyncCount;

            framePacingApplied = false;
        }
    }
}
