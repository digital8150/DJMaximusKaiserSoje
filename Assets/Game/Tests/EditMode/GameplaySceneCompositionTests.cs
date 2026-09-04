using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Tests.EditMode
{
    public sealed class GameplaySceneCompositionTests
    {
        private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
        private const string GearSpritePath = "Assets/Game/UI/Art/Generated/GameplayGear.png";
        private const float GearScale = 1080f / 1571f;

        [Test]
        public void GameplayGearFrame_WhenSceneIsBuilt_ReplacesCompositeRailsAndAlignsLiveLayers()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(GameplayScenePath);
            try
            {
                GameObject gear = Find(scene, "Gear");
                GameObject frame = Find(scene, "GearFrame");
                GameObject viewport = Find(scene, "LaneViewport");
                GameObject deckLayer = Find(scene, "DeckLayer");
                GameObject healthGauge = Find(scene, "HealthGauge");

                Assert.That(gear, Is.Not.Null);
                Assert.That(frame, Is.Not.Null);
                Assert.That(viewport, Is.Not.Null);
                Assert.That(deckLayer, Is.Not.Null);
                Assert.That(healthGauge, Is.Not.Null);
                Assert.That(Find(scene, "GearBody"), Is.Null);
                Assert.That(Find(scene, "RailLeft"), Is.Null);
                Assert.That(Find(scene, "RailRight"), Is.Null);
                Assert.That(Find(scene, "Deck"), Is.Null);

                var frameImage = frame.GetComponent<Image>();
                Assert.That(frameImage, Is.Not.Null);
                Assert.That(frameImage.sprite, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(frameImage.sprite), Is.EqualTo(GearSpritePath));

                AssertRect((RectTransform)gear.transform, Vector2.zero, new Vector2(784f * GearScale, 1080f));
                AssertRect((RectTransform)viewport.transform,
                    new Vector2(28f, 388f) * GearScale,
                    new Vector2(674f, 1183f) * GearScale);
                Assert.That(viewport.GetComponent<Image>().color.a, Is.EqualTo(0.55f).Within(0.001f));
                Assert.That(deckLayer.GetComponent<Image>().color.a, Is.EqualTo(0.82f).Within(0.001f));
                Assert.That(healthGauge.transform.parent, Is.EqualTo(gear.transform));
                Assert.That(healthGauge.GetComponentInChildren<Image>().color.a, Is.EqualTo(1f));

                MonoBehaviour playfield = FindBehaviour(gear, "PlayfieldView");
                Assert.That(playfield, Is.Not.Null);
                var serializedPlayfield = new SerializedObject(playfield);
                Assert.That(serializedPlayfield.FindProperty("geometryScale").floatValue,
                    Is.EqualTo(GearScale).Within(0.001f));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void HealthGauge_WhenSceneIsBuilt_CoversTheBakedBarWithAFillThatCanShrink()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(GameplayScenePath);
            try
            {
                GameObject gauge = Find(scene, "HealthGauge");
                var gaugeRect = (RectTransform)gauge.transform;
                var track = Find(gauge.transform, "Track").GetComponent<Image>();
                var fill = Find(gauge.transform, "Fill").GetComponent<Image>();

                // The frame paints a full gauge into this well. Anything the track does not cover
                // stays lit no matter how much health is left.
                AssertRect(gaugeRect, new Vector2(729f, 360f) * GearScale, new Vector2(21f, 608f) * GearScale);
                Assert.That(track.color.a, Is.EqualTo(1f), "A see-through track leaves the painted bar showing.");

                Assert.That(fill.type, Is.EqualTo(Image.Type.Filled));
                Assert.That(fill.fillMethod, Is.EqualTo(Image.FillMethod.Vertical));
                Assert.That(fill.fillOrigin, Is.EqualTo((int)Image.OriginVertical.Bottom));
                Assert.That(fill.sprite, Is.Not.Null,
                    "A filled Image with no sprite draws a full quad and ignores its fill amount.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void PlayProgress_WhenSceneIsBuilt_SitsInTheFrameOpeningsBelowTheDeck()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(GameplayScenePath);
            try
            {
                var deck = (RectTransform)Find(scene, "DeckLayer").transform;
                var bar = (RectTransform)Find(scene, "PlayBar").transform;
                var sectionIndex = (RectTransform)Find(scene, "SectionIndex").transform;
                var sectionName = (RectTransform)Find(scene, "SectionName").transform;

                AssertRect(deck, new Vector2(28f, 302f) * GearScale, new Vector2(674f, 84f) * GearScale);
                AssertRect(bar, new Vector2(28f, 252f) * GearScale, new Vector2(676f, 48f) * GearScale);
                AssertRect(sectionIndex, new Vector2(51f, 105f) * GearScale, new Vector2(108f, 110f) * GearScale);

                Assert.That(bar.anchoredPosition.y + bar.rect.height, Is.LessThanOrEqualTo(deck.anchoredPosition.y),
                    "The play bar and the deck must not share space.");
                Assert.That(sectionIndex.anchoredPosition.y + sectionIndex.rect.height,
                    Is.LessThanOrEqualTo(bar.anchoredPosition.y),
                    "The section number belongs under the play bar, not across it.");
                Assert.That(sectionName.anchoredPosition.x,
                    Is.GreaterThanOrEqualTo(sectionIndex.anchoredPosition.x + sectionIndex.rect.width),
                    "The section name belongs beside the number, not over it.");

                Assert.That(Find(scene, "SectionBadge"), Is.Null,
                    "The frame already draws the section slots; a painted badge doubles them up.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void AssertRect(RectTransform rect, Vector2 expectedPosition, Vector2 expectedSize)
        {
            Assert.That(rect.anchoredPosition.x, Is.EqualTo(expectedPosition.x).Within(0.01f));
            Assert.That(rect.anchoredPosition.y, Is.EqualTo(expectedPosition.y).Within(0.01f));
            Assert.That(rect.sizeDelta.x, Is.EqualTo(expectedSize.x).Within(0.01f));
            Assert.That(rect.sizeDelta.y, Is.EqualTo(expectedSize.y).Within(0.01f));
        }

        private static GameObject Find(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform match = Find(root.transform, name);
                if (match != null) return match.gameObject;
            }

            return null;
        }

        private static MonoBehaviour FindBehaviour(GameObject gameObject, string typeName)
        {
            foreach (MonoBehaviour behaviour in gameObject.GetComponents<MonoBehaviour>())
                if (behaviour != null && behaviour.GetType().Name == typeName)
                    return behaviour;

            return null;
        }

        private static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int index = 0; index < root.childCount; index++)
            {
                Transform match = Find(root.GetChild(index), name);
                if (match != null) return match;
            }

            return null;
        }
    }
}
