using DJMaximusKaiserSoje.Core;
using TMPro;
using UnityEngine;

namespace DJMaximusKaiserSoje.Presentation
{
    /// <summary>
    /// The combo and the last judgement, over the lanes. It fades between hits so a held reading of
    /// the playfield is never blocked by stale text.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class JudgementFeedbackView : MonoBehaviour
    {
        [SerializeField] internal CanvasGroup judgementGroup;
        [SerializeField] internal TMP_Text gradeLabel;
        [SerializeField] internal TMP_Text gradeSuffixLabel;
        [SerializeField] internal TMP_Text timingLabel;
        [SerializeField] internal CanvasGroup comboGroup;
        [SerializeField] internal TMP_Text comboLabel;
        [SerializeField] internal TMP_Text comboCaptionLabel;
        [SerializeField] internal TMP_Text bannerLabel;
        [SerializeField] internal ScalePunch gradePunch;
        [SerializeField] internal ScalePunch comboPunch;

        [SerializeField] internal float holdSeconds = 0.35f;
        [SerializeField] internal float fadeSeconds = 0.25f;
        [SerializeField] internal float restingAlpha = 0.2f;

        private float sinceJudgement = float.MaxValue;

        private void Awake()
        {
            if (comboGroup != null) comboGroup.alpha = 0f;
            if (judgementGroup != null) judgementGroup.alpha = 0f;
            if (bannerLabel != null) bannerLabel.text = string.Empty;
        }

        public void ShowJudgement(JudgementEvent judgement)
        {
            sinceJudgement = 0f;

            if (gradeLabel != null)
            {
                gradeLabel.text = UiNaming.GradeName(judgement.Grade);
                gradeLabel.color = UiPalette.GradeColor(judgement.Grade);
            }

            if (gradeSuffixLabel != null)
            {
                gradeSuffixLabel.text = UiNaming.GradeSuffix(judgement.Grade);
                gradeSuffixLabel.color = UiPalette.GradeColor(judgement.Grade).WithAlpha(0.85f);
            }

            if (timingLabel != null)
            {
                if (judgement.Grade == JudgementGrade.Miss)
                {
                    timingLabel.text = string.Empty;
                }
                else
                {
                    timingLabel.text = judgement.Timing == JudgementTiming.Exact
                        ? string.Empty
                        : judgement.Timing == JudgementTiming.Fast ? "FAST" : "SLOW";
                    timingLabel.color = judgement.Timing == JudgementTiming.Fast ? UiPalette.Cyan : UiPalette.Amber;
                }
            }

            if (judgementGroup != null) judgementGroup.alpha = 1f;
            if (gradePunch != null) gradePunch.Punch();

            SetCombo(judgement.Combo);
        }

        public void ShowHoldTick(HoldTickEvent tick)
        {
            ShowJudgement(new JudgementEvent(
                tick.Lane,
                JudgementGrade.PerfectHigh,
                JudgementTiming.Exact,
                0.0,
                tick.Combo,
                isHoldRelease: false));
        }

        public void SetCombo(int combo)
        {
            if (comboLabel != null) comboLabel.text = UiFormat.Combo(combo);
            if (comboCaptionLabel != null) comboCaptionLabel.text = "COMBO";
            if (comboGroup != null) comboGroup.alpha = combo > 0 ? 1f : 0f;
            if (combo > 0 && comboPunch != null) comboPunch.Punch();
        }

        /// <summary>The lead-in count, and anything else worth one word over the lanes.</summary>
        public void ShowBanner(string message)
        {
            if (bannerLabel == null) return;
            bannerLabel.text = message ?? string.Empty;
        }

        public void Clear()
        {
            sinceJudgement = float.MaxValue;
            if (judgementGroup != null) judgementGroup.alpha = 0f;
            if (comboGroup != null) comboGroup.alpha = 0f;
            ShowBanner(string.Empty);
        }

        private void Update()
        {
            if (judgementGroup == null || sinceJudgement > holdSeconds + fadeSeconds) return;

            sinceJudgement += Time.unscaledDeltaTime;
            if (sinceJudgement <= holdSeconds) return;

            float t = Mathf.Clamp01((sinceJudgement - holdSeconds) / Mathf.Max(0.0001f, fadeSeconds));
            judgementGroup.alpha = Mathf.Lerp(1f, restingAlpha, t);
        }
    }
}
