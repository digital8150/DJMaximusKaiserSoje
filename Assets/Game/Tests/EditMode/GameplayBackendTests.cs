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
            public AudioClip Clip { get; private set; }
            public void SetClip(AudioClip clip) => Clip = clip;
            public void PlayScheduled(double dspTime) => ScheduledDspTime = dspTime;
            public void Pause() { }
            public void Resume() { }
            public void Stop() { }
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
    }
}
