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
}
