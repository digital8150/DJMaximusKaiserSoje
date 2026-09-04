using DJMaximusKaiserSoje.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Presentation
{
    /// <summary>
    /// What the run came to. The three headline numbers count up, and the ribbons only appear where
    /// the player actually improved on their stored best.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResultScreenView : MonoBehaviour, IResultScreenView
    {
        [Header("Song")]
        [SerializeField] internal SongCardView songCard;

        [Header("Headline")]
        [SerializeField] internal ResultRowView ratingRow;
        [SerializeField] internal ResultRowView accuracyRow;
        [SerializeField] internal ResultRowView scoreRow;

        [Header("Breakdown")]
        [SerializeField] internal TMP_Text totalNotesLabel;
        [SerializeField] internal TMP_Text[] gradeCountLabels;
        [SerializeField] internal TMP_Text[] gradeCaptionLabels;
        [SerializeField] internal TMP_Text maxComboLabel;
        [SerializeField] internal FastSlowBarView fastSlowBar;

        [Header("Rank")]
        [SerializeField] internal RankBadgeView rankBadge;

        [Header("Player")]
        [SerializeField] internal TMP_Text playerNameLabel;
        [SerializeField] internal TMP_Text playerLevelLabel;
        [SerializeField] internal Image playerExpFill;

        [Header("Footer")]
        [SerializeField] internal TMP_Text keyGuideLabel;

        private GameServices services;
        private PlayResult result;
        private bool leaving;

        public void Bind(GameServices gameServices)
        {
            services = gameServices;
            leaving = false;

            if (keyGuideLabel != null) keyGuideLabel.text = "F5 다시하기    Enter / Esc 곡 선택으로";
            if (playerNameLabel != null) playerNameLabel.text = services.Profile.DisplayName;
            if (playerLevelLabel != null) playerLevelLabel.text = UiFormat.Level(services.Profile.Level);
            if (playerExpFill != null) playerExpFill.fillAmount = (float)services.Profile.ExpProgress01;

            ApplyGradeCaptions();
            services.Music.PlayTheme(ScreenTheme.Result);
        }

        public void ShowResult(PlayResult playResult)
        {
            result = playResult;
            var score = result.Score;

            if (songCard != null &&
                services.Songs.TryGetChart(result.ChartId, out var chart) &&
                services.Songs.TryGetSong(result.SongId, out var song))
                songCard.Bind(song, chart, result.Style);

            ratingRow?.Bind("RATING", score.Rating, value => UiFormat.Rating(value),
                result.IsNewRatingRecord, UiFormat.SignedRating(result.RatingDelta), 0.1f);

            accuracyRow?.Bind("JUDGE", score.Accuracy01, value => UiFormat.AccuracyPrecise(value),
                result.IsNewAccuracyRecord, UiFormat.SignedAccuracy(result.AccuracyDelta), 0.25f);

            scoreRow?.Bind("SCORE", score.Score, value => UiFormat.Score((long)value),
                result.IsNewScoreRecord, UiFormat.SignedScore(result.ScoreDelta), 0.4f);

            if (totalNotesLabel != null) totalNotesLabel.text = score.TotalNotes.ToString("N0");
            if (maxComboLabel != null) maxComboLabel.text = UiFormat.Combo(score.MaxCombo);

            if (gradeCountLabels != null)
            {
                for (int index = 0; index < gradeCountLabels.Length; index++)
                {
                    if (gradeCountLabels[index] == null) continue;
                    gradeCountLabels[index].text = UiFormat.PaddedCount(score.Tally.CountOf((JudgementGrade)index));
                }
            }

            fastSlowBar?.Bind(score.Tally.Fast, score.Tally.Slow);
            rankBadge?.Bind(result.Rank, score, result.Outcome);
        }

        private void ApplyGradeCaptions()
        {
            if (gradeCaptionLabels == null) return;
            for (int index = 0; index < gradeCaptionLabels.Length; index++)
            {
                if (gradeCaptionLabels[index] == null) continue;
                var grade = (JudgementGrade)index;
                gradeCaptionLabels[index].text = UiNaming.TallyLabel(grade);
                gradeCaptionLabels[index].color = UiPalette.GradeColor(grade);
            }
        }

        private void Update()
        {
            if (services == null || leaving) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f5Key.wasPressedThisFrame && services.Flow.CanRetry)
            {
                leaving = true;
                services.Music.StopTheme();
                services.Flow.RetryLast();
                return;
            }

            if (!keyboard.enterKey.wasPressedThisFrame &&
                !keyboard.numpadEnterKey.wasPressedThisFrame &&
                !keyboard.escapeKey.wasPressedThisFrame)
                return;

            leaving = true;
            services.Flow.ShowSongSelect();
        }
    }
}
