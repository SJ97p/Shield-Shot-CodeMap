using Shield_Shot.InputSystem.Data;
using Shield_Shot.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shield_Shot.UI
{
    public sealed class InputSettingsPanelUI : MonoBehaviour
    {
        private const float DefaultShieldRatio = 0.5f;

        [Header("Direction")]
        [SerializeField] private Button leftRightButton;
        [SerializeField] private Button topBottomButton;

        [Header("Shield Position")]
        [SerializeField] private Button firstRegionButton;
        [SerializeField] private Button secondRegionButton;
        [SerializeField] private TMP_Text firstRegionButtonText;
        [SerializeField] private TMP_Text secondRegionButtonText;

        [Header("Shield Ratio")]
        [SerializeField] private Slider ratioSlider;
        [SerializeField] private TMP_Text ratioValueText;

        [Header("Preview")]
        [SerializeField] private RectTransform firstRegion;
        [SerializeField] private RectTransform secondRegion;
        [SerializeField] private RectTransform boundaryLine;
        [SerializeField] private TMP_Text firstRegionText;
        [SerializeField] private TMP_Text secondRegionText;

        [Header("Actions")]
        [SerializeField] private Button resetButton;
        [SerializeField] private Button applyButton;

        private SplitMode draftSplitMode;
        private float draftShieldRatio = DefaultShieldRatio;
        private bool draftIsInverted;
        private bool listenersRegistered;

        private void Awake()
        {
            RegisterListeners();
        }

        private void OnEnable()
        {
            LoadSavedSettings();
        }

        private void OnDestroy()
        {
            UnregisterListeners();
        }

        public void Configure(
            Button leftRight,
            Button topBottom,
            Button firstPosition,
            Button secondPosition,
            Slider shieldRatioSlider,
            TMP_Text ratioText,
            RectTransform previewFirst,
            RectTransform previewSecond,
            RectTransform previewBoundary,
            TMP_Text previewFirstText,
            TMP_Text previewSecondText,
            Button reset,
            Button apply)
        {
            leftRightButton = leftRight;
            topBottomButton = topBottom;
            firstRegionButton = firstPosition;
            secondRegionButton = secondPosition;
            firstRegionButtonText = firstPosition != null
                ? firstPosition.GetComponentInChildren<TMP_Text>(true)
                : null;
            secondRegionButtonText = secondPosition != null
                ? secondPosition.GetComponentInChildren<TMP_Text>(true)
                : null;
            ratioSlider = shieldRatioSlider;
            ratioValueText = ratioText;
            firstRegion = previewFirst;
            secondRegion = previewSecond;
            boundaryLine = previewBoundary;
            firstRegionText = previewFirstText;
            secondRegionText = previewSecondText;
            resetButton = reset;
            applyButton = apply;
        }

        private void RegisterListeners()
        {
            if (listenersRegistered)
            {
                return;
            }

            leftRightButton?.onClick.AddListener(SelectLeftRight);
            topBottomButton?.onClick.AddListener(SelectTopBottom);
            firstRegionButton?.onClick.AddListener(SelectFirstRegion);
            secondRegionButton?.onClick.AddListener(SelectSecondRegion);
            ratioSlider?.onValueChanged.AddListener(SetShieldRatio);
            resetButton?.onClick.AddListener(ResetDraft);
            applyButton?.onClick.AddListener(ApplyDraft);
            listenersRegistered = true;
        }

        private void UnregisterListeners()
        {
            if (!listenersRegistered)
            {
                return;
            }

            leftRightButton?.onClick.RemoveListener(SelectLeftRight);
            topBottomButton?.onClick.RemoveListener(SelectTopBottom);
            firstRegionButton?.onClick.RemoveListener(SelectFirstRegion);
            secondRegionButton?.onClick.RemoveListener(SelectSecondRegion);
            ratioSlider?.onValueChanged.RemoveListener(SetShieldRatio);
            resetButton?.onClick.RemoveListener(ResetDraft);
            applyButton?.onClick.RemoveListener(ApplyDraft);
            listenersRegistered = false;
        }

        private void LoadSavedSettings()
        {
            if (GameSettingsManager.Instance == null)
            {
                return;
            }

            InputSettings settings = GameSettingsManager.Instance.Input;
            draftSplitMode = settings.splitMode;
            draftIsInverted = settings.isInverted;
            draftShieldRatio = draftIsInverted
                ? 1f - settings.splitRatio
                : settings.splitRatio;
            draftShieldRatio = Mathf.Clamp(draftShieldRatio, 0.3f, 0.7f);
            RefreshView();
        }

        private void SelectLeftRight()
        {
            draftSplitMode = SplitMode.LeftRight;
            RefreshView();
        }

        private void SelectTopBottom()
        {
            draftSplitMode = SplitMode.TopBottom;
            RefreshView();
        }

        private void SelectFirstRegion()
        {
            draftIsInverted = false;
            RefreshView();
        }

        private void SelectSecondRegion()
        {
            draftIsInverted = true;
            RefreshView();
        }

        private void SetShieldRatio(float value)
        {
            draftShieldRatio = Mathf.Clamp(value, 0.3f, 0.7f);
            RefreshView(updateSlider: false);
        }

        private void ResetDraft()
        {
            draftSplitMode = SplitMode.LeftRight;
            draftShieldRatio = DefaultShieldRatio;
            draftIsInverted = false;
            RefreshView();
        }

        private void ApplyDraft()
        {
            if (GameSettingsManager.Instance == null)
            {
                Debug.LogWarning("[Input Settings UI] GameSettingsManager를 찾지 못해 적용하지 못했습니다.");
                return;
            }

            float firstRegionRatio = draftIsInverted
                ? 1f - draftShieldRatio
                : draftShieldRatio;

            GameSettingsManager.Instance.SetInputLayout(
                draftSplitMode,
                firstRegionRatio,
                draftIsInverted);
            GameSettingsManager.Instance.SaveInputSettings();
        }

        private void RefreshView(bool updateSlider = true)
        {
            if (updateSlider && ratioSlider != null)
            {
                ratioSlider.SetValueWithoutNotify(draftShieldRatio);
            }

            if (ratioValueText != null)
            {
                ratioValueText.text = $"{Mathf.RoundToInt(draftShieldRatio * 100f)}%";
            }

            bool isLeftRight = draftSplitMode == SplitMode.LeftRight;
            if (firstRegionButtonText != null)
            {
                firstRegionButtonText.text = isLeftRight ? "왼쪽" : "아래";
            }

            if (secondRegionButtonText != null)
            {
                secondRegionButtonText.text = isLeftRight ? "오른쪽" : "위";
            }

            SetButtonSelected(leftRightButton, isLeftRight);
            SetButtonSelected(topBottomButton, !isLeftRight);
            SetButtonSelected(firstRegionButton, !draftIsInverted);
            SetButtonSelected(secondRegionButton, draftIsInverted);
            RefreshPreview(isLeftRight);
        }

        private void RefreshPreview(bool isLeftRight)
        {
            if (firstRegion == null || secondRegion == null || boundaryLine == null)
            {
                return;
            }

            float firstRatio = draftIsInverted
                ? 1f - draftShieldRatio
                : draftShieldRatio;

            if (isLeftRight)
            {
                SetRegion(firstRegion, 0.02f, 0.03f, firstRatio, 0.97f);
                SetRegion(secondRegion, firstRatio, 0.03f, 0.98f, 0.97f);
                SetRegion(
                    boundaryLine,
                    firstRatio - 0.005f,
                    0.03f,
                    firstRatio + 0.005f,
                    0.97f);
            }
            else
            {
                SetRegion(firstRegion, 0.02f, 0.03f, 0.98f, firstRatio);
                SetRegion(secondRegion, 0.02f, firstRatio, 0.98f, 0.97f);
                SetRegion(
                    boundaryLine,
                    0.02f,
                    firstRatio - 0.008f,
                    0.98f,
                    firstRatio + 0.008f);
            }

            if (firstRegionText != null)
            {
                firstRegionText.text = draftIsInverted ? "공격" : "방패";
            }

            if (secondRegionText != null)
            {
                secondRegionText.text = draftIsInverted ? "방패" : "공격";
            }
        }

        private static void SetRegion(
            RectTransform rect,
            float minX,
            float minY,
            float maxX,
            float maxY)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static void SetButtonSelected(Button button, bool selected)
        {
            if (button == null || button.targetGraphic == null)
            {
                return;
            }

            button.targetGraphic.color = selected
                ? new Color(0.72f, 1f, 0.64f, 1f)
                : Color.white;
        }
    }
}
