using DJMaximusKaiserSoje.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Editor
{
    internal static class TitleSceneBuilder
    {
        public const string SceneName = "Title";

        public static string Build()
        {
            var (scene, root) = SceneScaffold.Create(SceneName);
            SceneScaffold.Backdrop(root, "Bg-Title", 0.45f);

            var screen = Ui.Node("TitleScreen", root).Stretch();
            var view = screen.gameObject.AddComponent<TitleScreenView>();

            var mascot = Ui.Image("Mascot", screen, Ui.Art("Mascot-Title"), Color.white)
                .Set(Anchor.MiddleRight, -120f, -18f, 700f, 1044f);
            mascot.preserveAspect = true;
            mascot.type = Image.Type.Simple;

            var emblem = Ui.Image("Emblem", screen, Ui.Art("TitleEmblem"), Color.white)
                .Set(Anchor.MiddleLeft, 150f, 250f, 150f, 150f);
            emblem.preserveAspect = true;
            emblem.type = Image.Type.Simple;

            var title = Ui.Text("Title", screen, "DJ MAXIMUS", 132f, Weight.Black, UiPalette.TextPrimary)
                .Set(Anchor.MiddleLeft, 146f, 110f, 1100f, 150f);
            title.characterSpacing = -2f;

            var subtitle = Ui.Text("Subtitle", screen, "KAISER SOJE", 72f, Weight.ExtraBold, UiPalette.Cyan)
                .Set(Anchor.MiddleLeft, 152f, 10f, 1100f, 90f);
            subtitle.characterSpacing = 12f;

            Ui.Image("Rule", screen, Ui.Chrome("Bar"), UiPalette.Magenta.WithAlpha(0.85f))
                .Set(Anchor.MiddleLeft, 154f, -46f, 320f, 6f);

            var prompt = Ui.Text("Prompt", screen, "아무 키나 눌러 시작", 38f, Weight.Medium, UiPalette.TextSecondary)
                .Set(Anchor.MiddleLeft, 154f, -128f, 900f, 56f);
            var pulse = prompt.gameObject.AddComponent<PulsingGraphic>();
            pulse.target = prompt;
            pulse.minimumAlpha = 0.28f;
            pulse.maximumAlpha = 1f;
            pulse.cyclesPerSecond = 0.6f;

            var build = Ui.Text("Build", screen, string.Empty, 22f, Weight.Regular, UiPalette.TextMuted,
                TextAlignmentOptions.BottomRight).Set(Anchor.BottomRight, -48f, 36f, 400f, 32f);

            var options = Ui.Button("OptionsButton", screen, Ui.Chrome("PanelCut"), UiPalette.PanelRaised)
                .Set(Anchor.TopRight, -48f, -38f, 190f, 58f);
            Ui.Text("Label", options.transform, "설정   O", 24f, Weight.Bold, UiPalette.TextPrimary,
                TextAlignmentOptions.Center).Stretch();

            var fader = Ui.Group("Fader", screen).Stretch();
            fader.interactable = false;
            fader.blocksRaycasts = false;

            view.titleLabel = title;
            view.subtitleLabel = subtitle;
            view.promptLabel = prompt;
            view.buildLabel = build;
            view.optionsButton = options;
            view.fader = fader;

            return SceneScaffold.Save(scene, SceneName);
        }
    }
}
