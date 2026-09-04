using DJMaximusKaiserSoje.Core;
using TMPro;
using UnityEngine;

namespace DJMaximusKaiserSoje.Presentation
{
    /// <summary>The running count of every judgement tier, with the accuracy it adds up to.</summary>
    [DisallowMultipleComponent]
    public sealed class TallyPanelView : MonoBehaviour
    {
        [SerializeField] internal TMP_Text[] countLabels;
        [SerializeField] internal TMP_Text[] captionLabels;
        [SerializeField] internal TMP_Text accuracyLabel;
        [SerializeField] internal TMP_Text ratingLabel;

        private void Awake() => ApplyCaptions();

        public void Bind(RunScore score)
        {
            if (countLabels != null)
            {
                for (int index = 0; index < countLabels.Length; index++)
                {
                    if (countLabels[index] == null) continue;
                    var grade = (JudgementGrade)index;
                    countLabels[index].text = UiFormat.PaddedCount(score.Tally.CountOf(grade));
                }
            }

            // Nothing judged yet is a clean sheet, not a zero score.
            if (accuracyLabel != null)
                accuracyLabel.text = score.JudgedCount == 0
                    ? UiFormat.AccuracyPrecise(1.0)
                    : UiFormat.AccuracyPrecise(score.Accuracy01);

            if (ratingLabel != null) ratingLabel.text = UiFormat.Rating(score.Rating);
        }

        private void ApplyCaptions()
        {
            if (captionLabels == null) return;
            for (int index = 0; index < captionLabels.Length; index++)
            {
                if (captionLabels[index] == null) continue;
                var grade = (JudgementGrade)index;
                captionLabels[index].text = UiNaming.TallyLabel(grade);
                captionLabels[index].color = UiPalette.GradeColor(grade);
            }
        }
    }
}
