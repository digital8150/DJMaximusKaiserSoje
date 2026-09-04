using System;
using System.Collections.Generic;
using DJMaximusKaiserSoje.Content;
using DJMaximusKaiserSoje.Core;
using UnityEngine;

namespace DJMaximusKaiserSoje.Gameplay
{
    public interface IInputTimeSource
    {
        double CurrentTime { get; }
    }

    /// <summary>
    /// One song's playback, scheduled against the same clock the session judges on. The audio the
    /// adapter plays is fixed when it is constructed, so nothing Unity-typed reaches the session.
    /// </summary>
    public interface IAudioPlayback : IDisposable
    {
        double LengthSeconds { get; }

        void PlayScheduled(double dspTime);
        void Pause();
        void Resume();
        void Stop();
    }

    /// <summary>Small adapter that keeps Unity audio calls outside the session's judgement logic.</summary>
    public sealed class UnityAudioPlayback : IAudioPlayback
    {
        private readonly AudioSource source;

        public UnityAudioPlayback(AudioSource source, AudioClip clip)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0.0f;
            source.clip = clip;
        }

        public double LengthSeconds => source.clip == null ? 0.0 : source.clip.length;

        public void PlayScheduled(double dspTime) => source.PlayScheduled(dspTime);
        public void Pause() => source.Pause();
        public void Resume() => source.UnPause();
        public void Stop() => source.Stop();

