using System;
using TMPro;
using UnityEngine;

namespace DJMaximusKaiserSoje.Presentation
{
    /// <summary>
    /// Counts a number up to its final value. The result screen uses it so a score lands rather than
    /// simply appearing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ValueTicker : MonoBehaviour
    {
        [SerializeField] internal TMP_Text label;
        [SerializeField] internal float duration = 0.8f;

        private Func<double, string> formatter = value => value.ToString("0");
        private double from;
        private double to;
        private float elapsed;
        private bool running;

        public void Play(double target, Func<double, string> format, float delay = 0f)
        {
            formatter = format ?? formatter;
            from = 0.0;
            to = target;
            elapsed = -Mathf.Max(0f, delay);
            running = true;
            Render(0.0);
        }

        public void ShowImmediate(double value, Func<double, string> format)
        {
            formatter = format ?? formatter;
            running = false;
            Render(value);
        }

        private void Update()
        {
            if (!running) return;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed < 0f) return;

            float progress = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            // Ease out so the last digits settle instead of racing past.
            float eased = 1f - (1f - progress) * (1f - progress);
            Render(from + (to - from) * eased);
            if (progress >= 1f) running = false;
        }

        private void Render(double value)
        {
            if (label != null) label.text = formatter(value);
        }
    }
}
