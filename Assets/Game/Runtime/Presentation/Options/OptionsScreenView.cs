using System;
using System.Collections.Generic;
using DJMaximusKaiserSoje.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Presentation
{
    [DisallowMultipleComponent]
    public sealed class OptionsScreenView : MonoBehaviour, IOptionsScreenView
    {
        [Header("Audio")]
        [SerializeField] internal TMP_Text audioBufferLabel;
        [SerializeField] internal Button audioBufferDownButton;
        [SerializeField] internal Button audioBufferUpButton;
        [SerializeField] internal TMP_Text judgementOffsetLabel;
        [SerializeField] internal Button judgementOffsetDownButton;
        [SerializeField] internal Button judgementOffsetUpButton;

        [Header("Input")]
        [SerializeField] internal TabButtonView[] styleTabs;
        [SerializeField] internal Button[] keyButtons;
        [SerializeField] internal TMP_Text[] keyRoleLabels;
        [SerializeField] internal TMP_Text[] keyLabels;
        [SerializeField] internal Button resetKeysButton;

        [Header("Graphics")]
        [SerializeField] internal TMP_Text qualityLabel;
        [SerializeField] internal Button qualityDownButton;
        [SerializeField] internal Button qualityUpButton;
        [SerializeField] internal TMP_Text gearBackgroundOpacityLabel;
        [SerializeField] internal Button gearBackgroundOpacityDownButton;
        [SerializeField] internal Button gearBackgroundOpacityUpButton;
        [SerializeField] internal TMP_Text displayModeLabel;
        [SerializeField] internal Button displayModeButton;
        [SerializeField] internal TMP_Text resolutionLabel;
        [SerializeField] internal Button resolutionDownButton;
        [SerializeField] internal Button resolutionUpButton;
        [SerializeField] internal TMP_Text vSyncLabel;
        [SerializeField] internal Button vSyncButton;

        [Header("Navigation")]
        [SerializeField] internal TMP_Text descriptionLabel;
        [SerializeField] internal TMP_Text statusLabel;
        [SerializeField] internal Button backButton;

        private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
        private GameServices services;
        private PlayStyle editedStyle;
        private int listeningLane = -1;
        private bool leaving;

        private const float KeyAreaWidth = 1018f;
        private const float KeyGap = 18f;
        private const float KeyHeight = 48f;
        private const float KeyRowStep = 58f;

        public void Bind(GameServices gameServices)
        {
            services = gameServices ?? throw new ArgumentNullException(nameof(gameServices));
            editedStyle = services.Preferences.PlayStyle;
            leaving = false;
            BuildResolutionList();
            HookButtons();
            services.Preferences.Changed += Refresh;
            services.Music.PlayTheme(ScreenTheme.Title);
            if (descriptionLabel != null)
                descriptionLabel.text = "낮은 오디오 버퍼는 입력 반응을 빠르게 하지만 소리가 끊길 수 있어요.\n" +
                                        "계속 FAST면 판정 값을 올리고, SLOW면 내려 보세요.\n" +
                                        "키를 누르면 해당 위치에 새 키가 지정됩니다.";
            Refresh();
        }

        private void Update()
        {
            if (services == null || leaving) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (listeningLane >= 0)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    listeningLane = -1;
                    SetStatus(string.Empty);
                    RefreshKeys();
                    return;
                }

                for (int index = 0; index < keyboard.allKeys.Count; index++)
                {
                    var key = keyboard.allKeys[index];
                    if (!key.wasPressedThisFrame) continue;
                    services.Preferences.SetKeyBinding(editedStyle, listeningLane, key.name);
                    listeningLane = -1;
                    SetStatus("키 설정을 바꿨어요.");
                    return;
                }
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame) Close();
        }

        private void OnDestroy()
        {
            if (services != null) services.Preferences.Changed -= Refresh;
            UnhookButtons();
        }

        private void HookButtons()
        {
            audioBufferDownButton?.onClick.AddListener(() => StepAudioBuffer(-1));
            audioBufferUpButton?.onClick.AddListener(() => StepAudioBuffer(1));
            judgementOffsetDownButton?.onClick.AddListener(() => StepJudgementOffset(-1));
            judgementOffsetUpButton?.onClick.AddListener(() => StepJudgementOffset(1));
            qualityDownButton?.onClick.AddListener(() => StepQuality(-1));
            qualityUpButton?.onClick.AddListener(() => StepQuality(1));
            gearBackgroundOpacityDownButton?.onClick.AddListener(() => StepGearBackgroundOpacity(-1));
            gearBackgroundOpacityUpButton?.onClick.AddListener(() => StepGearBackgroundOpacity(1));
            displayModeButton?.onClick.AddListener(StepDisplayMode);
            resolutionDownButton?.onClick.AddListener(() => StepResolution(-1));
            resolutionUpButton?.onClick.AddListener(() => StepResolution(1));
            vSyncButton?.onClick.AddListener(ToggleVSync);
            resetKeysButton?.onClick.AddListener(ResetKeys);
            backButton?.onClick.AddListener(Close);

            if (styleTabs != null)
                for (int index = 0; index < styleTabs.Length; index++)
                    styleTabs[index]?.Bind(index, UiNaming.StyleShortName((PlayStyle)index), SelectStyle);

            if (keyButtons != null)
                for (int index = 0; index < keyButtons.Length; index++)
                {
                    int lane = index;
                    keyButtons[index]?.onClick.AddListener(() => ListenForKey(lane));
                }
        }

        private void UnhookButtons()
        {
            audioBufferDownButton?.onClick.RemoveAllListeners();
            audioBufferUpButton?.onClick.RemoveAllListeners();
            judgementOffsetDownButton?.onClick.RemoveAllListeners();
            judgementOffsetUpButton?.onClick.RemoveAllListeners();
            qualityDownButton?.onClick.RemoveAllListeners();
            qualityUpButton?.onClick.RemoveAllListeners();
            gearBackgroundOpacityDownButton?.onClick.RemoveAllListeners();
            gearBackgroundOpacityUpButton?.onClick.RemoveAllListeners();
            displayModeButton?.onClick.RemoveAllListeners();
            resolutionDownButton?.onClick.RemoveAllListeners();
            resolutionUpButton?.onClick.RemoveAllListeners();
            vSyncButton?.onClick.RemoveAllListeners();
            resetKeysButton?.onClick.RemoveAllListeners();
            backButton?.onClick.RemoveAllListeners();
            if (keyButtons != null)
                for (int index = 0; index < keyButtons.Length; index++) keyButtons[index]?.onClick.RemoveAllListeners();
        }

        private void Refresh()
        {
            if (services == null) return;
            IPlayPreferences preferences = services.Preferences;
            if (audioBufferLabel != null) audioBufferLabel.text = preferences.AudioBufferSize + " samples";
            if (judgementOffsetLabel != null) judgementOffsetLabel.text = UiFormat.Offset(preferences.JudgementOffsetMs);
            if (qualityLabel != null)
            {
                string[] names = QualitySettings.names;
                qualityLabel.text = names.Length == 0 ? "기본" : names[Mathf.Clamp(preferences.QualityLevel, 0, names.Length - 1)];
            }
            if (gearBackgroundOpacityLabel != null)
                gearBackgroundOpacityLabel.text = Mathf.RoundToInt(preferences.GearBackgroundOpacity * 100f) + "%";
            if (displayModeLabel != null) displayModeLabel.text = DisplayModeName(preferences.DisplayMode);
            if (resolutionLabel != null) resolutionLabel.text = preferences.ResolutionWidth + " × " + preferences.ResolutionHeight;
            if (vSyncLabel != null) vSyncLabel.text = preferences.VSync ? "켜짐" : "꺼짐";

            if (styleTabs != null)
                for (int index = 0; index < styleTabs.Length; index++) styleTabs[index]?.SetSelected((int)editedStyle == index);
            RefreshKeys();
        }

        private void RefreshKeys()
        {
            IReadOnlyList<string> bindings = services.Preferences.GetKeyBindings(editedStyle);
            LaneLayout layout = LaneLayout.Create(editedStyle);
            int normalIndex = 0;
            int fxIndex = 0;
            for (int index = 0; index < keyButtons.Length; index++)
            {
                bool visible = index < bindings.Count && index < layout.Lanes.Count;
                keyButtons[index].gameObject.SetActive(visible);
                if (!visible || index >= keyLabels.Length || keyLabels[index] == null) continue;

                LaneSpec lane = layout.Lanes[index];
                if (lane.IsFx)
                {
                    float fxRowY = layout.BaseLaneCount > 4 ? -KeyRowStep * 2f : -KeyRowStep;
                    PlaceKeyButton(keyButtons[index], fxIndex, 2, fxRowY);
                    SetKeyRole(index, lane.Role == LaneRole.LeftFx ? "왼쪽" : "오른쪽");
                    fxIndex++;
                }
                else
                {
                    int columns = layout.BaseLaneCount > 4 ? 3 : layout.BaseLaneCount;
                    int row = normalIndex / columns;
                    PlaceKeyButton(keyButtons[index], normalIndex % columns, columns, -row * KeyRowStep);
                    SetKeyRole(index, "라인 " + (normalIndex + 1));
                    normalIndex++;
                }

                keyLabels[index].text = index == listeningLane ? "키를 눌러 주세요" : UiNaming.KeyLabel(bindings[index]);
            }
        }

        private void SetKeyRole(int index, string value)
        {
            if (keyRoleLabels == null || index >= keyRoleLabels.Length || keyRoleLabels[index] == null) return;
            keyRoleLabels[index].text = value;
        }

        private static void PlaceKeyButton(Button button, int index, int count, float y)
        {
            if (button == null || count <= 0) return;
            float width = (KeyAreaWidth - KeyGap * (count - 1)) / count;
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(index * (width + KeyGap), y);
            rect.sizeDelta = new Vector2(width, KeyHeight);
        }

        private void SelectStyle(int index)
        {
            if (!Enum.IsDefined(typeof(PlayStyle), index)) return;
            editedStyle = (PlayStyle)index;
            listeningLane = -1;
            SetStatus(string.Empty);
            Refresh();
        }

        private void ListenForKey(int lane)
        {
            if (lane >= services.Preferences.GetKeyBindings(editedStyle).Count) return;
            listeningLane = lane;
            SetStatus("바꿀 키를 눌러 주세요.  Esc 취소");
            RefreshKeys();
        }

        private void ResetKeys()
        {
            listeningLane = -1;
            services.Preferences.ResetKeyBindings(editedStyle);
            SetStatus("기본 키로 되돌렸어요.");
        }

        private void StepAudioBuffer(int direction)
        {
            int current = Array.IndexOf(GameOptionRules.AudioBufferSizes, services.Preferences.AudioBufferSize);
            int next = Mathf.Clamp(current + direction, 0, GameOptionRules.AudioBufferSizes.Length - 1);
            services.Preferences.AudioBufferSize = GameOptionRules.AudioBufferSizes[next];
        }

        private void StepJudgementOffset(int direction) =>
            services.Preferences.JudgementOffsetMs =
                JudgementOffsetRange.Stepped(services.Preferences.JudgementOffsetMs, direction);

        private void StepQuality(int direction) => services.Preferences.QualityLevel += direction;

        private void StepGearBackgroundOpacity(int direction) =>
            services.Preferences.GearBackgroundOpacity = GameOptionRules.StepGearBackgroundOpacity(
                services.Preferences.GearBackgroundOpacity, direction);

        private void StepDisplayMode()
        {
            int count = Enum.GetValues(typeof(DisplayMode)).Length;
            services.Preferences.DisplayMode = (DisplayMode)(((int)services.Preferences.DisplayMode + 1) % count);
        }

        private void ToggleVSync() => services.Preferences.VSync = !services.Preferences.VSync;

        private void StepResolution(int direction)
        {
            if (resolutions.Count == 0) return;
            int current = resolutions.FindIndex(value => value.x == services.Preferences.ResolutionWidth &&
                                                         value.y == services.Preferences.ResolutionHeight);
            if (current < 0) current = 0;
            int next = (current + direction + resolutions.Count) % resolutions.Count;
            services.Preferences.SetResolution(resolutions[next].x, resolutions[next].y);
        }

        private void BuildResolutionList()
        {
            resolutions.Clear();
            foreach (Resolution resolution in Screen.resolutions)
            {
                var size = new Vector2Int(resolution.width, resolution.height);
                if (!resolutions.Contains(size)) resolutions.Add(size);
            }
            if (resolutions.Count == 0) resolutions.Add(new Vector2Int(Screen.width, Screen.height));
        }

        private void Close()
        {
            if (leaving) return;
            leaving = true;
            services.Flow.CloseOptions();
        }

        private void SetStatus(string value)
        {
            if (statusLabel != null) statusLabel.text = value;
        }

        private static string DisplayModeName(DisplayMode mode)
        {
            switch (mode)
            {
                case DisplayMode.Fullscreen: return "전체 화면";
                case DisplayMode.Windowed: return "창 모드";
                default: return "테두리 없는 창";
            }
        }
    }
}
