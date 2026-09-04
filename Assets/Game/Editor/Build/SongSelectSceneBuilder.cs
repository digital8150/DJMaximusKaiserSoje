using DJMaximusKaiserSoje.Core;
using DJMaximusKaiserSoje.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Editor
{
    internal static class SongSelectSceneBuilder
    {
        public const string SceneName = "SongSelect";

        private const float PanelWidth = 700f;
        private const float PanelHeight = 856f;
        private const float ListWidth = 1080f;

        public static string Build()
        {
            var (scene, root) = SceneScaffold.Create(SceneName);

            // Built after the scene swap so the temporary hierarchy never dirties a saved scene.
            var rowPrefab = SongRowPrefabBuilder.Build();

            SceneScaffold.Backdrop(root, "Bg-SongSelect", 0.6f);

            var screen = Ui.Node("SongSelectScreen", root).Stretch();
            var view = screen.gameObject.AddComponent<SongSelectScreenView>();

            BuildTopBar(screen, view);
            BuildDetailPanel(screen, view);
            BuildList(screen, view, rowPrefab);
            BuildFooter(screen, view);

            view.rowPrefab = rowPrefab;
            return SceneScaffold.Save(scene, SceneName);
        }

        private static void BuildTopBar(RectTransform screen, SongSelectScreenView view)
        {
            var bar = Ui.Node("TopBar", screen).Band(Anchor.TopLeft, 48f, -28f, 72f);

            Ui.Text("Heading", bar, "SELECT MUSIC", 40f, Weight.Black, UiPalette.TextPrimary)
                .Set(Anchor.MiddleLeft, 8f, 0f, 600f, 52f);

            var tabRow = Ui.Node("StyleTabs", bar).Set(Anchor.MiddleRight, -8f, 0f, 428f, 52f);
            var tabs = new TabButtonView[4];
            for (int index = 0; index < tabs.Length; index++)
            {
                var tab = UiWidgets.Tab(tabRow, UiNaming.StyleShortName((PlayStyle)index), 104f, 52f);
                tab.Set(Anchor.MiddleLeft, index * 108f, 0f, 104f, 52f);
                tabs[index] = tab;
            }

            view.styleTabs = tabs;
        }

        private static void BuildDetailPanel(RectTransform screen, SongSelectScreenView view)
        {
            var panel = Ui.CutPanel("DetailPanel", screen, UiPalette.Panel)
                .Set(Anchor.TopLeft, 48f, -124f, PanelWidth, PanelHeight);
            var body = panel.transform;

            var glow = Ui.Image("JacketGlow", body, Ui.Chrome("Glow"), UiPalette.Cyan.WithAlpha(0.3f))
                .Set(Anchor.TopCentre, 0f, 16f, 380f, 380f);
            var jacket = UiWidgets.Jacket(body, "Jacket", UiPalette.Cyan.WithAlpha(0.8f), 8f)
                .Set(Anchor.TopCentre, 0f, -24f, 300f, 300f);

            var category = Ui.Text("Category", body, string.Empty, 20f, Weight.Bold, UiPalette.Magenta)
                .Set(Anchor.TopLeft, 36f, -336f, PanelWidth - 72f, 26f);
            var title = Ui.Text("Title", body, string.Empty, 34f, Weight.ExtraBold, UiPalette.TextPrimary)
                .Set(Anchor.TopLeft, 36f, -362f, PanelWidth - 72f, 46f);
            var artist = Ui.Text("Artist", body, string.Empty, 20f, Weight.Regular, UiPalette.TextSecondary)
                .Set(Anchor.TopLeft, 38f, -410f, PanelWidth - 76f, 28f);
            var bpm = Ui.Text("Bpm", body, string.Empty, 18f, Weight.Medium, UiPalette.TextMuted)
                .Set(Anchor.TopLeft, 38f, -438f, 300f, 26f);

            var chipRow = Ui.Node("TierChips", body).Set(Anchor.TopCentre, 0f, -474f, 480f, 64f);
            var chips = new DifficultyChipView[5];
            for (int index = 0; index < chips.Length; index++)
            {
                var chip = UiWidgets.Chip(chipRow, (DifficultyTier)index, 88f, 64f);
                chip.Set(Anchor.MiddleLeft, index * 98f, 0f, 88f, 64f);
                chips[index] = chip;
            }

            var difficultyName = Ui.Text("DifficultyName", body, string.Empty, 24f, Weight.Bold, UiPalette.TextSecondary)
                .Set(Anchor.TopLeft, 36f, -552f, 420f, 32f);
            var level = Ui.Text("Level", body, "–", 40f, Weight.Black, UiPalette.Cyan, TextAlignmentOptions.Right)
                .Set(Anchor.TopRight, -36f, -548f, 200f, 46f);
            var noteCount = Ui.Text("NoteCount", body, string.Empty, 18f, Weight.Medium, UiPalette.TextMuted)
                .Set(Anchor.TopLeft, 36f, -588f, 420f, 26f);

            BuildSpeedRow(body, view);

            var play = Ui.Button("PlayButton", body, Ui.Chrome("PanelCut"), UiPalette.Cyan.WithAlpha(0.9f))
                .Set(Anchor.TopCentre, 0f, -690f, PanelWidth - 80f, 72f);
            Ui.Text("Label", play.transform, "PLAY", 34f, Weight.Black, UiPalette.Ink, TextAlignmentOptions.Center)
                .Stretch();

            view.jacket = jacket;
            view.categoryLabel = category;
            view.titleLabel = title;
            view.artistLabel = artist;
            view.bpmLabel = bpm;
            view.tierChips = chips;
            view.difficultyNameLabel = difficultyName;
            view.levelLabel = level;
            view.noteCountLabel = noteCount;
            view.playButton = play;
            view.recordPanel = BuildRecordPanel(body);
        }

        private static void BuildSpeedRow(Transform body, SongSelectScreenView view)
        {
            var row = Ui.Node("SpeedRow", body).Set(Anchor.TopLeft, 36f, -628f, PanelWidth - 72f, 48f);
            Ui.Text("Caption", row, "NOTE SPEED", 20f, Weight.Medium, UiPalette.TextMuted)
                .Set(Anchor.MiddleLeft, 0f, 0f, 260f, 28f);

            var down = Ui.Button("SpeedDown", row, Ui.Chrome("Bar"), UiPalette.PanelRaised)
                .Set(Anchor.MiddleRight, -132f, 0f, 44f, 44f);
            Ui.Text("Label", down.transform, "−", 30f, Weight.Bold, UiPalette.TextPrimary, TextAlignmentOptions.Center)
                .Stretch();

            var value = Ui.Text("SpeedValue", row, "5.0", 30f, Weight.Black, UiPalette.Cyan, TextAlignmentOptions.Center)
                .Set(Anchor.MiddleRight, -66f, 0f, 76f, 40f);

            var up = Ui.Button("SpeedUp", row, Ui.Chrome("Bar"), UiPalette.PanelRaised)
                .Set(Anchor.MiddleRight, 0f, 0f, 44f, 44f);
            Ui.Text("Label", up.transform, "+", 30f, Weight.Bold, UiPalette.TextPrimary, TextAlignmentOptions.Center)
                .Stretch();

            view.speedLabel = value;
            view.speedDownButton = down;
            view.speedUpButton = up;
        }

        private static RecordPanelView BuildRecordPanel(Transform body)
        {
            var panel = Ui.Image("RecordPanel", body, Ui.Chrome("Panel"), UiPalette.Night.WithAlpha(0.75f))
                .Set(Anchor.TopCentre, 0f, -778f, PanelWidth - 80f, 62f);
            var view = panel.gameObject.AddComponent<RecordPanelView>();

            string[] captions = { "BEST SCORE", "JUDGE", "COMBO", "RANK" };
            var values = new TMP_Text[captions.Length];
            float cell = (PanelWidth - 80f) / captions.Length;

            for (int index = 0; index < captions.Length; index++)
            {
                var cellRoot = Ui.Node(captions[index], panel.transform)
                    .Set(Anchor.MiddleLeft, index * cell, 0f, cell, 58f);
                Ui.Text("Caption", cellRoot, captions[index], 14f, Weight.Medium, UiPalette.TextMuted,
                    TextAlignmentOptions.Center).Set(Anchor.TopCentre, 0f, -8f, cell, 18f);
                values[index] = Ui.Text("Value", cellRoot, "—", 24f, Weight.Bold, UiPalette.TextPrimary,
                    TextAlignmentOptions.Center).Set(Anchor.TopCentre, 0f, -26f, cell, 30f);
            }

            view.scoreLabel = values[0];
            view.accuracyLabel = values[1];
            view.comboLabel = values[2];
            view.rankLabel = values[3];
            return view;
        }

        private static void BuildList(RectTransform screen, SongSelectScreenView view, SongRowView rowPrefab)
        {
            var frame = Ui.CutPanel("ListPanel", screen, UiPalette.Ink.WithAlpha(0.55f))
                .Set(Anchor.TopRight, -48f, -124f, ListWidth, PanelHeight);

            var scrollRoot = Ui.Node("Scroll", frame.transform).Stretch(10f, 10f, 10f, 10f);
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            var viewport = Ui.Image("Viewport", scrollRoot, null, Color.white.WithAlpha(0.004f)).Stretch();
            viewport.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = Ui.Node("Content", viewport.transform);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, content.offsetMin.y);
            content.offsetMax = new Vector2(0f, 0f);
            content.sizeDelta = new Vector2(0f, 0f);

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = content;

            var empty = Ui.Text("EmptyLibrary", frame.transform,
                "재생할 수 있는 곡이 없어요. 곡을 내려받은 뒤 다시 열어 주세요.",
                24f, Weight.Medium, UiPalette.TextSecondary, TextAlignmentOptions.Center).Stretch(40f, 40f, 40f, 40f);
            empty.textWrappingMode = TextWrappingModes.Normal;
            empty.gameObject.SetActive(false);

            view.listScroll = scroll;
            view.listContent = content;
            view.emptyLibraryLabel = empty;
        }

        private static void BuildFooter(RectTransform screen, SongSelectScreenView view)
        {
            var footer = Ui.Image("Footer", screen, Ui.Chrome("PanelCut"), UiPalette.Ink.WithAlpha(0.8f))
                .Band(Anchor.BottomLeft, 48f, 26f, 52f);
            view.keyGuideLabel = Ui.Text("KeyGuide", footer.transform, string.Empty, 20f, Weight.Medium,
                UiPalette.TextSecondary, TextAlignmentOptions.MidlineLeft).Stretch(24f, 0f, 220f, 0f);

            var options = Ui.Button("OptionsButton", footer.transform, Ui.Chrome("Bar"), UiPalette.PanelRaised)
                .Set(Anchor.MiddleRight, -12f, 0f, 178f, 42f);
            Ui.Text("Label", options.transform, "설정   O", 20f, Weight.Bold, UiPalette.TextPrimary,
                TextAlignmentOptions.Center).Stretch();
            view.optionsButton = options;
        }
    }
}
