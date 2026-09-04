using System;
using System.Linq;
using DJMaximusKaiserSoje.Presentation;
using TMPro;
using UnityEngine;

namespace DJMaximusKaiserSoje.Editor
{
    /// <summary>
    /// The scene the game starts in: it holds the composition root and shows the player why the
    /// first moments are not instant.
    /// </summary>
    internal static class BootSceneBuilder
    {
        public const string SceneName = "Boot";
        private const string BootstrapTypeName = "DJMaximusKaiserSoje.App.GameBootstrap";

        public static string Build()
        {
            var (scene, root) = SceneScaffold.Create(SceneName);

            Ui.Image("Background", root, null, UiPalette.Ink).Stretch();

            var label = Ui.Text("Status", root, "곡 데이터를 준비하고 있어요.", 30f, Weight.Medium,
                UiPalette.TextSecondary, TextAlignmentOptions.Center).Set(Anchor.Centre, 0f, -40f, 1200f, 44f);

            var mark = Ui.Text("Mark", root, "DJ MAXIMUS", 64f, Weight.Black, UiPalette.TextPrimary,
                TextAlignmentOptions.Center).Set(Anchor.Centre, 0f, 60f, 1200f, 80f);
            mark.characterSpacing = 4f;

            var pulse = label.gameObject.AddComponent<PulsingGraphic>();
            pulse.target = label;
            pulse.minimumAlpha = 0.4f;
            pulse.cyclesPerSecond = 0.8f;

            var bootstrapType = FindBootstrapType();
            var bootstrap = new GameObject("GameBootstrap");
            if (bootstrapType != null)
                bootstrap.AddComponent(bootstrapType);
            else
                Debug.LogWarning($"{BootstrapTypeName} was not found; the boot scene has a placeholder object only.");

            return SceneScaffold.Save(scene, SceneName);
        }

        private static Type FindBootstrapType() =>
            AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(BootstrapTypeName, false))
                .FirstOrDefault(type => type != null && typeof(MonoBehaviour).IsAssignableFrom(type));
    }
}
