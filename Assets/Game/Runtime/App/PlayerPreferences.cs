using System;
using System.Collections.Generic;
using DJMaximusKaiserSoje.Core;
using UnityEngine;

namespace DJMaximusKaiserSoje.App
{
    public sealed class PlayerPrefsPlayPreferences : IPlayPreferences
    {
        public const string ScrollSpeedKey = "rhythm.play.scroll-speed";
        public const string JudgementOffsetKey = "rhythm.play.judgement-offset-ms";
        public const string PlayStyleKey = "rhythm.play.style";
        public const string SelectedCrewKey = "rhythm.play.selected-crew";
        public const string AudioBufferSizeKey = "rhythm.audio.buffer-size";
        public const string QualityLevelKey = "rhythm.graphics.quality";
        public const string GearBackgroundOpacityKey = "rhythm.graphics.gear-background-opacity";
        public const string VSyncKey = "rhythm.graphics.vsync";
        public const string DisplayModeKey = "rhythm.graphics.display-mode";
        public const string ResolutionWidthKey = "rhythm.graphics.width";
        public const string ResolutionHeightKey = "rhythm.graphics.height";
        private const string BindingKeyPrefix = "rhythm.input.bindings.";

        /// <summary>
        /// Read before the mixer exists, so the device can be opened at the size the player chose
        /// rather than being opened at a default and immediately rebuilt.
        /// </summary>
        public static int StoredAudioBufferSize => GameOptionRules.NormalizeAudioBufferSize(
            PlayerPrefs.GetInt(AudioBufferSizeKey, GameOptionRules.DefaultAudioBufferSize));

        public PlayerPrefsPlayPreferences(IAudioDevice audioDevice)
        {
            this.audioDevice = audioDevice ?? throw new ArgumentNullException(nameof(audioDevice));
            scrollSpeed = ScrollSpeedRange.Clamp(PlayerPrefs.GetFloat(ScrollSpeedKey, ScrollSpeedRange.Default));
            judgementOffsetMs = JudgementOffsetRange.Clamp(PlayerPrefs.GetFloat(JudgementOffsetKey, 0.0f));
            playStyle = ReadStyle(PlayerPrefs.GetInt(PlayStyleKey, 0));
            selectedCrewId = BattleCrewId.Normalize(PlayerPrefs.GetString(SelectedCrewKey, BattleCrewId.Default));
            audioBufferSize = StoredAudioBufferSize;
            qualityLevel = Mathf.Clamp(PlayerPrefs.GetInt(QualityLevelKey, QualitySettings.GetQualityLevel()),
                0, Mathf.Max(0, QualitySettings.names.Length - 1));
            gearBackgroundOpacity = GameOptionRules.NormalizeGearBackgroundOpacity(
                PlayerPrefs.GetFloat(GearBackgroundOpacityKey, GameOptionRules.DefaultGearBackgroundOpacity));
            vSync = PlayerPrefs.GetInt(VSyncKey, QualitySettings.vSyncCount > 0 ? 1 : 0) != 0;
            displayMode = ReadDisplayMode(PlayerPrefs.GetInt(DisplayModeKey, (int)CurrentDisplayMode()));
            resolutionWidth = Mathf.Max(640, PlayerPrefs.GetInt(ResolutionWidthKey, Screen.width));
            resolutionHeight = Mathf.Max(360, PlayerPrefs.GetInt(ResolutionHeightKey, Screen.height));
            ApplyAll();
        }

        private readonly IAudioDevice audioDevice;
        private float scrollSpeed;
        private double judgementOffsetMs;
        private PlayStyle playStyle;
        private string selectedCrewId;
        private int audioBufferSize;
        private int qualityLevel;
        private float gearBackgroundOpacity;
        private bool vSync;
        private DisplayMode displayMode;
        private int resolutionWidth;
        private int resolutionHeight;

