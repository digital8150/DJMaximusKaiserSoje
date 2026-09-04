using DJMaximusKaiserSoje.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Presentation
{
    /// <summary>Jacket, title, and difficulty of the chart in play. Shared by gameplay and results.</summary>
    [DisallowMultipleComponent]
    public sealed class SongCardView : MonoBehaviour
    {
        [SerializeField] internal AddressableImage jacket;
        [SerializeField] internal TMP_Text titleLabel;
        [SerializeField] internal TMP_Text artistLabel;
        [SerializeField] internal TMP_Text bpmLabel;
        [SerializeField] internal TMP_Text categoryLabel;
        [SerializeField] internal TMP_Text tierLabel;
        [SerializeField] internal TMP_Text levelLabel;
        [SerializeField] internal Image tierPlate;
        [SerializeField] internal TMP_Text styleLabel;

        public void Bind(SongSummary song, ChartSummary chart, PlayStyle style)
        {
            if (song != null)
            {
                if (jacket != null) jacket.Show(song.JacketAddress);
                if (titleLabel != null) titleLabel.text = song.Title;
                if (artistLabel != null) artistLabel.text = song.Artist;
                if (bpmLabel != null) bpmLabel.text = UiFormat.Bpm(song.Bpm);
                if (categoryLabel != null) categoryLabel.text = song.Category;
            }

            if (styleLabel != null) styleLabel.text = UiNaming.StyleName(style);
            if (chart == null) return;

            var tierColor = UiPalette.TierColor(chart.Tier);
            if (tierLabel != null)
            {
                tierLabel.text = UiNaming.TierName(chart.Tier);
                tierLabel.color = tierColor;
            }

            if (levelLabel != null) levelLabel.text = chart.Level.ToString();
            if (tierPlate != null) tierPlate.color = tierColor.WithAlpha(0.28f);
        }

        /// <summary>Fallback for when only the chart's own header is available.</summary>
        public void Bind(BeatmapHeader header, PlayStyle style)
        {
            if (header == null) return;
            if (titleLabel != null) titleLabel.text = header.Title;
            if (artistLabel != null) artistLabel.text = header.Artist;
            if (bpmLabel != null) bpmLabel.text = UiFormat.Bpm(header.Bpm);
            if (tierLabel != null) tierLabel.text = header.DifficultyName;
            if (styleLabel != null) styleLabel.text = UiNaming.StyleName(style);
        }
    }
}
