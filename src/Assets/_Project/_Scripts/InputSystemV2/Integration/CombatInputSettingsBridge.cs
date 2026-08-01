using System;
using Shield_Shot.InputSystem.Data;
using Shield_Shot.InputSystemV2.Combat.Domain;
using Shield_Shot.Settings;
using UnityEngine;

namespace Shield_Shot.InputSystemV2.Integration
{
    public sealed class CombatInputSettingsBridge
        : MonoBehaviour
    {
        [SerializeField]
        private InputSystemV2RuntimeBehaviour runtime;

        [SerializeField]
        private bool applyOnStart = true;

        private GameSettingsManager subscribedSettingsManager;
        private void Start()
        {
            TrySubscribe();

            if (applyOnStart)
            {
                ApplyCurrentSettings();
            }
        }
        private void OnEnable()
        {
            TrySubscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void TrySubscribe()
        {
            GameSettingsManager currentManager =
                GameSettingsManager.Instance;

            if (currentManager == null)
            {
                return;
            }

            if (subscribedSettingsManager ==
                currentManager)
            {
                return;
            }

            Unsubscribe();

            subscribedSettingsManager =
                currentManager;

            subscribedSettingsManager
                .InputSettingsChanged +=
                    HandleInputSettingsChanged;
        }

        private void HandleInputSettingsChanged(
    InputSettings settings)
        {
            Apply(settings);
        }

        private void Unsubscribe()
        {
            if (subscribedSettingsManager == null)
            {
                return;
            }

            subscribedSettingsManager
                .InputSettingsChanged -=
                    HandleInputSettingsChanged;

            subscribedSettingsManager = null;
        }

        [ContextMenu("Apply Current Input Settings")]
        public void ApplyCurrentSettings()
        {
            if (runtime == null)
            {
                Debug.LogError(
                    "InputSystemV2RuntimeBehaviour is not assigned.",
                    this);

                return;
            }

            GameSettingsManager settingsManager =
                GameSettingsManager.Instance;

            if (settingsManager == null)
            {
                Debug.LogError(
                    "GameSettingsManager instance is not available.",
                    this);

                return;
            }

            InputSettings settings =
                settingsManager.Input;

            if (settings == null)
            {
                Debug.LogError(
                    "InputSettings is not available.",
                    this);

                return;
            }

            CombatInputLayout layout =
                CreateLayout(settings);

            runtime.ApplyCombatLayout(
                in layout);
        }

        public void Apply(
            InputSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(
                    nameof(settings));
            }

            if (runtime == null)
            {
                throw new InvalidOperationException(
                    "InputSystemV2RuntimeBehaviour is not assigned.");
            }

            CombatInputLayout layout =
                CreateLayout(settings);

            runtime.ApplyCombatLayout(
                in layout);
        }

        private static CombatInputLayout CreateLayout(
            InputSettings settings)
        {
            CombatSplitDirection splitDirection =
                ConvertSplitDirection(
                    settings.splitMode);

            float splitRatio =
                SanitizeSplitRatio(
                    settings.splitRatio);

            return new CombatInputLayout(
                splitDirection,
                splitRatio,
                settings.isInverted);
        }

        private static CombatSplitDirection
            ConvertSplitDirection(
                SplitMode splitMode)
        {
            switch (splitMode)
            {
                case SplitMode.LeftRight:
                    return
                        CombatSplitDirection.LeftRight;

                case SplitMode.TopBottom:
                    return
                        CombatSplitDirection.BottomTop;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(splitMode),
                        splitMode,
                        "Unsupported input split mode.");
            }
        }

        private static float SanitizeSplitRatio(
            float splitRatio)
        {
            if (float.IsNaN(splitRatio) ||
                float.IsInfinity(splitRatio))
            {
                return 0.5f;
            }

            return Mathf.Clamp(
                splitRatio,
                0.01f,
                0.99f);
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Apply LeftRight 70")]
        private void DebugApplyLeftRight70()
        {
            if (GameSettingsManager.Instance == null)
            {
                Debug.LogError(
                    "GameSettingsManager instance is not available.",
                    this);

                return;
            }

            GameSettingsManager.Instance.SetInputLayout(
                SplitMode.LeftRight,
                0.7f,
                false);
        }

        [ContextMenu("Debug/Apply LeftRight 70 Inverted")]
        private void DebugApplyLeftRight70Inverted()
        {
            if (GameSettingsManager.Instance == null)
            {
                Debug.LogError(
                    "GameSettingsManager instance is not available.",
                    this);

                return;
            }

            GameSettingsManager.Instance.SetInputLayout(
                SplitMode.LeftRight,
                0.7f,
                true);
        }

        [ContextMenu("Debug/Apply TopBottom 60")]
        private void DebugApplyTopBottom60()
        {
            if (GameSettingsManager.Instance == null)
            {
                Debug.LogError(
                    "GameSettingsManager instance is not available.",
                    this);

                return;
            }

            GameSettingsManager.Instance.SetInputLayout(
                SplitMode.TopBottom,
                0.6f,
                false);
        }
#endif
    }
}