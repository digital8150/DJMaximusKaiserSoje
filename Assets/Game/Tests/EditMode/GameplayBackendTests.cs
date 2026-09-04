using System;
using System.Collections.Generic;
using DJMaximusKaiserSoje.Core;
using DJMaximusKaiserSoje.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace DJMaximusKaiserSoje.Tests.EditMode
{
    public sealed class GameplayBackendTests
    {
        private sealed class FakeDsp : IDspTimeSource
        {
            public double DspTime { get; set; }
        }

        private sealed class FakeInput : ILaneInput
        {
            public event Action<int, double> Pressed;
            public event Action<int, double> Released;
            public bool Enabled { get; private set; }
            public void Enable() => Enabled = true;
            public void Disable() => Enabled = false;
            public void Dispose() => Enabled = false;
            public void Press(int lane, double timestamp) { if (Enabled) Pressed?.Invoke(lane, timestamp); }
            public void Release(int lane, double timestamp) { if (Enabled) Released?.Invoke(lane, timestamp); }
        }

        private sealed class FakeAudio : IAudioPlayback
        {
            public FakeAudio(double lengthSeconds) { LengthSeconds = lengthSeconds; }
            public double LengthSeconds { get; }
            public double ScheduledDspTime { get; private set; }
            public int PauseCount { get; private set; }
            public int ResumeCount { get; private set; }
            public bool Disposed { get; private set; }
            public void PlayScheduled(double dspTime) => ScheduledDspTime = dspTime;
            public void Pause() => PauseCount++;
            public void Resume() => ResumeCount++;
            public void Stop() { }
            public void Dispose() => Disposed = true;
        }

        private sealed class FakeInputTime : IInputTimeSource
        {
            public double CurrentTime { get; set; }
        }

        private sealed class EmptyRecords : IRecordStore
        {
            public event Action<ChartRecord> RecordChanged;
            public ChartRecord Get(string chartId) => ChartRecord.Empty(chartId);
            public ChartRecord Submit(PlayResult result) => result == null ? null : Get(result.ChartId);
        }

        [TestCase(499.9, false, 0.0)]
        [TestCase(500.0, true, 0.0)]
        [TestCase(1750.0, true, 1.25)]
        [TestCase(2500.0, false, 2.0)]
        public void BgaTimeline_AppliesBeatmapStartAndStopsAtVideoEnd(
            double songTimeMs, bool expectedVisible, double expectedVideoTime)
        {
            bool visible = BgaTimeline.TryResolveTime(songTimeMs, 500.0, 2.0, out double videoTime);

            Assert.That(visible, Is.EqualTo(expectedVisible));
            Assert.That(videoTime, Is.EqualTo(expectedVideoTime).Within(0.000001));
        }

        [TestCase(true, true, BgaVisualMode.Video)]
        [TestCase(true, false, BgaVisualMode.Video)]
        [TestCase(false, true, BgaVisualMode.Jacket)]
        [TestCase(false, false, BgaVisualMode.None)]
        public void BgaVisual_UsesJacketOnlyWhenVideoIsUnavailable(
            bool hasVideo, bool hasJacket, BgaVisualMode expected)
        {
            Assert.That(BgaTimeline.ResolveVisual(hasVideo, hasJacket), Is.EqualTo(expected));
        }

        [Test]
        public void Session_SchedulesAudioFromDspClockAndTranslatesInputTimestamp()
        {
            var dsp = new FakeDsp { DspTime = 10.0 };
            var inputTime = new FakeInputTime { CurrentTime = 10.0 };
            var input = new FakeInput();
            var audio = new FakeAudio(1.0);
            var chart = new Beatmap(new BeatmapHeader("T", "A", "Hard", 4),
                new[] { new BeatmapNote(0, 0.0, 0.0) });
            var session = new PlaySession("song", "song.hard", PlayStyle.FourKey, chart, audio, input, dsp,
                new EmptyRecords(), inputTime: inputTime);
            JudgementEvent judged = default;
            session.Judged += value => judged = value;

            session.Start();
            input.Press(0, 12.25);

            Assert.That(audio.ScheduledDspTime, Is.EqualTo(12.25).Within(0.000001));
            Assert.That(judged.Grade, Is.EqualTo(JudgementGrade.PerfectHigh));
            Assert.That(judged.OffsetMs, Is.EqualTo(0.0).Within(0.000001));
        }

        [Test]
        public void Session_HoldReleaseProducesSeparateReleaseJudgement()
        {
            var dsp = new FakeDsp { DspTime = 10.0 };
            var input = new FakeInput();
            var audio = new FakeAudio(1.0);
            var chart = new Beatmap(new BeatmapHeader("T", "A", "Hard", 4),
                new[] { new BeatmapNote(0, 0.0, 500.0) });
            var session = new PlaySession("song", "song.hard", PlayStyle.FourKey, chart, audio, input, dsp,
                new EmptyRecords());
            int judgements = 0;
            JudgementEvent last = default;
            session.Judged += value => { judgements++; last = value; };

            session.Start();
            input.Press(0, 12.25);
            input.Release(0, 12.75);

            Assert.That(judgements, Is.EqualTo(2));
            Assert.That(last.IsHoldRelease, Is.True);
            Assert.That(session.Score.Tally.PerfectHigh, Is.EqualTo(2));
        }

        [Test]
        public void Session_HeldLongNote_PulsesAtTwentyFourthNoteRateWithoutChangingScoreHealthOrAccuracy()
        {
            var dsp = new FakeDsp { DspTime = 10.0 };
            var input = new FakeInput();
            var audio = new FakeAudio(2.0);
            var chart = new Beatmap(new BeatmapHeader("T", "A", "Hard", 4, bpm: 120.0),
                new[] { new BeatmapNote(0, 0.0, 1600.0) });
            var session = new PlaySession("song", "song.hard", PlayStyle.FourKey, chart, audio, input, dsp,
                new EmptyRecords());
            var ticks = new List<HoldTickEvent>();
            session.HoldTicked += ticks.Add;

            session.Start();
            input.Press(0, 12.25);
            RunScore headScore = session.Score;
            HealthState headHealth = session.Health;
            dsp.DspTime = 12.3332;
            session.Tick();
            dsp.DspTime = 12.3334;
            session.Tick();
            dsp.DspTime = 12.4167;
            session.Tick();

            Assert.That(ticks.Count, Is.EqualTo(2));
            Assert.That(ticks[0].Combo, Is.EqualTo(2));
            Assert.That(ticks[1].Combo, Is.EqualTo(3));
            Assert.That(session.Score.Score, Is.EqualTo(headScore.Score));
            Assert.That(session.Score.Accuracy01, Is.EqualTo(headScore.Accuracy01));
            Assert.That(session.Score.Tally.Judged, Is.EqualTo(headScore.Tally.Judged));
            Assert.That(session.Health.Value, Is.EqualTo(headHealth.Value));
        }

        [Test]
        public void Session_ReleasingLongNoteEarly_ProducesOneMissRatherThanOnePerRemainingTick()
        {
            var dsp = new FakeDsp { DspTime = 10.0 };
            var input = new FakeInput();
            var audio = new FakeAudio(3.0);
            var chart = new Beatmap(new BeatmapHeader("T", "A", "Hard", 4, bpm: 120.0),
                new[] { new BeatmapNote(0, 0.0, 2000.0) });
            var session = new PlaySession("song", "song.hard", PlayStyle.FourKey, chart, audio, input, dsp,
                new EmptyRecords());
            int tickCount = 0;
            session.HoldTicked += _ => tickCount++;

            session.Start();
            input.Press(0, 12.25);
            input.Release(0, 12.30);
            dsp.DspTime = 14.25;
            session.Tick();

            Assert.That(tickCount, Is.EqualTo(0));
            Assert.That(session.Score.Tally.Miss, Is.EqualTo(1));
            Assert.That(session.Score.Tally.Judged, Is.EqualTo(2));
        }

        [Test]
        public void Session_RestartPublishesResetScoreAndHealthSnapshots()
        {
            var dsp = new FakeDsp { DspTime = 10.0 };
            var input = new FakeInput();
            var audio = new FakeAudio(1.0);
            var chart = new Beatmap(new BeatmapHeader("T", "A", "Hard", 4),
                new[] { new BeatmapNote(0, 0.0, 0.0) });
            var session = new PlaySession("song", "song.hard", PlayStyle.FourKey, chart, audio, input, dsp,
                new EmptyRecords());
            int scoreChanges = 0;
            int healthChanges = 0;
            session.ScoreChanged += _ => scoreChanges++;
            session.HealthChanged += _ => healthChanges++;

            session.Start();
            input.Press(0, 12.25);
            session.Restart();

            Assert.That(session.Score.JudgedCount, Is.EqualTo(0));
            Assert.That(session.Health.Value01, Is.EqualTo(1.0));
            Assert.That(scoreChanges, Is.EqualTo(3));
            Assert.That(healthChanges, Is.EqualTo(3));
        }

        [Test]
        public void Session_HoldHeadMissStillJudgesTheRelease()
        {
            var dsp = new FakeDsp { DspTime = 10.0 };
            var input = new FakeInput();
            var audio = new FakeAudio(1.0);
            var chart = new Beatmap(new BeatmapHeader("T", "A", "Hard", 4),
                new[] { new BeatmapNote(0, 0.0, 500.0) });
            var session = new PlaySession("song", "song.hard", PlayStyle.FourKey, chart, audio, input, dsp,
                new EmptyRecords());
            int judgements = 0;
            session.Judged += _ => judgements++;

            session.Start();
            input.Press(0, 12.40);
            input.Release(0, 12.75);

            Assert.That(judgements, Is.EqualTo(2));
            Assert.That(session.Score.Tally.Miss, Is.EqualTo(1));
            Assert.That(session.Score.Tally.PerfectHigh, Is.EqualTo(1));
        }

        [Test]
        public void Session_ResumeWaitsThreeSecondsBeforeAudioAndInputContinue()
        {
            var dsp = new FakeDsp { DspTime = 10.0 };
            var input = new FakeInput();
            var audio = new FakeAudio(10.0);
            var chart = new Beatmap(new BeatmapHeader("T", "A", "Hard", 4),
                new[] { new BeatmapNote(0, 5000.0, 5000.0) });
            var session = new PlaySession("song", "song.hard", PlayStyle.FourKey, chart, audio, input, dsp,
                new EmptyRecords());

            session.Start();
            dsp.DspTime = 13.0;
            session.Tick();
            session.Pause();
            double pausedTime = session.SongTimeMs;
            session.Resume();

            Assert.That(session.State, Is.EqualTo(PlaySessionState.Resuming));
            Assert.That(session.ResumeCountdownRemainingMs, Is.EqualTo(3000.0));
            Assert.That(session.SongTimeMs, Is.EqualTo(pausedTime - 3000.0).Within(0.001));
            Assert.That(input.Enabled, Is.False);

            dsp.DspTime = 15.99;
            session.Tick();
            Assert.That(session.State, Is.EqualTo(PlaySessionState.Resuming));
            Assert.That(audio.ResumeCount, Is.EqualTo(0));

            dsp.DspTime = 16.0;
            session.Tick();
            Assert.That(session.State, Is.EqualTo(PlaySessionState.Playing));
            Assert.That(session.SongTimeMs, Is.EqualTo(pausedTime).Within(0.001));
            Assert.That(audio.ResumeCount, Is.EqualTo(1));
            Assert.That(input.Enabled, Is.True);
        }
    }
}
