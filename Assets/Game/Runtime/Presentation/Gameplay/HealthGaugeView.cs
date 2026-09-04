using DJMaximusKaiserSoje.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Presentation
{
    /// <summary>The gauge beside the lanes. It reddens as it empties so the danger is visible.</summary>
    [DisallowMultipleComponent]
    public sealed class HealthGaugeView : MonoBehaviour
    {
        [SerializeField] internal Image fill;
        [SerializeField] internal SmoothFill smoothFill;
        [SerializeField] internal Image glow;
        [SerializeField] internal float dangerBelow = 0.3f;
        [SerializeField] internal float pulseHeight = 0.04f;

        private float immediateValue;
        private double songTimeMs;
        private double bpm;

        public void SetImmediate(HealthState health)
        {
            immediateValue = (float)health.Value01;
            if (smoothFill != null) smoothFill.SetImmediate((float)health.Value01);
            else if (fill != null) fill.fillAmount = (float)health.Value01;
            Recolor(health);
        }

        public void Set(HealthState health)
        {
            immediateValue = (float)health.Value01;
            if (smoothFill != null) smoothFill.Set((float)health.Value01);
            else if (fill != null) fill.fillAmount = (float)health.Value01;
            Recolor(health);
        }

        public void SetPulseTiming(double currentSongTimeMs, double chartBpm)
        {
            songTimeMs = currentSongTimeMs;
            bpm = chartBpm;
        }

        internal static float CalculateDisplayedFill(
            float baseFill, double currentSongTimeMs, double chartBpm, float pulseAmount)
        {
            float clampedBase = Mathf.Clamp01(baseFill);
            if (chartBpm <= 0.0 || double.IsNaN(chartBpm) || double.IsInfinity(chartBpm) || pulseAmount <= 0f)
                return clampedBase;

            double eighthNoteMs = 30000.0 / chartBpm;
            double elapsed = currentSongTimeMs % eighthNoteMs;
            if (elapsed < 0.0) elapsed += eighthNoteMs;
            float phase = (float)(elapsed / eighthNoteMs);
            float decay = 1f - phase;
            float pulse = pulseAmount * decay * decay;
            return Mathf.Min(1f, clampedBase + pulse);
        }

        private void LateUpdate()
        {
            if (fill == null) return;
            float baseFill = smoothFill == null ? immediateValue : smoothFill.CurrentValue;
            fill.fillAmount = CalculateDisplayedFill(baseFill, songTimeMs, bpm, pulseHeight);
        }

        private void Recolor(HealthState health)
        {
            var color = health.Value01 <= dangerBelow ? UiPalette.Rose : UiPalette.Cyan;
            if (fill != null) fill.color = color;
            if (glow != null) glow.color = color.WithAlpha(health.Value01 <= dangerBelow ? 0.5f : 0.25f);
        }
    }
}
