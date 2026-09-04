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
