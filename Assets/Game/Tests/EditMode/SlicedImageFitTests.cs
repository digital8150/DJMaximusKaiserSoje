using DJMaximusKaiserSoje.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Tests.EditMode
{
    /// <summary>
    /// A 9-slice border is measured in sprite pixels, so the same cap that rounds a 40 px pill is far
    /// too tall for the 4 px judgement line drawn from it. Unity squashes a border that does not fit,
    /// which is what turns round chrome into ovals; these cover the multiplier that shrinks it instead.
    /// </summary>
    public sealed class SlicedImageFitTests
    {
        // The pill the bars, rules and gauges are all cut from: 40 x 40, rounded by half its height.
        private static readonly Vector4 PillBorder = new Vector4(19f, 19f, 19f, 19f);

        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            root = null;
        }

        [Test]
        public void Multiplier_WhenTheBorderAlreadyFits_LeavesTheCornerAsDrawn()
        {
            float multiplier = SlicedImageFit.Multiplier(PillBorder, 1f, new Vector2(200f, 64f), 1f);

            Assert.That(multiplier, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void Multiplier_ForABarThinnerThanItsCaps_MakesTheCapsMeetInTheMiddle()
        {
            var size = new Vector2(320f, 12f);

            float multiplier = SlicedImageFit.Multiplier(PillBorder, 1f, size, 1f);
            float drawnBorder = PillBorder.y / multiplier;

            Assert.That(drawnBorder, Is.EqualTo(size.y * 0.5f).Within(0.001f),
                "Caps that meet exactly draw a pill; anything wider is squashed into a lens.");
            Assert.That(PillBorder.x / multiplier, Is.EqualTo(drawnBorder).Within(0.001f),
                "A corner has to stay square, or it draws as an ellipse rather than a circle.");
        }

        [Test]
        public void Multiplier_ForATallThinBar_FitsTheNarrowAxisToo()
        {
            float multiplier = SlicedImageFit.Multiplier(PillBorder, 1f, new Vector2(6f, 72f), 1f);

            Assert.That(PillBorder.x / multiplier, Is.EqualTo(3f).Within(0.001f));
        }

        [Test]
        public void Multiplier_ForChromeInsideScaledDownArt_ShrinksTheCornerWithIt()
        {
            const float gearScale = 1080f / 1571f;

            float multiplier = SlicedImageFit.Multiplier(new Vector4(0f, 15f, 0f, 15f), 1f,
                new Vector2(60f, 400f), gearScale);

            Assert.That(15f / multiplier, Is.EqualTo(15f * gearScale).Within(0.001f),
                "A note cap inside a gear fitted to the screen belongs at the gear's scale.");
        }

        [Test]
        public void Multiplier_WhenTheSpriteHasItsOwnPixelsPerUnit_MeasuresInWidgetPixels()
        {
            var size = new Vector2(320f, 12f);

            float multiplier = SlicedImageFit.Multiplier(PillBorder, 2f, size, 1f);

            Assert.That(PillBorder.y / (2f * multiplier), Is.EqualTo(size.y * 0.5f).Within(0.001f));
        }

        [Test]
        public void Fit_WhenTheWidgetIsResized_FollowsItsNewShape()
        {
            Image image = BuildSlicedImage(PillBorder);
            var fit = image.gameObject.AddComponent<SlicedImageFit>();

            image.rectTransform.sizeDelta = new Vector2(320f, 12f);

            Assert.That(image.pixelsPerUnitMultiplier, Is.EqualTo(38f / 12f).Within(0.001f));

            image.rectTransform.sizeDelta = new Vector2(320f, 64f);

            Assert.That(image.pixelsPerUnitMultiplier, Is.EqualTo(1f).Within(0.001f),
                "A widget that grew back past its border should get its authored corner back.");
            Assert.That(fit, Is.Not.Null);
        }

        [Test]
        public void Fit_OnAFilledImage_LeavesItAlone()
        {
            Image image = BuildSlicedImage(PillBorder);
            image.type = Image.Type.Filled;
            image.rectTransform.sizeDelta = new Vector2(320f, 12f);

            image.gameObject.AddComponent<SlicedImageFit>();

            Assert.That(image.pixelsPerUnitMultiplier, Is.EqualTo(1f).Within(0.001f),
                "A filled Image never draws the border, so there is nothing to fit.");
        }

        private Image BuildSlicedImage(Vector4 border)
        {
            root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var texture = new Texture2D(40, 40);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 40f, 40f), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, border);

            var holder = new GameObject("Bar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            holder.transform.SetParent(root.transform, false);
            var image = holder.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            return image;
        }
    }
}
