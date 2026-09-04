using DJMaximusKaiserSoje.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Presentation
{
    /// <summary>
    /// The play screen. It owns nothing about the run: the session raises what happened and this
    /// arranges it on screen.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayScreenView : MonoBehaviour, IGameplayScreenView
    {
        [Header("Playfield")]
        [SerializeField] internal PlayfieldView playfield;
        [SerializeField] internal JudgementFeedbackView feedback;

        [Header("Panels")]
        [SerializeField] internal SongCardView songCard;
        [SerializeField] internal TallyPanelView tally;
        [SerializeField] internal HealthGaugeView health;
        [SerializeField] internal ProgressStripView progress;

        [Header("Player")]
        [SerializeField] internal TMP_Text playerNameLabel;
        [SerializeField] internal TMP_Text playerTagLabel;
        [SerializeField] internal TMP_Text scoreLabel;
        [SerializeField] internal Image playerAvatar;

        [Header("Chips")]
        [SerializeField] internal TMP_Text speedLabel;
        [SerializeField] internal TMP_Text offsetLabel;
        [SerializeField] internal TMP_Text keyGuideLabel;

        [Header("Pause")]
        [SerializeField] internal CanvasGroup pauseOverlay;
        [SerializeField] internal TMP_Text pauseTitleLabel;
        [SerializeField] internal TMP_Text pauseGuideLabel;

        private GameServices services;
        private IPlaySession session;
        private bool leaving;
        private bool countdownShown;
        private string lastCountdown;

        /// <summary>The run this screen is showing, or null before one is bound.</summary>
        internal IPlaySession Session => session;

        public void Bind(GameServices gameServices)
        {
            services = gameServices;
            leaving = false;

            if (playerNameLabel != null) playerNameLabel.text = services.Profile.DisplayName;
            if (playerTagLabel != null) playerTagLabel.text = services.Profile.Tag;
            if (speedLabel != null) speedLabel.text = UiFormat.Speed(services.Preferences.ScrollSpeed);
            if (offsetLabel != null) offsetLabel.text = UiFormat.Offset(services.Preferences.JudgementOffsetMs);
            if (keyGuideLabel != null) keyGuideLabel.text = "Space 일시정지    F5 다시하기    Esc 곡 선택";
            if (pauseTitleLabel != null) pauseTitleLabel.text = "일시정지";
            if (pauseGuideLabel != null) pauseGuideLabel.text = "Space 계속하기    F5 처음부터    Esc 곡 선택으로";

            ShowPauseOverlay(false);
        }

        public void BindSession(IPlaySession playSession)
        {
            Unsubscribe();
            session = playSession;
            lastCountdown = null;
            countdownShown = false;

            if (playfield != null) playfield.Bind(session, services.Preferences.ScrollSpeed);
            if (feedback != null) feedback.Clear();

            if (songCard != null)
            {
                if (services.Songs.TryGetChart(session.ChartId, out var chart) &&
                    services.Songs.TryGetSong(session.SongId, out var song))
                    songCard.Bind(song, chart, session.Layout.Style);
                else
                    songCard.Bind(session.Chart, session.Layout.Style);
            }

            if (tally != null) tally.Bind(session.Score);
            if (scoreLabel != null) scoreLabel.text = UiFormat.Score(session.Score.Score);
            if (health != null) health.SetImmediate(session.Health);
            if (progress != null)
            {
                progress.SetProgress(0.0, 0.0, session.SongLengthMs);
                progress.SetSection(session.CurrentSection);
            }

            session.Judged += OnJudged;
            session.ScoreChanged += OnScoreChanged;
            session.HealthChanged += OnHealthChanged;
            session.SectionChanged += OnSectionChanged;
            session.StateChanged += OnStateChanged;
            session.Finished += OnFinished;
        }

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (session == null) return;
            session.Judged -= OnJudged;
            session.ScoreChanged -= OnScoreChanged;
            session.HealthChanged -= OnHealthChanged;
            session.SectionChanged -= OnSectionChanged;
            session.StateChanged -= OnStateChanged;
            session.Finished -= OnFinished;
            session = null;
        }

        private void OnJudged(JudgementEvent judgement) => feedback?.ShowJudgement(judgement);

        private void OnScoreChanged(RunScore score)
        {
            tally?.Bind(score);
            if (scoreLabel != null) scoreLabel.text = UiFormat.Score(score.Score);
        }

        private void OnHealthChanged(HealthState state) => health?.Set(state);

        private void OnSectionChanged(SectionMarker section) => progress?.SetSection(section);

        private void OnStateChanged(PlaySessionState state)
        {
            ShowPauseOverlay(state == PlaySessionState.Paused);
            if (state == PlaySessionState.Playing) feedback?.ShowBanner(string.Empty);
        }

        private void OnFinished(PlayResult result)
        {
            if (leaving) return;
            leaving = true;
            services.Flow.ShowResult(result);
        }

        private void Update()
        {
            if (session == null || leaving) return;

            UpdateCountdown();
            if (progress != null)
                progress.SetProgress(session.Progress01, session.SongTimeMs, session.SongLengthMs);

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.spaceKey.wasPressedThisFrame) TogglePause();
            if (keyboard.f5Key.wasPressedThisFrame) Restart();
            if (keyboard.escapeKey.wasPressedThisFrame) Quit();
            if (keyboard.leftBracketKey.wasPressedThisFrame) NudgeOffset(-JudgementOffsetRange.StepMs);
            if (keyboard.rightBracketKey.wasPressedThisFrame) NudgeOffset(JudgementOffsetRange.StepMs);
        }

        private void UpdateCountdown()
        {
            if (feedback == null) return;

            double songTimeMs = session.SongTimeMs;
            if (songTimeMs >= 0.0)
            {
                if (countdownShown) feedback.ShowBanner(string.Empty);
                countdownShown = false;
                return;
            }

            int count = Mathf.CeilToInt((float)(-songTimeMs) / 1000f);
            string banner = count > 0 ? count.ToString() : "GO";
            if (banner == lastCountdown) return;

            lastCountdown = banner;
            countdownShown = true;
            feedback.ShowBanner(banner);
        }

        private void TogglePause()
        {
            if (session.State == PlaySessionState.Paused) session.Resume();
            else session.Pause();
        }

        private void Restart()
        {
            feedback?.Clear();
            session.Restart();
        }

        private void Quit()
        {
            leaving = true;
            session.Abort();
            services.Flow.ShowSongSelect();
        }

        private void NudgeOffset(double deltaMs)
        {
            services.Preferences.JudgementOffsetMs =
                JudgementOffsetRange.Clamp(services.Preferences.JudgementOffsetMs + deltaMs);
            if (offsetLabel != null) offsetLabel.text = UiFormat.Offset(services.Preferences.JudgementOffsetMs);
        }

        private void ShowPauseOverlay(bool visible)
        {
            if (pauseOverlay == null) return;
            pauseOverlay.alpha = visible ? 1f : 0f;
            pauseOverlay.blocksRaycasts = visible;
        }
    }
}
