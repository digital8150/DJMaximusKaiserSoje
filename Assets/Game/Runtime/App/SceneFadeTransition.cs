using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.App
{
    /// <summary>A persistent full-screen curtain used while the result scene replaces gameplay.</summary>
    [DisallowMultipleComponent]
    public sealed class SceneFadeTransition : MonoBehaviour
    {
        public const float DefaultDurationSeconds = 0.35f;

        [SerializeField] internal float durationSeconds = DefaultDurationSeconds;

        private CanvasGroup curtain;

        private void Awake()
        {
            BuildCurtain();
            SetImmediate(0f);
        }

        public IEnumerator FadeToBlack() => FadeTo(1f);

        public IEnumerator FadeFromBlack() => FadeTo(0f);

        internal static float EvaluateAlpha(float start, float end, float elapsed, float duration)
        {
            if (duration <= 0f) return end;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = progress * progress * (3f - 2f * progress);
            return Mathf.Lerp(start, end, eased);
        }

        private IEnumerator FadeTo(float target)
        {
            BuildCurtain();
            float start = curtain.alpha;
            float elapsed = 0f;
            curtain.blocksRaycasts = true;

            while (elapsed < durationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                curtain.alpha = EvaluateAlpha(start, target, elapsed, durationSeconds);
                yield return null;
            }

            SetImmediate(target);
        }

        private void SetImmediate(float alpha)
        {
            if (curtain == null) return;
            curtain.alpha = Mathf.Clamp01(alpha);
            curtain.blocksRaycasts = curtain.alpha > 0f;
            curtain.interactable = false;
        }

        private void BuildCurtain()
        {
            if (curtain != null) return;

            var curtainObject = new GameObject("SceneFadeCurtain", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(Image));
            curtainObject.transform.SetParent(transform, false);

            var rect = (RectTransform)curtainObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var canvas = curtainObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            var scaler = curtainObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var image = curtainObject.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = true;

            curtain = curtainObject.GetComponent<CanvasGroup>();
        }
    }
}
