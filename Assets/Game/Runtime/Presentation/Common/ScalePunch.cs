using UnityEngine;

namespace DJMaximusKaiserSoje.Presentation
{
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
