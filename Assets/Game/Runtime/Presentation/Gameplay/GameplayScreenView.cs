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
        [SerializeField] internal TMP_Text keyGuideLabel;

        [Header("Pause")]
        [SerializeField] internal CanvasGroup pauseOverlay;
        [SerializeField] internal TMP_Text pauseTitleLabel;
        [SerializeField] internal TMP_Text pauseGuideLabel;
        [SerializeField] internal Button resumeButton;
        [SerializeField] internal Button restartButton;
        [SerializeField] internal Button quitButton;

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
            if (keyGuideLabel != null) keyGuideLabel.text = "F1 / F2 노트 속도    Esc 일시정지";
            if (pauseTitleLabel != null) pauseTitleLabel.text = "일시정지";
            if (pauseGuideLabel != null) pauseGuideLabel.text = "계속할 준비가 되면 재개를 눌러 주세요";

            HookButtons();
            services.Preferences.Changed += OnPreferencesChanged;

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

        private void OnDestroy()
        {
            Unsubscribe();
            UnhookButtons();
            if (services != null) services.Preferences.Changed -= OnPreferencesChanged;
        }

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

            if (keyboard.f5Key.wasPressedThisFrame) Restart();
            if (keyboard.escapeKey.wasPressedThisFrame) TogglePause();
            if (keyboard.f1Key.wasPressedThisFrame) NudgeSpeed(-1);
            if (keyboard.f2Key.wasPressedThisFrame) NudgeSpeed(1);
        }

        private void UpdateCountdown()
        {
            if (feedback == null) return;

            if (session.State == PlaySessionState.Resuming)
            {
                int resumeCount = Mathf.Max(1,
                    Mathf.CeilToInt((float)session.ResumeCountdownRemainingMs / 1000f));
                string resumeBanner = resumeCount.ToString();
                if (resumeBanner == lastCountdown) return;
                lastCountdown = resumeBanner;
                countdownShown = true;
                feedback.ShowBanner(resumeBanner);
                return;
            }

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
            else if (session.State == PlaySessionState.Playing || session.State == PlaySessionState.Resuming)
                session.Pause();
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

        private void NudgeSpeed(int steps)
        {
            services.Preferences.ScrollSpeed =
                ScrollSpeedRange.Stepped(services.Preferences.ScrollSpeed, steps);
        }

        private void OnPreferencesChanged()
        {
            float speed = services.Preferences.ScrollSpeed;
            if (speedLabel != null) speedLabel.text = UiFormat.Speed(speed);
            playfield?.SetScrollSpeed(speed);
        }

        private void HookButtons()
        {
            UnhookButtons();
            resumeButton?.onClick.AddListener(Resume);
            restartButton?.onClick.AddListener(Restart);
            quitButton?.onClick.AddListener(Quit);
        }

        private void UnhookButtons()
        {
            resumeButton?.onClick.RemoveListener(Resume);
            restartButton?.onClick.RemoveListener(Restart);
            quitButton?.onClick.RemoveListener(Quit);
        }

        private void Resume()
        {
            if (session != null && session.State == PlaySessionState.Paused) session.Resume();
        }

        private void ShowPauseOverlay(bool visible)
        {
            if (pauseOverlay == null) return;
            pauseOverlay.alpha = visible ? 1f : 0f;
            pauseOverlay.blocksRaycasts = visible;
            pauseOverlay.interactable = visible;
        }
    }
}