        public void Dispose()
        {
            if (source == null) return;
            source.Stop();
            source.clip = null;
        }
    }

    public sealed class UnityDspTimeSource : IDspTimeSource
    {
        public double DspTime => AudioSettings.dspTime;
    }

    /// <summary>
    /// One deterministic gameplay run. The only time source used for note processing is the DSP
    /// clock; frame updates merely call Tick to observe that clock.
    /// </summary>
    public sealed class PlaySession : IPlaySession, IDisposable
    {
        public const double LeadInSeconds = 2.25;
        public const double ResumeCountdownSeconds = 3.0;

        private sealed class RuntimeNote
        {
            public int Id;
            public BeatmapNote Data;
            public int InputLane;
            public bool HeadJudged;
            public bool Complete;
        }

        private readonly string songId;
        private readonly string chartId;
        private readonly PlayStyle style;
        private readonly Beatmap beatmap;
        private readonly IAudioPlayback audio;
        private readonly ILaneInput input;
        private readonly IDspTimeSource dspTime;
        private readonly IInputTimeSource inputTime;
        private readonly IRecordStore records;
        private readonly JudgementEngine judgement;
        private readonly HealthRules healthRules;
        private readonly SectionTimeline sections;
        private readonly Func<DateTime> dateTimeProvider;
        private readonly List<RuntimeNote> notes;
        private readonly List<ActiveNote> pendingNoteViews = new List<ActiveNote>();
        private readonly bool[] laneHeld;
        private readonly double judgementOffsetMs;
        private readonly double songLengthMs;
        private DspSongClock clock;
        private ScoreAccumulator scoreAccumulator;
        private HealthState health;
        private SectionMarker currentSection;
        private bool disposed;
        private double inputToDspOffset;
        private double resumeCountdownStartDspTime;

        public PlaySession(
            string songId,
            string chartId,
            PlayStyle style,
            Beatmap beatmap,
            IAudioPlayback audio,
            ILaneInput input,
            IDspTimeSource dspTime,
            IRecordStore records,
            double judgementOffsetMs = 0.0,
            JudgementWindows judgementWindows = null,
            HealthRules healthRules = null,
            IInputTimeSource inputTime = null,
            Func<DateTime> dateTimeProvider = null)
        {
            this.songId = songId ?? throw new ArgumentNullException(nameof(songId));
            this.chartId = chartId ?? throw new ArgumentNullException(nameof(chartId));
            this.style = style;
            this.beatmap = beatmap ?? throw new ArgumentNullException(nameof(beatmap));
            this.audio = audio ?? throw new ArgumentNullException(nameof(audio));
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            this.dspTime = dspTime ?? throw new ArgumentNullException(nameof(dspTime));
            this.records = records ?? throw new ArgumentNullException(nameof(records));
            this.inputTime = inputTime;
            this.dateTimeProvider = dateTimeProvider ?? (() => DateTime.UtcNow);
            this.judgementOffsetMs = JudgementOffsetRange.Clamp(judgementOffsetMs);
            this.judgement = new JudgementEngine(judgementWindows ?? JudgementWindows.Default);
            this.healthRules = healthRules ?? new HealthRules();
            clock = new DspSongClock(dspTime);
            Layout = LaneLayout.Create(style);
            laneHeld = new bool[Layout.Lanes.Count];
            notes = new List<RuntimeNote>(beatmap.Notes.Count);
            for (int index = 0; index < beatmap.Notes.Count; index++)
            {
                BeatmapNote note = beatmap.Notes[index];
                notes.Add(new RuntimeNote
                {
                    Id = index,
                    Data = note,
                    InputLane = Layout.MapChartLane(note.Lane, beatmap.Header.KeyCount)
                });
            }
            notes.Sort((left, right) => left.Data.StartTimeMs.CompareTo(right.Data.StartTimeMs));

            int judgementCount = beatmap.Notes.Count;
            for (int index = 0; index < beatmap.Notes.Count; index++)
                if (beatmap.Notes[index].IsHold) judgementCount++;
            scoreAccumulator = new ScoreAccumulator(judgementCount, this.judgement);
            health = this.healthRules.StartingHealth;
            sections = new SectionTimeline(Math.Max(beatmap.LastNoteTimeMs, audio.LengthSeconds * 1000.0));
            currentSection = sections.GetSection(0.0);
            songLengthMs = Math.Max(beatmap.LastNoteTimeMs + judgement.Windows.MissMs, audio.LengthSeconds * 1000.0);
            if (songLengthMs <= 0.0) songLengthMs = judgement.Windows.MissMs;
            Chart = beatmap.Header;
            State = PlaySessionState.Ready;
            Score = scoreAccumulator.Current;
            Health = health;
            SongTimeMs = -LeadInSeconds * 1000.0;
            input.Pressed += OnLanePressed;
            input.Released += OnLaneReleased;
        }

        public PlaySessionState State { get; private set; }
        public string SongId => songId;
        public string ChartId => chartId;
        public RunScore Score { get; private set; }
        public HealthState Health { get; private set; }
        public SectionMarker CurrentSection => currentSection;
        public LaneLayout Layout { get; }
        public BeatmapHeader Chart { get; }
        public double SongTimeMs { get; private set; }
        public double SongLengthMs => songLengthMs;
        public double ResumeCountdownRemainingMs { get; private set; }
        public double Progress01 => Score.Progress01;
        public IReadOnlyList<ActiveNote> PendingNotes
        {
            get
            {
                pendingNoteViews.Clear();
                for (int index = 0; index < notes.Count; index++)
                {
                    RuntimeNote note = notes[index];
                    if (note.Complete) continue;
                    pendingNoteViews.Add(new ActiveNote(note.Id, note.InputLane, note.Data.StartTimeMs, note.Data.EndTimeMs, note.HeadJudged));
                }
                return pendingNoteViews;
            }
        }
        public double InputToDspOffset { get => inputToDspOffset; set => inputToDspOffset = value; }

        public event Action<JudgementEvent> Judged;
        public event Action<RunScore> ScoreChanged;
        public event Action<HealthState> HealthChanged;
        public event Action<SectionMarker> SectionChanged;
        public event Action<PlaySessionState> StateChanged;
        public event Action<int> LanePressed;
        public event Action<int> LaneReleased;
        public event Action<PlayResult> Finished;

        public void Start()
        {
            EnsureNotDisposed();
            if (State == PlaySessionState.Playing) return;
            if (State == PlaySessionState.Paused) return;
            ResetRunState();
            clock.Schedule(LeadInSeconds);
            inputToDspOffset = inputTime == null ? 0.0 : dspTime.DspTime - inputTime.CurrentTime;
            audio.PlayScheduled(clock.StartDspTime);
            input.Enable();
            SetState(PlaySessionState.Playing);
        }

        public void Tick()
        {
            EnsureNotDisposed();
            if (State == PlaySessionState.Resuming)
            {
                TickResumeCountdown();
                return;
            }
            if (State != PlaySessionState.Playing) return;
            SongTimeMs = clock.SongTimeMs + judgementOffsetMs;
            UpdateSection(SongTimeMs);

            for (int index = 0; index < notes.Count; index++)
            {
                RuntimeNote note = notes[index];
                if (!note.HeadJudged && SongTimeMs - note.Data.StartTimeMs > judgement.Windows.MissMs)
                {
                    JudgeNote(note, JudgementGrade.Miss, SongTimeMs - note.Data.StartTimeMs, false);
                    if (State != PlaySessionState.Playing) break;
                    continue;
                }
                if (note.HeadJudged && !note.Complete && note.Data.IsHold && SongTimeMs >= note.Data.EndTimeMs)
                {
                    JudgementGrade grade = laneHeld[note.InputLane] ? JudgementGrade.PerfectHigh : JudgementGrade.Miss;
                    JudgeHoldRelease(note, grade, SongTimeMs - note.Data.EndTimeMs);
                    if (State != PlaySessionState.Playing) break;
                }
            }

            if (AllNotesComplete() && clock.SongTimeMs >= songLengthMs)
                Finish(PlayOutcome.Cleared);
            else if (clock.SongTimeMs > songLengthMs + 500.0)
                Finish(PlayOutcome.Cleared);
        }

        public void SyncInputClock()
        {
            if (inputTime != null) inputToDspOffset = dspTime.DspTime - inputTime.CurrentTime;
        }

        public double SongTimeAtInput(double inputTimestamp)
        {
            if (clock == null || State == PlaySessionState.Ready) return -LeadInSeconds * 1000.0;
            return (inputTimestamp + inputToDspOffset - clock.StartDspTime) * 1000.0 + judgementOffsetMs;
        }

        public void Pause()
        {
            if (State == PlaySessionState.Resuming)
            {
                ResumeCountdownRemainingMs = 0.0;
                SongTimeMs = clock.SongTimeMs + judgementOffsetMs;
                SetState(PlaySessionState.Paused);
                return;
            }
            if (State != PlaySessionState.Playing) return;
            clock.Pause();
            audio.Pause();
            input.Disable();
            SetState(PlaySessionState.Paused);
        }

        public void Resume()
        {
            if (State != PlaySessionState.Paused) return;
            resumeCountdownStartDspTime = dspTime.DspTime;
            ResumeCountdownRemainingMs = ResumeCountdownSeconds * 1000.0;
            SongTimeMs = clock.SongTimeMs + judgementOffsetMs - ResumeCountdownRemainingMs;
            SetState(PlaySessionState.Resuming);
        }

        public void Restart()
        {
            EnsureNotDisposed();
            audio.Stop();
            input.Disable();
            ResetRunState();
            clock.Schedule(LeadInSeconds);
            SyncInputClock();
            audio.PlayScheduled(clock.StartDspTime);
            input.Enable();
            SetState(PlaySessionState.Playing);
        }

        private void TickResumeCountdown()
        {
            double elapsedMs = Math.Max(0.0, (dspTime.DspTime - resumeCountdownStartDspTime) * 1000.0);
            ResumeCountdownRemainingMs = Math.Max(0.0, ResumeCountdownSeconds * 1000.0 - elapsedMs);
            SongTimeMs = clock.SongTimeMs + judgementOffsetMs - ResumeCountdownRemainingMs;
            if (ResumeCountdownRemainingMs > 0.0) return;

            clock.Resume();
            SyncInputClock();
            audio.Resume();
            input.Enable();
            SongTimeMs = clock.SongTimeMs + judgementOffsetMs;
            SetState(PlaySessionState.Playing);
        }

        public void Abort()
        {
            if (State == PlaySessionState.Finished || State == PlaySessionState.Loading) return;
            Finish(PlayOutcome.Abandoned);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            input.Pressed -= OnLanePressed;
            input.Released -= OnLaneReleased;
            input.Dispose();
            audio.Stop();
            audio.Dispose();
        }

        private void OnLanePressed(int lane, double timestamp)
        {
            if (State != PlaySessionState.Playing || lane < 0 || lane >= laneHeld.Length) return;
            laneHeld[lane] = true;
            LanePressed?.Invoke(lane);
            double songTime = SongTimeAtInput(timestamp);
            RuntimeNote candidate = FindCandidate(lane, songTime);
            if (candidate == null) return;
            double offset = songTime - candidate.Data.StartTimeMs;
            if (!judgement.TryJudge(offset, out JudgementGrade grade)) return;
            candidate.HeadJudged = true;
            if (candidate.Data.IsHold)
            {
                ApplyJudgement(candidate.InputLane, grade, offset, false);
                return;
            }

            JudgeNote(candidate, grade, offset, false);
        }

        private void OnLaneReleased(int lane, double timestamp)
        {
            if (lane < 0 || lane >= laneHeld.Length) return;
            laneHeld[lane] = false;
            if (State != PlaySessionState.Playing) return;
            LaneReleased?.Invoke(lane);
            double songTime = SongTimeAtInput(timestamp);
            for (int index = 0; index < notes.Count; index++)
            {
                RuntimeNote note = notes[index];
                if (note.InputLane != lane || !note.HeadJudged || note.Complete || !note.Data.IsHold) continue;
                double offset = songTime - note.Data.EndTimeMs;
                JudgementGrade grade;
                if (offset < -judgement.Windows.GoodMs || !judgement.TryJudge(offset, out grade)) grade = JudgementGrade.Miss;
                JudgeHoldRelease(note, grade, offset);
                return;
            }
        }

        private RuntimeNote FindCandidate(int lane, double songTime)
        {
            RuntimeNote candidate = null;
            double candidateDistance = double.MaxValue;
            for (int index = 0; index < notes.Count; index++)
            {
                RuntimeNote note = notes[index];
                if (note.InputLane != lane || note.HeadJudged || note.Complete) continue;
                double distance = Math.Abs(songTime - note.Data.StartTimeMs);
                if (distance < candidateDistance)
                {
                    candidate = note;
                    candidateDistance = distance;
                }
            }

            return candidate;
        }

        private void JudgeNote(RuntimeNote note, JudgementGrade grade, double offset, bool isHoldRelease)
        {
            if (note.Complete) return;
            note.HeadJudged = true;
            note.Complete = true;
            ApplyJudgement(note.InputLane, grade, offset, isHoldRelease);
        }

        private void JudgeHoldRelease(RuntimeNote note, JudgementGrade grade, double offset)
        {
            if (note.Complete) return;
            note.Complete = true;
            ApplyJudgement(note.InputLane, grade, offset, true);
        }

        private void ApplyJudgement(int lane, JudgementGrade grade, double offset, bool isHoldRelease)
        {
            Score = scoreAccumulator.Feed(grade, judgement.TimingOf(offset));
            Health = healthRules.Apply(Health, grade);
            ScoreChanged?.Invoke(Score);
            HealthChanged?.Invoke(Health);
            Judged?.Invoke(new JudgementEvent(lane, grade, judgement.TimingOf(offset), offset, Score.Combo, isHoldRelease));
            if (healthRules.HasFailed(Health)) Finish(PlayOutcome.Failed);
        }

        private void ResetRunState()
        {
            SectionMarker previousSection = currentSection;
            for (int index = 0; index < notes.Count; index++)
            {
                notes[index].HeadJudged = false;
                notes[index].Complete = false;
            }
            Array.Clear(laneHeld, 0, laneHeld.Length);
            scoreAccumulator = new ScoreAccumulator(scoreAccumulator.Current.TotalNotes, judgement);
            Score = scoreAccumulator.Current;
            health = healthRules.StartingHealth;
            Health = health;
            SongTimeMs = -LeadInSeconds * 1000.0;
            ResumeCountdownRemainingMs = 0.0;
            currentSection = sections.GetSection(0.0);
            ScoreChanged?.Invoke(Score);
            HealthChanged?.Invoke(Health);
            if (!currentSection.Equals(previousSection)) SectionChanged?.Invoke(currentSection);
        }

        private bool AllNotesComplete()
        {
            for (int index = 0; index < notes.Count; index++)
                if (!notes[index].Complete) return false;
            return true;
        }

        private void UpdateSection(double songTime)
        {
            SectionMarker section = sections.GetSection(songTime);
            if (section.Equals(currentSection)) return;
            currentSection = section;
            SectionChanged?.Invoke(section);
        }

        private void Finish(PlayOutcome outcome)
        {
            if (State == PlaySessionState.Finished) return;
            input.Disable();
            audio.Stop();
            SetState(PlaySessionState.Finished);
            ChartRecord previous = records.Get(chartId) ?? ChartRecord.Empty(chartId);
            var result = new PlayResult(songId, chartId, style, Score,
                RankCalculator.FromAccuracy(Score.Accuracy01), outcome, previous, dateTimeProvider());
            Finished?.Invoke(result);
        }

        private void SetState(PlaySessionState state)
        {
            if (State == state) return;
            State = state;
            StateChanged?.Invoke(state);
        }

        private void EnsureNotDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(PlaySession));
        }

    }

    /// <summary>Bridges Unity's frame loop to a DSP-clocked session without putting UI in it.</summary>
    public sealed class PlaySessionDriver : MonoBehaviour
    {
        private PlaySession session;
        private LoadedSongContent content;
        private BgaPlayback bga;

        public void Bind(PlaySession value)
        {
            session = value ?? throw new ArgumentNullException(nameof(value));
        }

        public void Bind(PlaySession value, LoadedSongContent loadedContent)
        {
            Bind(value);
            content = loadedContent;
            bga = gameObject.AddComponent<BgaPlayback>();
            bga.Bind(session, content?.Video, content?.Jacket);
        }

        private void Update()
        {
            session?.Tick();
            bga?.Tick();
        }

        /// <summary>
        /// Frees the run now rather than at the end of the frame. Destroying the object is deferred,
        /// which is too late when the mixer that owns the song's sound is being torn down.
        /// </summary>
        public void ReleaseNow()
        {
            session?.Dispose();
            content?.Dispose();
            session = null;
            content = null;
        }

        private void OnDestroy() => ReleaseNow();
    }
}
