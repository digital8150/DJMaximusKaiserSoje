using System;
using System.IO;
using DJMaximusKaiserSoje.App;
using DJMaximusKaiserSoje.Core;
using NUnit.Framework;
using UnityEngine;

namespace DJMaximusKaiserSoje.Tests.EditMode
{
    public sealed class AppBackendTests
    {
        private sealed class FakeAudioDevice : IAudioDevice
        {
            public int BufferLength { get; private set; }
            public void SetBufferLength(int samples) => BufferLength = samples;
        }

        private sealed class FakePreviewClock : IPreviewClock
        {
            public double Now { get; set; }
        }

        [Test]
        public void PreviewDwell_RequestMustRemainHighlightedForHalfSecond()
        {
            var clock = new FakePreviewClock();
            var debounce = new PreviewDwellDebouncer(clock);
            debounce.Request("song-a");
            clock.Now = 0.49;
            Assert.That(debounce.TryTake(out _), Is.False);
            clock.Now = 0.50;

            Assert.That(debounce.TryTake(out string songId), Is.True);
            Assert.That(songId, Is.EqualTo("song-a"));
        }

        [Test]
        public void PreviewDwell_NewRequestReplacesUnstartedRequest()
        {
            var clock = new FakePreviewClock();
            var debounce = new PreviewDwellDebouncer(clock);
            debounce.Request("song-a");
            clock.Now = 0.25;
            debounce.Request("song-b");
            clock.Now = 0.50;

            Assert.That(debounce.TryTake(out _), Is.False);
            clock.Now = 0.75;
            Assert.That(debounce.TryTake(out string songId), Is.True);
            Assert.That(songId, Is.EqualTo("song-b"));
        }

        [Test]
        public void Records_WorseRunDoesNotOverwriteBetterBest()
        {
            string path = Path.Combine(Path.GetTempPath(), "djmaximus-record-test-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var records = new JsonRecordStore(path);
                var better = new PlayResult("song", "song.hard", PlayStyle.FourKey,
                    new RunScore(10000, 0.95, 95.0, 100, 100, 100, default), Rank.A,
                    PlayOutcome.Cleared, ChartRecord.Empty("song.hard"), DateTime.UtcNow);
                var worse = new PlayResult("song", "song.hard", PlayStyle.FourKey,
                    new RunScore(100, 0.50, 50.0, 2, 2, 100, default), Rank.F,
                    PlayOutcome.Failed, ChartRecord.Empty("song.hard"), DateTime.UtcNow);

                records.Submit(better);
                ChartRecord result = records.Submit(worse);

                Assert.That(result.BestScore, Is.EqualTo(10000));
                Assert.That(result.BestAccuracy01, Is.EqualTo(0.95));
                Assert.That(result.BestRating, Is.EqualTo(95.0));
                Assert.That(result.BestCombo, Is.EqualTo(100));
                Assert.That(result.BestRank, Is.EqualTo(Rank.A));
                Assert.That(result.Cleared, Is.True);
                Assert.That(result.PlayCount, Is.EqualTo(2));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void Records_CorruptFileStartsWithEmptyRecords()
        {
            string path = Path.Combine(Path.GetTempPath(), "djmaximus-corrupt-record-test-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(path, "not json");

                var records = new JsonRecordStore(path);

                Assert.That(records.Get("missing").IsEmpty, Is.True);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void Preferences_GearBackgroundOpacityPersistsAndClamps()
        {
            bool hadValue = PlayerPrefs.HasKey(PlayerPrefsPlayPreferences.GearBackgroundOpacityKey);
            float previous = PlayerPrefs.GetFloat(PlayerPrefsPlayPreferences.GearBackgroundOpacityKey);
            try
            {
                PlayerPrefs.SetFloat(PlayerPrefsPlayPreferences.GearBackgroundOpacityKey, 0.37f);
                var preferences = new PlayerPrefsPlayPreferences(new FakeAudioDevice());
                int changes = 0;
                preferences.Changed += () => changes++;

                Assert.That(preferences.GearBackgroundOpacity, Is.EqualTo(0.37f).Within(0.0001f));

                preferences.GearBackgroundOpacity = 2f;

                Assert.That(preferences.GearBackgroundOpacity, Is.EqualTo(1f));
                Assert.That(PlayerPrefs.GetFloat(PlayerPrefsPlayPreferences.GearBackgroundOpacityKey), Is.EqualTo(1f));
                Assert.That(changes, Is.EqualTo(1));
            }
            finally
            {
                if (hadValue)
                    PlayerPrefs.SetFloat(PlayerPrefsPlayPreferences.GearBackgroundOpacityKey, previous);
                else
                    PlayerPrefs.DeleteKey(PlayerPrefsPlayPreferences.GearBackgroundOpacityKey);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void ResultFade_UsesSmoothEndpointsAndMidpoint()
        {
            Assert.That(SceneFadeTransition.EvaluateAlpha(0f, 1f, 0f, 0.35f), Is.EqualTo(0f));
            Assert.That(SceneFadeTransition.EvaluateAlpha(0f, 1f, 0.175f, 0.35f), Is.EqualTo(0.5f));
            Assert.That(SceneFadeTransition.EvaluateAlpha(0f, 1f, 0.35f, 0.35f), Is.EqualTo(1f));
        }
    }
}
