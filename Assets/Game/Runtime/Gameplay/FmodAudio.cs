using System;
using System.Runtime.InteropServices;
using DJMaximusKaiserSoje.Core;

namespace DJMaximusKaiserSoje.Gameplay
{
    public sealed class FmodAudioException : Exception
    {
        public FmodAudioException(string message, FMOD.RESULT result)
            : base(message + " (" + FMOD.Error.String(result) + ")")
        {
            Result = result;
        }

        public FMOD.RESULT Result { get; }
    }

    /// <summary>
    /// The FMOD system the game mixes through, created and owned here rather than taken from the
    /// integration package's RuntimeManager. FMOD only accepts a DSP buffer size before a system is
    /// initialised, and buffer size is the one latency control a rhythm player tunes by ear, so the
    /// game keeps the ability to tear the mixer down and bring it back at a new size.
    /// </summary>
    public sealed class FmodOutput : IDisposable
    {
        /// <summary>One song, one preview, and headroom for the effects a chart may add later.</summary>
        private const int MaxChannels = 64;

        private FMOD.System system;
        private bool disposed;

        public FmodOutput(int dspBufferLength, int dspBufferCount)
        {
            Require(FMOD.Factory.System_Create(out system), "The FMOD system could not be created.");
            try
            {
                Require(system.setDSPBufferSize((uint)dspBufferLength, dspBufferCount),
                    "The requested audio buffer size was refused.");
#if UNITY_WEBGL && !UNITY_EDITOR
                Require(system.setOutput(FMOD.OUTPUTTYPE.WEBAUDIO), "The browser audio output is unavailable.");
#endif
                Require(system.init(MaxChannels, FMOD.INITFLAGS.NORMAL, IntPtr.Zero),
                    "The FMOD system could not be started.");
                Require(system.getMasterChannelGroup(out FMOD.ChannelGroup master),
                    "The FMOD master channel group is unavailable.");
                Require(system.getSoftwareFormat(out int rate, out _, out _),
                    "The FMOD output format is unavailable.");
                if (rate <= 0)
                    throw new FmodAudioException("FMOD reported no output sample rate.", FMOD.RESULT.ERR_INVALID_PARAM);

                // The device may refuse the exact size asked for, so report what it actually runs at.
                Require(system.getDSPBufferSize(out uint actualLength, out int actualCount),
                    "The audio buffer size could not be read back.");
                Master = master;
                SampleRate = rate;
                BufferLength = (int)actualLength;
                BufferCount = actualCount;
            }
            catch
            {
                system.release();
                system.clearHandle();
                throw;
            }
        }

        public int SampleRate { get; }

        /// <summary>The buffer size the device accepted, which may differ from the one requested.</summary>
        public int BufferLength { get; }

        public int BufferCount { get; }

        internal FMOD.System Core => system;

        internal FMOD.ChannelGroup Master { get; }

        /// <summary>
        /// False once the mixer is gone. Closing a system frees every sound and channel on it, so
        /// anything still holding one must check here before touching its handle.
        /// </summary>
        public bool IsOpen => !disposed && system.hasHandle();

        /// <summary>The mixer's output sample counter, in seconds since the device started.</summary>
        public double DspTimeSeconds =>
            !disposed && Master.getDSPClock(out ulong dspClock, out _) == FMOD.RESULT.OK
                ? SampleClock.ToSeconds(dspClock, SampleRate)
                : 0.0;

        /// <summary>Pumped once a frame; FMOD does its own mixing on another thread but needs this.</summary>
        public void Update()
        {
            if (!disposed) system.update();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (!system.hasHandle()) return;
            system.close();
            system.release();
            system.clearHandle();
        }

        internal static void Require(FMOD.RESULT result, string message)
        {
            if (result != FMOD.RESULT.OK) throw new FmodAudioException(message, result);
        }

        internal static FMOD.Sound CreateSound(FmodOutput output, byte[] encodedAudio, FMOD.MODE mode)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (encodedAudio == null || encodedAudio.Length == 0)
                throw new ArgumentException("The song has no audio data.", nameof(encodedAudio));