        public float ScrollSpeed
        {
            get => scrollSpeed;
            set
            {
                float clamped = ScrollSpeedRange.Clamp(value);
                if (Math.Abs(scrollSpeed - clamped) < 0.0001f) return;
                scrollSpeed = clamped;
                PlayerPrefs.SetFloat(ScrollSpeedKey, scrollSpeed);
                SaveAndNotify();
            }
        }

        public double JudgementOffsetMs
        {
            get => judgementOffsetMs;
            set
            {
                double clamped = JudgementOffsetRange.Clamp(value);
                if (Math.Abs(judgementOffsetMs - clamped) < 0.0001) return;
                judgementOffsetMs = clamped;
                PlayerPrefs.SetFloat(JudgementOffsetKey, (float)judgementOffsetMs);
                SaveAndNotify();
            }
        }

        public PlayStyle PlayStyle
        {
            get => playStyle;
            set
            {
                if (!IsDefined(value)) value = DJMaximusKaiserSoje.Core.PlayStyle.FourKey;
                if (playStyle == value) return;
                playStyle = value;
                PlayerPrefs.SetInt(PlayStyleKey, (int)playStyle);
                SaveAndNotify();
            }
        }

        public string SelectedCrewId
        {
            get => selectedCrewId;
            set
            {
                string normalized = BattleCrewId.Normalize(value);
                if (string.Equals(selectedCrewId, normalized, StringComparison.Ordinal)) return;
                selectedCrewId = normalized;
                PlayerPrefs.SetString(SelectedCrewKey, selectedCrewId);
                SaveAndNotify();
            }
        }

        public int AudioBufferSize
        {
            get => audioBufferSize;
            set
            {
                int normalized = GameOptionRules.NormalizeAudioBufferSize(value);
                if (audioBufferSize == normalized) return;
                audioBufferSize = normalized;
                PlayerPrefs.SetInt(AudioBufferSizeKey, normalized);
                ApplyAudioBuffer();
                SaveAndNotify();
            }
        }

