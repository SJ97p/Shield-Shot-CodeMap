using Shield_Shot.InputSystem;
using Shield_Shot.InputSystemV2.Integration;
using UnityEngine;

namespace Shield_Shot.Performance.InputComparison
{
    [DefaultExecutionOrder(-10000)]
    public sealed class BenchmarkInputModeController
        : MonoBehaviour
    {
        [Header("Selected Version")]
        [SerializeField]
        private BenchmarkInputVersion selectedVersion =
            BenchmarkInputVersion.V2;

        [Header("Input Runtimes")]
        [SerializeField]
        private InputProvider inputSystemV1;

        [SerializeField]
        private InputSystemV2RuntimeBehaviour inputSystemV2;

        public BenchmarkInputVersion SelectedVersion =>
            selectedVersion;

        private void Awake()
        {
            ApplySelectedVersion();
        }

        private void OnEnable()
        {
            ApplySelectedVersion();
        }

        [ContextMenu("Apply Selected Input Version")]
        public void ApplySelectedVersion()
        {
            if (!ValidateReferences())
            {
                return;
            }

            /*
             * 반드시 양쪽을 먼저 끈다.
             *
             * 기존 경로에 활성 입력이 있다면 비활성화 과정에서
             * 가능한 상태 정리가 먼저 수행된다.
             */
            inputSystemV1.enabled = false;
            inputSystemV2.enabled = false;

            switch (selectedVersion)
            {
                case BenchmarkInputVersion.V1:
                    inputSystemV1.enabled = true;
                    break;

                case BenchmarkInputVersion.V2:
                    inputSystemV2.enabled = true;
                    break;

                default:
                    Debug.LogError(
                        $"Unsupported input version: {selectedVersion}",
                        this);
                    break;
            }
        }

        public void SelectVersion(
            BenchmarkInputVersion version)
        {
            if (selectedVersion == version &&
                IsSelectionApplied())
            {
                return;
            }

            selectedVersion = version;
            ApplySelectedVersion();
        }

        private bool IsSelectionApplied()
        {
            if (inputSystemV1 == null ||
                inputSystemV2 == null)
            {
                return false;
            }

            switch (selectedVersion)
            {
                case BenchmarkInputVersion.V1:
                    return
                        inputSystemV1.enabled &&
                        !inputSystemV2.enabled;

                case BenchmarkInputVersion.V2:
                    return
                        !inputSystemV1.enabled &&
                        inputSystemV2.enabled;

                default:
                    return false;
            }
        }

        private bool ValidateReferences()
        {
            if (inputSystemV1 == null)
            {
                Debug.LogError(
                    "Input System V1 InputProvider is not assigned.",
                    this);

                return false;
            }

            if (inputSystemV2 == null)
            {
                Debug.LogError(
                    "Input System V2 Runtime is not assigned.",
                    this);

                return false;
            }

            return true;
        }
    }
}