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
}
