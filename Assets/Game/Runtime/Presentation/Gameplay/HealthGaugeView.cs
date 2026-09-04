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

        public void SetImmediate(HealthState health)
        {
            if (smoothFill != null) smoothFill.SetImmediate((float)health.Value01);
            else if (fill != null) fill.fillAmount = (float)health.Value01;
            Recolor(health);
        }

        public void Set(HealthState health)
        {
            if (smoothFill != null) smoothFill.Set((float)health.Value01);
            else if (fill != null) fill.fillAmount = (float)health.Value01;
            Recolor(health);
        }

        private void Recolor(HealthState health)
        {
            var color = health.Value01 <= dangerBelow ? UiPalette.Rose : UiPalette.Cyan;
            if (fill != null) fill.color = color;
            if (glow != null) glow.color = color.WithAlpha(health.Value01 <= dangerBelow ? 0.5f : 0.25f);
        }
    }
}
