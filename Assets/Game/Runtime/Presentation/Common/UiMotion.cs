using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Presentation
{
    /// <summary>Breathes a graphic's alpha so an idle prompt does not read as a frozen screen.</summary>
    [DisallowMultipleComponent]
    public sealed class PulsingGraphic : MonoBehaviour
    {
        [SerializeField] internal Graphic target;
        [SerializeField] internal float minimumAlpha = 0.35f;
        [SerializeField] internal float maximumAlpha = 1f;
        [SerializeField] internal float cyclesPerSecond = 0.7f;

        private void Reset() => target = GetComponent<Graphic>();

        private void Update()
        {
            if (target == null) return;
            float phase = Mathf.Sin(Time.unscaledTime * cyclesPerSecond * Mathf.PI * 2f) * 0.5f + 0.5f;
            var color = target.color;
            color.a = Mathf.Lerp(minimumAlpha, maximumAlpha, phase);
            target.color = color;
        }
    }

    /// <summary>Eases an <see cref="Image"/> fill toward a target so gauges do not snap.</summary>
    [DisallowMultipleComponent]
    public sealed class SmoothFill : MonoBehaviour
    {
        [SerializeField] internal Image target;
        [SerializeField] internal float responseTime = 0.12f;

        private float goal;

        public void SetImmediate(float value)
        {
            goal = Mathf.Clamp01(value);
            if (target != null) target.fillAmount = goal;
        }

        public void Set(float value) => goal = Mathf.Clamp01(value);

        private void Update()
        {
            if (target == null) return;
            target.fillAmount = responseTime <= 0f
                ? goal
                : Mathf.MoveTowards(target.fillAmount, goal, Time.unscaledDeltaTime / responseTime);
        }
    }

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

    /// <summary>A one-shot scale punch, used when a judgement or a record lands.</summary>
    [DisallowMultipleComponent]
    public sealed class ScalePunch : MonoBehaviour
    {
        [SerializeField] internal RectTransform target;
        [SerializeField] internal float attack = 0.09f;
        [SerializeField] internal float settle = 0.11f;
        [SerializeField] internal float peakScale = 1.18f;

        private float elapsed = float.MaxValue;

        private void Reset() => target = GetComponent<RectTransform>();

        public void Punch() => elapsed = 0f;

        private void Update()
        {
            if (target == null || elapsed > attack + settle) return;

            elapsed += Time.unscaledDeltaTime;
            float scale;
            if (elapsed <= attack)
            {
                scale = Mathf.Lerp(1f, peakScale, Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(0.0001f, attack)));
            }
            else
            {
                float t = Mathf.SmoothStep(0f, 1f, (elapsed - attack) / Mathf.Max(0.0001f, settle));
                scale = Mathf.Lerp(peakScale, 1f, t);
            }

            target.localScale = Vector3.one * scale;
        }
    }
}