            var info = new FMOD.CREATESOUNDEXINFO
            {
                cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO)),
                length = (uint)encodedAudio.Length
            };
            // ACCURATETIME makes FMOD scan the file for its true length instead of estimating it,
            // which a variable-bitrate mp3 otherwise reports wrongly and the chart would outrun.
            Require(output.Core.createSound(encodedAudio, mode | FMOD.MODE.OPENMEMORY | FMOD.MODE.ACCURATETIME |
                                            FMOD.MODE.LOOP_OFF, ref info, out FMOD.Sound sound),
                "The song audio could not be decoded.");
            return sound;
        }

        internal static double LengthSecondsOf(FMOD.Sound sound)
        {
            FMOD.RESULT result = sound.getLength(out uint lengthMs, FMOD.TIMEUNIT.MS);
            if (result == FMOD.RESULT.OK) return lengthMs / 1000.0;
            sound.release();
            throw new FmodAudioException("The song length could not be read.", result);
        }
    }

    /// <summary>Reads <see cref="FmodOutput"/>'s sample counter as the session's authoritative clock.</summary>
    public sealed class FmodDspTimeSource : IDspTimeSource
    {
        private readonly FmodOutput output;

        public FmodDspTimeSource(FmodOutput output)
        {
            this.output = output ?? throw new ArgumentNullException(nameof(output));
        }

        public double DspTime => output.DspTimeSeconds;
    }

    /// <summary>
    /// Plays one song through FMOD from its encoded bytes. Start times arrive as absolute times on
    /// the same output clock the session judges against, which the mixer honours to the sample
    /// rather than to the frame.
    /// </summary>
    public sealed class FmodAudioPlayback : IAudioPlayback
    {
        private readonly FmodOutput output;
        private FMOD.Sound sound;
        private FMOD.Channel channel;
        private bool disposed;

        private FmodAudioPlayback(FmodOutput output, FMOD.Sound sound, double lengthSeconds)
        {
            this.output = output;
            this.sound = sound;
            LengthSeconds = lengthSeconds;
        }

        public double LengthSeconds { get; }

        /// <summary>
        /// Decodes the song up front. A chart is judged against this audio for its whole length, so
        /// the decode cost belongs to the loading screen rather than to a mid-song hitch.
        /// </summary>
        public static FmodAudioPlayback Create(byte[] encodedAudio, FmodOutput output)
        {
            FMOD.Sound sound = FmodOutput.CreateSound(output, encodedAudio, FMOD.MODE.CREATESAMPLE);
            return new FmodAudioPlayback(output, sound, FmodOutput.LengthSecondsOf(sound));
        }

        public void PlayScheduled(double dspTime)
        {
            if (disposed || !output.IsOpen) return;
            StopChannel();

            FmodOutput.Require(output.Core.playSound(sound, output.Master, true, out channel),
                "The song could not be started.");

            // A channel's delay counts on its parent group's clock, which is the same counter the
            // session's time source reads, so the requested time converts straight to samples.
            ulong start = SampleClock.ToSamples(dspTime, output.SampleRate);
            if (channel.getDSPClock(out _, out ulong parentClock) == FMOD.RESULT.OK && start < parentClock)
                start = parentClock;
            channel.setDelay(start, 0UL, true);
            channel.setPaused(false);
        }

        public void Pause()
        {
            if (channel.hasHandle() && output.IsOpen) channel.setPaused(true);
        }

        public void Resume()
        {
            if (channel.hasHandle() && output.IsOpen) channel.setPaused(false);
        }

        public void Stop() => StopChannel();

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            StopChannel();
            // Closing the system already freed this sound; releasing it again crashes FMOD.
            if (sound.hasHandle() && output.IsOpen) sound.release();
            sound.clearHandle();
        }

        private void StopChannel()
        {
            if (!channel.hasHandle()) return;
            if (output.IsOpen) channel.stop();
            channel.clearHandle();
        }
    }

    /// <summary>
    /// A song preview. It plays the same bytes the chart does but leaves them compressed in memory,
    /// because previews are started and thrown away as the player moves through the song list.
    /// </summary>
    public sealed class FmodPreviewPlayback : IDisposable
    {
        private readonly FmodOutput output;
        private FMOD.Sound sound;
        private FMOD.Channel channel;
        private bool disposed;

        private FmodPreviewPlayback(FmodOutput output, FMOD.Sound sound, double lengthSeconds)
        {
            this.output = output;
            this.sound = sound;
            LengthSeconds = lengthSeconds;
        }

        public double LengthSeconds { get; }

        public static FmodPreviewPlayback Create(byte[] encodedAudio, FmodOutput output)
        {
            FMOD.Sound sound = FmodOutput.CreateSound(output, encodedAudio, FMOD.MODE.CREATECOMPRESSEDSAMPLE);
            return new FmodPreviewPlayback(output, sound, FmodOutput.LengthSecondsOf(sound));
        }

        /// <summary>Starts silent at the given point; the caller fades it against the outgoing theme.</summary>
        public void Play(double startSeconds)
        {
            if (disposed || !output.IsOpen) return;
            Stop();
            FmodOutput.Require(output.Core.playSound(sound, output.Master, true, out channel),
                "The song preview could not be started.");
            channel.setVolume(0.0f);
            uint startMs = (uint)Math.Max(0.0, Math.Min((LengthSeconds - 0.01) * 1000.0, startSeconds * 1000.0));
            channel.setPosition(startMs, FMOD.TIMEUNIT.MS);
            channel.setPaused(false);
        }

        public void SetVolume(float volume)
        {
            if (channel.hasHandle() && output.IsOpen) channel.setVolume(volume);
        }

        public void Stop()
        {
            if (!channel.hasHandle()) return;
            if (output.IsOpen) channel.stop();
            channel.clearHandle();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Stop();
            // Closing the system already freed this sound; releasing it again crashes FMOD.
            if (sound.hasHandle() && output.IsOpen) sound.release();
            sound.clearHandle();
        }
    }
}
