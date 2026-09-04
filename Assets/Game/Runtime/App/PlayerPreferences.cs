using System;
using DJMaximusKaiserSoje.Core;
using UnityEngine;

namespace DJMaximusKaiserSoje.App
{
    public sealed class PlayerPrefsPlayPreferences : IPlayPreferences
    {
        public const string ScrollSpeedKey = "rhythm.play.scroll-speed";
        public const string JudgementOffsetKey = "rhythm.play.judgement-offset-ms";
        public const string PlayStyleKey = "rhythm.play.style";

        public PlayerPrefsPlayPreferences()
        {
            scrollSpeed = ScrollSpeedRange.Clamp(PlayerPrefs.GetFloat(ScrollSpeedKey, ScrollSpeedRange.Default));
            judgementOffsetMs = JudgementOffsetRange.Clamp(PlayerPrefs.GetFloat(JudgementOffsetKey, 0.0f));
            playStyle = ReadStyle(PlayerPrefs.GetInt(PlayStyleKey, 0));
        }

        private float scrollSpeed;
        private double judgementOffsetMs;
        private PlayStyle playStyle;

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

        public event Action Changed;

        private void SaveAndNotify()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static PlayStyle ReadStyle(int value) => IsDefined((PlayStyle)value) ? (PlayStyle)value : DJMaximusKaiserSoje.Core.PlayStyle.FourKey;

        private static bool IsDefined(PlayStyle style) =>
            style >= DJMaximusKaiserSoje.Core.PlayStyle.FourKey && style <= DJMaximusKaiserSoje.Core.PlayStyle.SixKeyFx;
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
