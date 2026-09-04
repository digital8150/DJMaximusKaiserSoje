using UnityEngine;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Presentation
{
    /// <summary>Eases an <see cref="Image"/> fill toward a target so gauges do not snap.</summary>
    [DisallowMultipleComponent]
    public sealed class SmoothFill : MonoBehaviour
    {
        [SerializeField] internal Image target;
        [SerializeField] internal float responseTime = 0.12f;

        private float goal;
        private float current;

        internal float CurrentValue => current;

        public void SetImmediate(float value)
        {
            goal = Mathf.Clamp01(value);
            current = goal;
            if (target != null) target.fillAmount = current;
        }

        public void Set(float value) => goal = Mathf.Clamp01(value);

        private void Update()
        {
            if (target == null) return;
            current = responseTime <= 0f
                ? goal
                : Mathf.MoveTowards(current, goal, Time.unscaledDeltaTime / responseTime);
            target.fillAmount = current;
        }
    }
}
