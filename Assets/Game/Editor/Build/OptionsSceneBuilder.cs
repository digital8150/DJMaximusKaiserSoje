using DJMaximusKaiserSoje.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Editor
{
    internal static class OptionsSceneBuilder
    {
        public const string SceneName = "Options";

        public static string Build()
        {
            var (scene, root) = SceneScaffold.Create(SceneName);
            SceneScaffold.Backdrop(root, "Bg-SongSelect", 0.82f);

            var screen = Ui.Node("OptionsScreen", root).Stretch();
            var view = screen.gameObject.AddComponent<OptionsScreenView>();

            Ui.Text("Eyebrow", screen, "OPTION", 20f, Weight.Bold, UiPalette.Cyan)
                .Set(Anchor.TopLeft, 84f, -54f, 300f, 28f);
            Ui.Text("Title", screen, "설정", 66f, Weight.Black, UiPalette.TextPrimary)
                .Set(Anchor.TopLeft, 82f, -78f, 700f, 82f);
            Ui.Text("Guide", screen, "O 옵션   Esc 돌아가기", 20f, Weight.Medium, UiPalette.TextMuted,
                TextAlignmentOptions.Right).Set(Anchor.TopRight, -80f, -76f, 500f, 32f);

            var list = Ui.Panel("Settings", screen, UiPalette.Panel.WithAlpha(0.94f))
                .Set(Anchor.TopLeft, 82f, -184f, 1090f, 808f);
            var detail = Ui.Panel("Description", screen, UiPalette.PanelSoft.WithAlpha(0.9f))
                .Set(Anchor.TopRight, -82f, -184f, 610f, 808f);

            BuildAudio(list.transform, view);
            BuildInput(list.transform, view);
            BuildGraphics(list.transform, view);
            BuildDescription(detail.transform, view);

            var back = Ui.Button("BackButton", screen, Ui.Chrome("BarCut"), UiPalette.PanelRaised)
                .Set(Anchor.BottomRight, -82f, 40f, 250f, 62f);
            Ui.Text("Label", back.transform, "돌아가기", 24f, Weight.Bold, UiPalette.TextPrimary,
                TextAlignmentOptions.Center).Stretch();
            view.backButton = back;

            return SceneScaffold.Save(scene, SceneName);
        }

        private static void BuildAudio(Transform parent, OptionsScreenView view)
        {
            Section(parent, "오디오", -28f);
            SettingRow(parent, "AudioBuffer", "오디오 버퍼 크기", -70f,
                out view.audioBufferLabel, out view.audioBufferDownButton, out view.audioBufferUpButton);
            SettingRow(parent, "JudgementOffset", "판정 오프셋", -128f,
                out view.judgementOffsetLabel, out view.judgementOffsetDownButton, out view.judgementOffsetUpButton);
        }

        private static void BuildInput(Transform parent, OptionsScreenView view)
        {
            Section(parent, "키 설정", -204f);
            var tabs = Ui.Node("ModeTabs", parent).Set(Anchor.TopLeft, 34f, -244f, 760f, 48f);
            view.styleTabs = new TabButtonView[4];
            string[] names = { "4K", "4K+2", "6K", "6K+2" };
            for (int index = 0; index < names.Length; index++)
                view.styleTabs[index] = UiWidgets.Tab(tabs, names[index], 174f, 46f)
                    .Set(Anchor.MiddleLeft, index * 188f, 0f, 174f, 46f);

            view.keyButtons = new Button[8];
            view.keyRoleLabels = new TMP_Text[8];
            view.keyLabels = new TMP_Text[8];
            var keys = Ui.Node("Keys", parent).Set(Anchor.TopLeft, 34f, -306f, 1018f, 164f);
            for (int index = 0; index < view.keyButtons.Length; index++)
            {
                var button = Ui.Button("Key" + index, keys, Ui.Chrome("PanelCut"), UiPalette.PanelRaised)
                    .Set(Anchor.TopLeft, 0f, 0f, 230f, 48f);
                var role = Ui.Text("Role", button.transform, string.Empty, 13f, Weight.Medium, UiPalette.TextMuted)
                    .Set(Anchor.MiddleLeft, 16f, 0f, 72f, 20f);
                var label = Ui.Text("Key", button.transform, string.Empty, 19f, Weight.Bold, UiPalette.Cyan,
                    TextAlignmentOptions.Right).Set(Anchor.MiddleRight, -16f, 0f, 142f, 24f);
                view.keyButtons[index] = button;
                view.keyRoleLabels[index] = role;
                view.keyLabels[index] = label;
            }

            var reset = Ui.Button("ResetKeys", parent, Ui.Chrome("BarCut"), UiPalette.PanelSoft)
                .Set(Anchor.TopRight, -34f, -478f, 230f, 44f);
            Ui.Text("Label", reset.transform, "기본 키로 되돌리기", 17f, Weight.Bold, UiPalette.TextSecondary,
                TextAlignmentOptions.Center).Stretch();
            view.resetKeysButton = reset;
        }

        private static void BuildGraphics(Transform parent, OptionsScreenView view)
        {
            Section(parent, "그래픽", -534f);
            SettingRow(parent, "Quality", "그래픽 품질", -574f,
                out view.qualityLabel, out view.qualityDownButton, out view.qualityUpButton);

            SettingButtonRow(parent, "DisplayMode", "화면 모드", -628f,
                out view.displayModeLabel, out view.displayModeButton);

            SettingRow(parent, "Resolution", "해상도", -682f,
                out view.resolutionLabel, out view.resolutionDownButton, out view.resolutionUpButton);

            SettingButtonRow(parent, "VSync", "수직 동기화", -736f,
                out view.vSyncLabel, out view.vSyncButton);
        }

        private static void BuildDescription(Transform parent, OptionsScreenView view)
        {
            Ui.Text("Caption", parent, "설정 안내", 28f, Weight.ExtraBold, UiPalette.TextPrimary)
                .Set(Anchor.TopLeft, 36f, -40f, 530f, 38f);
            Ui.Image("Rule", parent, Ui.Chrome("Bar"), UiPalette.Cyan.WithAlpha(0.8f))
                .Set(Anchor.TopLeft, 36f, -92f, 120f, 4f);
            var description = Ui.Text("Description", parent, string.Empty, 21f, Weight.Regular,
                UiPalette.TextSecondary).Set(Anchor.TopLeft, 36f, -126f, 530f, 210f);
            description.textWrappingMode = TextWrappingModes.Normal;
            description.overflowMode = TextOverflowModes.Truncate;
            view.descriptionLabel = description;

            Ui.Text("TipCaption", parent, "키 설정", 18f, Weight.Bold, UiPalette.Magenta)
                .Set(Anchor.TopLeft, 36f, -382f, 530f, 28f);
            var status = Ui.Text("Status", parent, string.Empty, 23f, Weight.Bold, UiPalette.TextPrimary)
                .Set(Anchor.TopLeft, 36f, -422f, 530f, 96f);
            status.textWrappingMode = TextWrappingModes.Normal;
            view.statusLabel = status;

            Ui.Text("Footer", parent, "변경한 설정은 바로 저장됩니다.", 18f, Weight.Medium, UiPalette.TextMuted)
                .Set(Anchor.BottomLeft, 36f, 34f, 530f, 30f);
        }

        private static void Section(Transform parent, string title, float y)
        {
            Ui.Text(title, parent, title, 25f, Weight.ExtraBold, UiPalette.TextPrimary)
                .Set(Anchor.TopLeft, 34f, y, 400f, 34f);
            Ui.Image(title + "Rule", parent, Ui.Chrome("Bar"), UiPalette.Divider)
                .Set(Anchor.TopRight, -34f, y - 34f, 1018f, 2f);
        }

        private static void SettingRow(Transform parent, string name, string caption, float y,
            out TMP_Text value, out Button down, out Button up)
        {
            var row = Ui.Image(name, parent, Ui.Chrome("BarCut"), UiPalette.PanelSoft)
                .Set(Anchor.TopLeft, 34f, y, 1018f, 52f);
            Ui.Text("Caption", row.transform, caption, 19f, Weight.Bold, UiPalette.TextSecondary)
                .Set(Anchor.MiddleLeft, 20f, 0f, 350f, 28f);
            down = SmallButton("Down", row.transform, "‹", -302f);
            value = Ui.Text("Value", row.transform, string.Empty, 20f, Weight.Bold, UiPalette.TextPrimary,
                TextAlignmentOptions.Center).Set(Anchor.MiddleRight, -76f, 0f, 260f, 28f);
            up = SmallButton("Up", row.transform, "›", -16f);
        }

        private static void SettingButtonRow(Transform parent, string name, string caption, float y,
            out TMP_Text value, out Button button)
        {
            var row = Ui.Image(name, parent, Ui.Chrome("BarCut"), UiPalette.PanelSoft)
                .Set(Anchor.TopLeft, 34f, y, 1018f, 52f);
            Ui.Text("Caption", row.transform, caption, 19f, Weight.Bold, UiPalette.TextSecondary)
                .Set(Anchor.MiddleLeft, 20f, 0f, 350f, 28f);
            button = Ui.Button("ValueButton", row.transform, Ui.Chrome("PanelCut"), UiPalette.PanelRaised)
                .Set(Anchor.MiddleRight, -16f, 0f, 336f, 38f);
            value = Ui.Text("Value", button.transform, string.Empty, 19f, Weight.Bold, UiPalette.Cyan,
                TextAlignmentOptions.Center).Stretch();
        }

        private static Button SmallButton(string name, Transform parent, string label, float x)
        {
            var button = Ui.Button(name, parent, Ui.Chrome("PanelCut"), UiPalette.PanelRaised)
                .Set(Anchor.MiddleRight, x, 0f, 54f, 38f);
            Ui.Text("Label", button.transform, label, 30f, Weight.Bold, UiPalette.Cyan,
                TextAlignmentOptions.Center).Stretch();
            return button;
        }
    }
}
