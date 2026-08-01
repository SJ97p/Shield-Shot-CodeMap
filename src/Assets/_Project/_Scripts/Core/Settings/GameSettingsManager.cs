using Shield_Shot.Settings.Data;
using Shield_Shot.InputSystem.Data;
using UnityEngine;
using System;

namespace Shield_Shot.Settings
{
    public class GameSettingsManager : PersistentSingleton<GameSettingsManager>
    {
        [SerializeField] private InputSettings inputSettings = new InputSettings();
        [SerializeField] private SoundSettings soundSettings = new SoundSettings();
        [SerializeField] private ScreenSettings screenSettings = new ScreenSettings();

        private const string InputSplitModeKey = "Input_SplitMode";

        private const string InputSplitRatioKey = "Input_SplitRatio";

        private const string InputIsInvertedKey = "Input_IsInverted";

        // 외부에서 각 시스템이 자기 세팅만 쏙 빼갈 수 있도록 프로퍼티 제공
        public InputSettings Input => inputSettings;
        public SoundSettings Sound => soundSettings;
        public ScreenSettings Screen => screenSettings;

        public event Action<InputSettings> InputSettingsChanged;

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this)
            {
                return;
            }

            LoadSoundSettings();
            LoadInputSettings();
        }

        private void LoadSoundSettings()
        {
            soundSettings.bgmVolume = PlayerPrefs.GetFloat("BGM_Volume", soundSettings.bgmVolume);
            soundSettings.sfxVolume = PlayerPrefs.GetFloat("SFX_Volume", soundSettings.sfxVolume);
            soundSettings.uiVolume = PlayerPrefs.GetFloat("UI_Volume", soundSettings.uiVolume);
        }

        public void SetInputLayout(
    SplitMode splitMode,
    float splitRatio,
    bool isInverted)
        {
            float safeSplitRatio =
                SanitizeSplitRatio(splitRatio);

            bool hasChanged =
                inputSettings.splitMode != splitMode ||
                !Mathf.Approximately(
                    inputSettings.splitRatio,
                    safeSplitRatio) ||
                inputSettings.isInverted != isInverted;

            if (!hasChanged)
            {
                return;
            }

            inputSettings.splitMode =
                splitMode;

            inputSettings.splitRatio =
                safeSplitRatio;

            inputSettings.isInverted =
                isInverted;

            PlayerPrefs.SetInt(
                InputSplitModeKey,
                (int)inputSettings.splitMode);

            PlayerPrefs.SetFloat(
                InputSplitRatioKey,
                inputSettings.splitRatio);

            PlayerPrefs.SetInt(
                InputIsInvertedKey,
                inputSettings.isInverted ? 1 : 0);


            InputSettingsChanged?.Invoke(
                inputSettings);
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

        private void LoadInputSettings()
        {
            int savedSplitMode =
                PlayerPrefs.GetInt(
                    InputSplitModeKey,
                    (int)inputSettings.splitMode);

            inputSettings.splitMode =
                ConvertSplitModeOrDefault(
                    savedSplitMode,
                    inputSettings.splitMode);

            float savedSplitRatio =
                PlayerPrefs.GetFloat(
                    InputSplitRatioKey,
                    inputSettings.splitRatio);

            inputSettings.splitRatio =
                SanitizeSplitRatio(
                    savedSplitRatio);

            inputSettings.isInverted =
                PlayerPrefs.GetInt(
                    InputIsInvertedKey,
                    inputSettings.isInverted ? 1 : 0) != 0;
        }

        private static SplitMode ConvertSplitModeOrDefault(
            int value,
            SplitMode fallback)
        {
            switch ((SplitMode)value)
            {
                case SplitMode.LeftRight:
                    return SplitMode.LeftRight;

                case SplitMode.TopBottom:
                    return SplitMode.TopBottom;

                default:
                    return fallback;
            }
        }
        public void SaveInputSettings()
        {
            PlayerPrefs.Save();
        }

        private void OnApplicationPause(
    bool isPaused)
        {
            if (isPaused)
            {
                PlayerPrefs.Save();
            }
        }

        private void OnApplicationQuit()
        {
            PlayerPrefs.Save();
        }
    }
}