using System;
using DJMaximusKaiserSoje.Core;
using UnityEngine;

namespace DJMaximusKaiserSoje.Gameplay
{
    /// <summary>
    /// Owns the mixer for the whole session and keeps it pumped. A buffer size change cannot be
    /// applied to a running FMOD system, so this rebuilds it — every sound created on the old system
    /// dies with it, which is why holders are told to let go before the swap.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FmodAudioDevice : MonoBehaviour, IAudioDevice
    {
        /// <summary>Four blocks is FMOD's own default and keeps a small buffer from underrunning.</summary>
        private const int BufferCount = 4;

        private FmodOutput output;
        private int requestedBufferLength = GameOptionRules.DefaultAudioBufferSize;

        /// <summary>Raised before the mixer is torn down, so sound owners can drop their handles.</summary>
        public event Action Closing;

        /// <summary>Null while the mixer is unavailable, which is how a failed device stays non-fatal.</summary>
        public FmodOutput Output => output;

        public int BufferLength => output?.BufferLength ?? requestedBufferLength;

        public void SetBufferLength(int samples)
        {
            int normalized = GameOptionRules.NormalizeAudioBufferSize(samples);
            if (output != null && normalized == requestedBufferLength) return;
            requestedBufferLength = normalized;
            Rebuild();
        }

        private void Awake() => Rebuild();

        private void Update() => output?.Update();

        private void OnDestroy() => Close();

        private void Rebuild()
        {
            Close();
            try
            {
                output = new FmodOutput(requestedBufferLength, BufferCount);
            }
            catch (Exception exception)
            {
                output = null;
                Debug.LogError("게임 소리를 시작하지 못했습니다: " + exception.Message);
            }
        }

        private void Close()
        {
            if (output == null) return;
            Closing?.Invoke();
            output.Dispose();
            output = null;
        }
    }
}
