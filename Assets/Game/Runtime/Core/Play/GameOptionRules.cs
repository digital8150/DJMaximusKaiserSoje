using System;
using System.Collections.Generic;

namespace DJMaximusKaiserSoje.Core
{
    public enum DisplayMode
    {
        Fullscreen,
        Borderless,
        Windowed
    }

    /// <summary>Deterministic defaults and validation shared by persistence, input, and the option UI.</summary>
    public static class GameOptionRules
    {
        public static readonly int[] AudioBufferSizes = { 128, 256, 512, 1024 };

        public static int NormalizeAudioBufferSize(int value)
        {
            int closest = AudioBufferSizes[0];
            int distance = Math.Abs(value - closest);
            for (int index = 1; index < AudioBufferSizes.Length; index++)
            {
                int candidateDistance = Math.Abs(value - AudioBufferSizes[index]);
                if (candidateDistance >= distance) continue;
                closest = AudioBufferSizes[index];
                distance = candidateDistance;
            }
            return closest;
        }

        public static string[] DefaultBindings(PlayStyle style)
        {
            LaneLayout layout = LaneLayout.Create(style);
            var result = new string[layout.Lanes.Count];
            for (int index = 0; index < result.Length; index++) result[index] = layout.Lanes[index].KeyName;
            return result;
        }

        public static string[] NormalizeBindings(PlayStyle style, IReadOnlyList<string> bindings)
        {
            string[] defaults = DefaultBindings(style);
            if (bindings == null || bindings.Count != defaults.Length) return defaults;

            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new string[defaults.Length];
            for (int index = 0; index < result.Length; index++)
            {
                string key = bindings[index]?.Trim();
                if (string.IsNullOrWhiteSpace(key) || !used.Add(key)) return defaults;
                result[index] = key;
            }
            return result;
        }

        /// <summary>Assigns a key while preserving one-key-per-lane by swapping an existing binding.</summary>
        public static string[] Rebind(PlayStyle style, IReadOnlyList<string> bindings, int lane, string keyName)
        {
            string[] result = NormalizeBindings(style, bindings);
            if (lane < 0 || lane >= result.Length) throw new ArgumentOutOfRangeException(nameof(lane));
            if (string.IsNullOrWhiteSpace(keyName)) throw new ArgumentException("A key name is required.", nameof(keyName));

            string normalized = keyName.Trim();
            int duplicate = -1;
            for (int index = 0; index < result.Length; index++)
                if (string.Equals(result[index], normalized, StringComparison.OrdinalIgnoreCase))
                {
                    duplicate = index;
                    break;
                }

            if (duplicate >= 0 && duplicate != lane) result[duplicate] = result[lane];
            result[lane] = normalized;
            return result;
        }
    }
}