        public int QualityLevel
        {
            get => qualityLevel;
            set
            {
                int clamped = Mathf.Clamp(value, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
                if (qualityLevel == clamped) return;
                qualityLevel = clamped;
                PlayerPrefs.SetInt(QualityLevelKey, qualityLevel);
                QualitySettings.SetQualityLevel(qualityLevel, true);
                SaveAndNotify();
            }
        }

        public float GearBackgroundOpacity
        {
            get => gearBackgroundOpacity;
            set
            {
                float normalized = GameOptionRules.NormalizeGearBackgroundOpacity(value);
                if (Math.Abs(gearBackgroundOpacity - normalized) < 0.0001f) return;
                gearBackgroundOpacity = normalized;
                PlayerPrefs.SetFloat(GearBackgroundOpacityKey, normalized);
                SaveAndNotify();
            }
        }

        public bool VSync
        {
            get => vSync;
            set
            {
                if (vSync == value) return;
                vSync = value;
                PlayerPrefs.SetInt(VSyncKey, value ? 1 : 0);
                QualitySettings.vSyncCount = value ? 1 : 0;
                SaveAndNotify();
            }
        }

        public DisplayMode DisplayMode
        {
            get => displayMode;
            set
            {
                if (!Enum.IsDefined(typeof(DisplayMode), value)) value = DisplayMode.Borderless;
                if (displayMode == value) return;
                displayMode = value;
                PlayerPrefs.SetInt(DisplayModeKey, (int)value);
                ApplyResolution();
                SaveAndNotify();
            }
        }

        public int ResolutionWidth => resolutionWidth;
        public int ResolutionHeight => resolutionHeight;

        public void SetResolution(int width, int height)
        {
            width = Mathf.Max(640, width);
            height = Mathf.Max(360, height);
            if (resolutionWidth == width && resolutionHeight == height) return;
            resolutionWidth = width;
            resolutionHeight = height;
            PlayerPrefs.SetInt(ResolutionWidthKey, width);
            PlayerPrefs.SetInt(ResolutionHeightKey, height);
            ApplyResolution();
            SaveAndNotify();
        }

        public IReadOnlyList<string> GetKeyBindings(PlayStyle style)
        {
            string serialized = PlayerPrefs.GetString(BindingKey(style), string.Empty);
            string[] values = string.IsNullOrWhiteSpace(serialized) ? null : serialized.Split('|');
            return GameOptionRules.NormalizeBindings(style, values);
        }

        public void SetKeyBinding(PlayStyle style, int lane, string keyName)
        {
            string[] values = GameOptionRules.Rebind(style, GetKeyBindings(style), lane, keyName);
            PlayerPrefs.SetString(BindingKey(style), string.Join("|", values));
            SaveAndNotify();
        }

        public void ResetKeyBindings(PlayStyle style)
        {
            PlayerPrefs.DeleteKey(BindingKey(style));
            SaveAndNotify();
        }

        public event Action Changed;

        private void SaveAndNotify()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static PlayStyle ReadStyle(int value) => IsDefined((PlayStyle)value) ? (PlayStyle)value : DJMaximusKaiserSoje.Core.PlayStyle.FourKey;

        private static bool IsDefined(PlayStyle style) =>
            style >= DJMaximusKaiserSoje.Core.PlayStyle.FourKey && style <= DJMaximusKaiserSoje.Core.PlayStyle.SixKeyFx;

        private void ApplyAll()
        {
            QualitySettings.SetQualityLevel(qualityLevel, true);
            QualitySettings.vSyncCount = vSync ? 1 : 0;
            ApplyAudioBuffer();
            ApplyResolution();
        }

        /// <summary>
        /// Gameplay is mixed by FMOD, so this is the buffer that decides how quickly a keypress is
        /// heard. Unity's own buffer is left alone: it only carries the screen themes, which have
        /// nothing to stay in time with.
        /// </summary>
        private void ApplyAudioBuffer() => audioDevice.SetBufferLength(audioBufferSize);

        private void ApplyResolution() => Screen.SetResolution(resolutionWidth, resolutionHeight, ToUnityMode(displayMode));

        private static string BindingKey(PlayStyle style) => BindingKeyPrefix + style;

        private static DisplayMode ReadDisplayMode(int value) => Enum.IsDefined(typeof(DisplayMode), value)
            ? (DisplayMode)value
            : DisplayMode.Borderless;

        private static DisplayMode CurrentDisplayMode()
        {
            switch (Screen.fullScreenMode)
            {
                case FullScreenMode.ExclusiveFullScreen: return DisplayMode.Fullscreen;
                case FullScreenMode.Windowed: return DisplayMode.Windowed;
                default: return DisplayMode.Borderless;
            }
        }

        private static FullScreenMode ToUnityMode(DisplayMode mode)
        {
            switch (mode)
            {
                case DisplayMode.Fullscreen: return FullScreenMode.ExclusiveFullScreen;
                case DisplayMode.Windowed: return FullScreenMode.Windowed;
                default: return FullScreenMode.FullScreenWindow;
            }
        }
    }

    public sealed class LocalPlayerProfile : IPlayerProfile
    {
        public LocalPlayerProfile(string displayName = "PLAYER", string tag = "RHYTHM")
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "PLAYER" : displayName;
            Tag = string.IsNullOrWhiteSpace(tag) ? "RHYTHM" : tag;
            Level = 1;
            ExpProgress01 = 0.0;
        }

        public string DisplayName { get; }
        public string Tag { get; }
        public int Level { get; private set; }
        public double ExpProgress01 { get; private set; }

        public void SetProgress(int level, double expProgress01)
        {
            Level = Math.Max(1, level);
            ExpProgress01 = Math.Max(0.0, Math.Min(1.0, expProgress01));
        }
    }
}
