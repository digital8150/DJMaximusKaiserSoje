using DJMaximusKaiserSoje.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Tests.EditMode
{
    public sealed class HealthGaugeViewTests
    {
        [Test]
        public void Pulse_AtEighthNoteBoundary_RaisesTheDisplayedFill()
        {
            float result = HealthGaugeView.CalculateDisplayedFill(0.8f, 250.0, 120.0, 0.04f);

            Assert.That(result, Is.EqualTo(0.84f).Within(0.0001f));
        }

        [Test]
        public void Pulse_WhenGaugeIsFull_NeverDrawsPastMaximum()
        {
            float result = HealthGaugeView.CalculateDisplayedFill(1f, 0.0, 180.0, 0.04f);

            Assert.That(result, Is.EqualTo(1f));
        }

        [Test]
        public void SmoothFill_SetImmediateKeepsAnUnpulsedBaseValue()
        {
            var root = new GameObject("Gauge", typeof(RectTransform));
            try
            {
                var image = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(root.transform, false);
                var smooth = root.AddComponent<SmoothFill>();
                smooth.target = image;

                smooth.SetImmediate(0.6f);

                Assert.That(smooth.CurrentValue, Is.EqualTo(0.6f).Within(0.0001f));
                Assert.That(image.fillAmount, Is.EqualTo(0.6f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
