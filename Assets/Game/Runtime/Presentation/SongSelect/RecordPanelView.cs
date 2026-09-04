using DJMaximusKaiserSoje.Core;
using TMPro;
using UnityEngine;

namespace DJMaximusKaiserSoje.Presentation
{
    /// <summary>The player's best on the highlighted chart. Dashes until they have finished it once.</summary>
    [DisallowMultipleComponent]
    public sealed class RecordPanelView : MonoBehaviour
    {
        private const string Blank = "—";

        [SerializeField] internal TMP_Text scoreLabel;
        [SerializeField] internal TMP_Text accuracyLabel;
        [SerializeField] internal TMP_Text comboLabel;
        [SerializeField] internal TMP_Text rankLabel;
        [SerializeField] internal TMP_Text playCountLabel;

        public void Bind(ChartRecord record)
        {
            bool played = record != null && !record.IsEmpty;

            if (scoreLabel != null) scoreLabel.text = played ? UiFormat.Score(record.BestScore) : Blank;
            if (accuracyLabel != null) accuracyLabel.text = played ? UiFormat.AccuracyShort(record.BestAccuracy01) : Blank;
            if (comboLabel != null) comboLabel.text = played ? UiFormat.Combo(record.BestCombo) : Blank;
            if (playCountLabel != null) playCountLabel.text = played ? record.PlayCount.ToString() : Blank;

            if (rankLabel == null) return;
            rankLabel.text = played ? UiNaming.RankName(record.BestRank) : Blank;
            rankLabel.color = played ? UiPalette.RankColor(record.BestRank) : UiPalette.TextMuted;
        }
    }
}
