using System.Collections;
using DJMaximusKaiserSoje.Core;
using DJMaximusKaiserSoje.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

namespace DJMaximusKaiserSoje.Tests.PlayMode
{
    /// <summary>
    /// Covers the audio path that only exists at runtime: FMOD is not initialised in the editor, so
    /// decoding a real song and reading the mixer's clock cannot be checked without entering play
    /// mode. The judgement rules built on top of this clock stay in the EditMode suite.
    /// </summary>
    public sealed class FmodAudioTests
    {
        private const string SongAudioAddress = "song.shooting-star.audio";
        private const int BufferCount = 4;

        private FmodOutput output;

        [TearDown]
        public void TearDown()
        {
            output?.Dispose();
            output = null;
        }

        private FmodOutput Open(int bufferLength = GameOptionRules.DefaultAudioBufferSize)
        {
            output?.Dispose();
            output = new FmodOutput(bufferLength, BufferCount);
            return output;
        }

        [Test]
        public void Output_ReportsASampleRateTheSessionCanScheduleAgainst()
        {
            FmodOutput opened = Open();

            Assert.That(opened.SampleRate, Is.GreaterThan(0), "FMOD reported no output sample rate.");
            Assert.That(SampleClock.ToSamples(1.0, opened.SampleRate), Is.EqualTo((ulong)opened.SampleRate));
        }

        [TestCase(64)]
        [TestCase(256)]
        [TestCase(1024)]
        public void Output_OpensAtTheRequestedBufferSize(int requested)
        {
            // This is the whole point of owning the system: the integration package fixes the
            // buffer at startup, and a rhythm player needs to move it.
            Assert.That(Open(requested).BufferLength, Is.EqualTo(requested));
        }

        [UnityTest]
        public IEnumerator Output_DspClockAdvancesWithRealTime()
        {
            var clock = new FmodDspTimeSource(Open());

            double start = clock.DspTime;
            yield return new WaitForSecondsRealtime(0.5f);
            double elapsed = clock.DspTime - start;

            // A mixer that stalls or restarts would break every judgement built on this clock.
            Assert.That(elapsed, Is.GreaterThan(0.2).And.LessThan(1.5),
                "The FMOD DSP clock did not advance with real time.");
        }

        [UnityTest]
        public IEnumerator Playback_DecodesTheDeliveredSongBytesAndReportsItsLength()
        {
            AsyncOperationHandle<TextAsset> handle = Addressables.LoadAssetAsync<TextAsset>(SongAudioAddress);
            yield return handle;
            Assert.That(handle.Status, Is.EqualTo(AsyncOperationStatus.Succeeded),
                "The song audio could not be loaded from Addressables.");

            byte[] encoded = handle.Result.bytes;
            Assert.That(encoded, Is.Not.Empty, "The delivered song audio was empty.");

            FmodAudioPlayback playback = FmodAudioPlayback.Create(encoded, Open());
            try
            {
                // ACCURATETIME is what makes this a real length rather than a bitrate estimate.
                Assert.That(playback.LengthSeconds, Is.GreaterThan(30.0),
                    "FMOD decoded the song but reported an implausible length.");
            }
            finally
            {
                playback.Dispose();
                Addressables.Release(handle);
            }
        }

        [UnityTest]
        public IEnumerator Playback_ScheduledStart_BeginsAtTheRequestedClockTime()
        {
            AsyncOperationHandle<TextAsset> handle = Addressables.LoadAssetAsync<TextAsset>(SongAudioAddress);
            yield return handle;
            Assert.That(handle.Status, Is.EqualTo(AsyncOperationStatus.Succeeded));

            FmodOutput opened = Open();
            var clock = new DspSongClock(new FmodDspTimeSource(opened));
            FmodAudioPlayback playback = FmodAudioPlayback.Create(handle.Result.bytes, opened);
            try
            {
                const double leadInSeconds = 0.75;
                clock.Schedule(leadInSeconds);
                playback.PlayScheduled(clock.StartDspTime);

                // Before the scheduled start the song has not begun, so its time is still negative.
                Assert.That(clock.SongTimeMs, Is.LessThan(0.0));

                yield return new WaitForSecondsRealtime((float)leadInSeconds + 0.25f);

                Assert.That(clock.SongTimeMs, Is.GreaterThan(0.0),
                    "The song clock did not pass its scheduled start.");
            }
            finally
            {
                playback.Dispose();
                Addressables.Release(handle);
            }
        }
    }
}
