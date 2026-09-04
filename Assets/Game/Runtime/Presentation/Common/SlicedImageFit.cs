using UnityEngine;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Presentation
{
    /// <summary>
    /// Keeps a 9-sliced sprite's corners the shape they were drawn as.
    ///
    /// A border is measured in sprite pixels, so the cap of a 40 px pill stays 19 px tall however thin
    /// the widget is. Once the two opposing borders no longer fit, Unity squashes both into whatever
    /// room is left rather than shrinking the artwork — which is what turns a 4 px judgement line drawn
    /// from a round cap into a flat lens, and a 12 px gauge into an ellipse. The pixels-per-unit
    /// multiplier is the only dial that scales the border instead of crushing it, and its right value
    /// depends on the rect, so it is measured here rather than authored per widget.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    [ExecuteAlways]
    public sealed class SlicedImageFit : MonoBehaviour
    {
        [Tooltip("How far the art around this widget was scaled down. 1 keeps the corner as drawn.")]
        [SerializeField] internal float artScale = 1f;

        private Image image;

        private Image Target => image != null ? image : image = GetComponent<Image>();

        /// <summary>
        /// Shrinks the corner by the same factor as the art it sits in, so chrome dropped into a
        /// fitted frame carries that frame's radius rather than the screen's.
        /// </summary>
        public void SetArtScale(float scale)
        {
            artScale = Mathf.Clamp(scale, 0.01f, 1f);
            Apply();
        }

        /// <summary>
        /// The smallest multiplier that still leaves opposing borders room to sit side by side. At
        /// exactly that value a pill's two caps meet in the middle — the shape the sprite was drawn
        /// as — and each corner stays square, so it draws as a circle rather than an ellipse.
        /// </summary>
        public static float Multiplier(Vector4 border, float pixelsPerUnit, Vector2 size, float artScale)
        {
            float unit = Mathf.Max(0.0001f, pixelsPerUnit);
            float horizontal = size.x > 0.0001f ? (border.x + border.z) / (unit * size.x) : 0f;
            float vertical = size.y > 0.0001f ? (border.y + border.w) / (unit * size.y) : 0f;
            float floor = 1f / Mathf.Clamp(artScale, 0.01f, 1f);
            return Mathf.Max(floor, Mathf.Max(horizontal, vertical));
        }

        private void OnEnable() => Apply();

        private void OnRectTransformDimensionsChange() => Apply();

        private void OnValidate() => Apply();

        private void Apply()
        {
            Image target = Target;
            if (target == null || target.sprite == null || target.type != Image.Type.Sliced) return;
            if (target.sprite.border == Vector4.zero) return;

            float multiplier = Multiplier(target.sprite.border, target.pixelsPerUnit,
                target.rectTransform.rect.size, artScale);

            // Writing an unchanged value would dirty the scene every time one is opened.
            if (Mathf.Abs(target.pixelsPerUnitMultiplier - multiplier) < 0.0005f) return;
            target.pixelsPerUnitMultiplier = multiplier;
        }
    }
}
