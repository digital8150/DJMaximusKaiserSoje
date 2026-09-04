using System.Collections.Generic;
using DJMaximusKaiserSoje.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Editor
{
    internal enum Anchor
    {
        TopLeft,
        TopCentre,
        TopRight,
        MiddleLeft,
        Centre,
        MiddleRight,
        BottomLeft,
        BottomCentre,
        BottomRight
    }

    internal enum Weight
    {
        Regular,
        Medium,
        Bold,
        ExtraBold,
        Black
    }

    /// <summary>
    /// The vocabulary the screen builders are written in. Keeping construction here means a screen
    /// reads as its layout rather than as a hundred lines of RectTransform bookkeeping.
    /// </summary>
    internal static class Ui
    {
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;

        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<Weight, TMP_FontAsset> FontCache = new Dictionary<Weight, TMP_FontAsset>();

        public static void ResetCaches()
        {
            SpriteCache.Clear();
            FontCache.Clear();
        }

        // --- Assets ---------------------------------------------------------------------------

        public static Sprite Chrome(string name) => LoadSprite(ChromeSpriteGenerator.OutputFolder + "/" + name + ".png");

        public static Sprite Art(string name) => LoadSprite("Assets/Game/UI/Art/Generated/" + name + ".png");

        private static Sprite LoadSprite(string path)
        {
            if (SpriteCache.TryGetValue(path, out var cached)) return cached;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning("Sprite not found: " + path);
            SpriteCache[path] = sprite;
            return sprite;
        }

        public static TMP_FontAsset Font(Weight weight)
        {
            if (FontCache.TryGetValue(weight, out var cached)) return cached;
            var font = FontAssetBuilder.Load(weight.ToString());
            if (font == null) Debug.LogWarning("Font asset not found for weight " + weight);
            FontCache[weight] = font;
            return font;
        }

        // --- Construction ---------------------------------------------------------------------

        public static RectTransform Node(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = LayerMask.NameToLayer("UI");
            gameObject.transform.SetParent(parent, false);
            return (RectTransform)gameObject.transform;
        }

        public static Image Image(string name, Transform parent, Sprite sprite = null, Color? color = null)
        {
            var rect = Node(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color ?? Color.white;
            image.raycastTarget = false;

            bool sliced = sprite != null && sprite.border != Vector4.zero;
            image.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            // A border authored for the sprite's own size is too big for most widgets it is dropped
            // into, and Unity's answer to a border that does not fit is to flatten it.
            if (sliced) image.gameObject.AddComponent<SlicedImageFit>();
            return image;
        }

        public static Image Panel(string name, Transform parent, Color? color = null) =>
            Image(name, parent, Chrome("Panel"), color ?? UiPalette.Panel);

        public static Image CutPanel(string name, Transform parent, Color? color = null) =>
            Image(name, parent, Chrome("PanelCut"), color ?? UiPalette.Panel);

        public static Image BarPanel(string name, Transform parent, Color? color = null) =>
            Image(name, parent, Chrome("Bar"), color ?? UiPalette.PanelSoft);

        public static TextMeshProUGUI Text(
            string name,
            Transform parent,
            string value,
            float size,
            Weight weight = Weight.Medium,
            Color? color = null,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var rect = Node(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Font(weight);
            text.text = value;
            text.fontSize = size;
            text.color = color ?? UiPalette.TextPrimary;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        public static Button Button(string name, Transform parent, Sprite sprite = null, Color? color = null)
        {
            var image = Image(name, parent, sprite ?? Chrome("Bar"), color ?? UiPalette.PanelRaised);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            return button;
        }

        public static CanvasGroup Group(string name, Transform parent)
        {
            var rect = Node(name, parent);
            return rect.gameObject.AddComponent<CanvasGroup>();
        }

        // --- Layout ---------------------------------------------------------------------------

        public static T Set<T>(this T component, Anchor anchor, float x, float y, float width, float height)
            where T : Component
        {
            var rect = (RectTransform)component.transform;
            var (min, max, pivot) = Resolve(anchor);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            return component;
        }

        public static T Stretch<T>(this T component, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
            where T : Component
        {
            var rect = (RectTransform)component.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return component;
        }

        /// <summary>
        /// A full-height column. Offsets are exact edges, so a positive <paramref name="top"/> pushes
        /// the column past the top of its parent — which is how the playfield runs off the screen and
        /// notes arrive from outside it.
        /// </summary>
        public static T Column<T>(this T component, float anchorX, float left, float right, float bottom, float top)
            where T : Component
        {
            var rect = (RectTransform)component.transform;
            rect.anchorMin = new Vector2(anchorX, 0f);
            rect.anchorMax = new Vector2(anchorX, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
            return component;
        }

        /// <summary>Full width, fixed height, measured from the given edge.</summary>
        public static T Band<T>(this T component, Anchor anchor, float inset, float y, float height)
            where T : Component
        {
            var rect = (RectTransform)component.transform;
            bool fromTop = anchor == Anchor.TopLeft || anchor == Anchor.TopCentre || anchor == Anchor.TopRight;
            rect.anchorMin = new Vector2(0f, fromTop ? 1f : 0f);
            rect.anchorMax = new Vector2(1f, fromTop ? 1f : 0f);
            rect.pivot = new Vector2(0.5f, fromTop ? 1f : 0f);
            rect.offsetMin = new Vector2(inset, rect.offsetMin.y);
            rect.offsetMax = new Vector2(-inset, rect.offsetMax.y);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(-inset * 2f, height);
            return component;
        }

        /// <summary>
        /// Scales a sliced sprite's corners down with the art the widget is dropped into, so chrome
        /// inside a fitted frame carries the frame's radius rather than the full screen's.
        /// </summary>
        public static Image Fit(this Image image, float artScale)
        {
            var fit = image.GetComponent<SlicedImageFit>();
            if (fit != null) fit.SetArtScale(artScale);
            return image;
        }

        public static T Tint<T>(this T graphic, Color color) where T : Graphic
        {
            graphic.color = color;
            return graphic;
        }

        public static T Raycast<T>(this T graphic, bool enabled) where T : Graphic
        {
            graphic.raycastTarget = enabled;
            return graphic;
        }

        private static (Vector2 min, Vector2 max, Vector2 pivot) Resolve(Anchor anchor)
        {
            switch (anchor)
            {
                case Anchor.TopLeft: return (new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
                case Anchor.TopCentre: return (new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                case Anchor.TopRight: return (new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
                case Anchor.MiddleLeft: return (new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
                case Anchor.Centre: return (new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                case Anchor.MiddleRight: return (new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
                case Anchor.BottomLeft: return (Vector2.zero, Vector2.zero, Vector2.zero);
                case Anchor.BottomCentre: return (new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
                default: return (new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            }
        }
    }
}
