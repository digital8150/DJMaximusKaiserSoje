using DJMaximusKaiserSoje.Core;
using UnityEngine;

namespace DJMaximusKaiserSoje.Presentation
{
    /// <summary>
    /// The one place colours are decided. Screens and the editor-side sprite generator both read it,
    /// so a palette change moves the whole game at once.
    /// </summary>
    public static class UiPalette
    {
        public static readonly Color Ink = new Color32(0x06, 0x08, 0x16, 0xFF);
        public static readonly Color Night = new Color32(0x0C, 0x11, 0x2B, 0xFF);
        public static readonly Color Panel = new Color32(0x13, 0x1B, 0x3D, 0xF2);
        public static readonly Color PanelSoft = new Color32(0x1B, 0x24, 0x4E, 0xE6);
        public static readonly Color PanelRaised = new Color32(0x25, 0x30, 0x63, 0xFF);
        public static readonly Color Divider = new Color32(0x3A, 0x47, 0x82, 0x66);

        public static readonly Color TextPrimary = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        public static readonly Color TextSecondary = new Color32(0xAE, 0xBB, 0xE2, 0xFF);
        public static readonly Color TextMuted = new Color32(0x6A, 0x78, 0xA6, 0xFF);

        public static readonly Color Cyan = new Color32(0x46, 0xDC, 0xFF, 0xFF);
        public static readonly Color Magenta = new Color32(0xFF, 0x59, 0xD8, 0xFF);
        public static readonly Color Mint = new Color32(0x7C, 0xFF, 0xC4, 0xFF);
        public static readonly Color Amber = new Color32(0xFF, 0xC8, 0x5C, 0xFF);
        public static readonly Color Rose = new Color32(0xFF, 0x5E, 0x7A, 0xFF);
        public static readonly Color Violet = new Color32(0x94, 0x84, 0xFF, 0xFF);

        public static Color GradeColor(JudgementGrade grade)
        {
            switch (grade)
            {
                case JudgementGrade.PerfectHigh: return Cyan;
                case JudgementGrade.Perfect: return Magenta;
                case JudgementGrade.Great: return Violet;
                case JudgementGrade.Good: return Mint;
                default: return Rose;
            }
        }

        public static Color TierColor(DifficultyTier tier)
        {
            switch (tier)
            {
                case DifficultyTier.Easy: return Mint;
                case DifficultyTier.Normal: return Cyan;
                case DifficultyTier.Hard: return Amber;
                case DifficultyTier.SuperHard: return Rose;
                default: return Violet;
            }
        }

        public static Color RankColor(Rank rank)
        {
            switch (rank)
            {
                case Rank.SSS:
                case Rank.SS:
                case Rank.S: return Amber;
                case Rank.A: return Cyan;
                case Rank.B: return Mint;
                case Rank.C: return Violet;
                case Rank.D: return TextSecondary;
                default: return Rose;
            }
        }

        public static Color WithAlpha(this Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }

    /// <summary>How judgement tiers, difficulty tiers, and ranks are worded on screen.</summary>
    public static class UiNaming
    {
        public static string GradeName(JudgementGrade grade)
        {
            switch (grade)
            {
                case JudgementGrade.PerfectHigh: return "PERFECT";
                case JudgementGrade.Perfect: return "PERFECT";
                case JudgementGrade.Great: return "GREAT";
                case JudgementGrade.Good: return "GOOD";
                default: return "MISS";
            }
        }

        /// <summary>The small line under the judgement, empty for grades that do not need one.</summary>
        public static string GradeSuffix(JudgementGrade grade) =>
            grade == JudgementGrade.PerfectHigh ? "HIGH" : string.Empty;

        public static string TallyLabel(JudgementGrade grade) =>
            grade == JudgementGrade.PerfectHigh ? "PERFECT HIGH" : GradeName(grade);

        public static string TierName(DifficultyTier tier)
        {
            switch (tier)
            {
                case DifficultyTier.Easy: return "EASY";
                case DifficultyTier.Normal: return "NORMAL";
                case DifficultyTier.Hard: return "HARD";
                case DifficultyTier.SuperHard: return "SUPER HARD";
                default: return "OVER";
            }
        }

        public static string TierShortName(DifficultyTier tier)
        {
            switch (tier)
            {
                case DifficultyTier.Easy: return "EZ";
                case DifficultyTier.Normal: return "NM";
                case DifficultyTier.Hard: return "HD";
                case DifficultyTier.SuperHard: return "SHD";
                default: return "OVR";
            }
        }

        public static string RankName(Rank rank) => rank.ToString();

        public static string StyleName(PlayStyle style)
        {
            switch (style)
            {
                case PlayStyle.FourKey: return "4 KEY";
                case PlayStyle.FourKeyFx: return "4 KEY + FX";
                case PlayStyle.SixKey: return "6 KEY";
                default: return "6 KEY + FX";
            }
        }

        public static string StyleShortName(PlayStyle style)
        {
            switch (style)
            {
                case PlayStyle.FourKey: return "4K";
                case PlayStyle.FourKeyFx: return "4K+";
                case PlayStyle.SixKey: return "6K";
                default: return "6K+";
            }
        }
    }
}
