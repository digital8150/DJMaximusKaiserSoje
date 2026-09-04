using DJMaximusKaiserSoje.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Presentation
{
    /// <summary>The rank medallion and the one line that sums the run up.</summary>
    [DisallowMultipleComponent]
    public sealed class RankBadgeView : MonoBehaviour
    {
        [SerializeField] internal Image medallion;
        [SerializeField] internal Image glow;
        [SerializeField] internal TMP_Text rankLabel;
        [SerializeField] internal TMP_Text captionLabel;
        [SerializeField] internal Image captionPlate;
        [SerializeField] internal ScalePunch punch;

        public void Bind(Rank rank, RunScore score, PlayOutcome outcome)
        {
            var color = UiPalette.RankColor(rank);

            if (rankLabel != null)
            {
                rankLabel.text = UiNaming.RankName(rank);
                rankLabel.color = color;
            }

            if (glow != null) glow.color = color.WithAlpha(0.35f);
            if (medallion != null) medallion.color = Color.white;

            string caption;
            Color captionColor;
            if (outcome == PlayOutcome.Failed)
            {
                caption = "FAILED";
                captionColor = UiPalette.Rose;
            }
            else if (score.IsAllPerfect)
            {
                caption = "ALL PERFECT";
                captionColor = UiPalette.Amber;
            }
            else if (score.IsFullCombo)
            {
                caption = "FULL COMBO";
                captionColor = UiPalette.Cyan;
            }
            else
            {
                caption = "CLEAR";
                captionColor = UiPalette.Mint;
            }

            if (captionLabel != null)
            {
                captionLabel.text = caption;
                captionLabel.color = captionColor;
            }

            if (captionPlate != null) captionPlate.color = captionColor.WithAlpha(0.2f);
            if (punch != null) punch.Punch();
        }
    }
}
