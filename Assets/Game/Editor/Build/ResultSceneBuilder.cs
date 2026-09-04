using DJMaximusKaiserSoje.Core;
using DJMaximusKaiserSoje.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Editor
{
    internal static class ResultSceneBuilder
    {
        public const string SceneName = "Result";

        private const float CentreX = 560f;
        private const float CentreWidth = 780f;
        private const float RowHeight = 92f;

        public static string Build()
        {
            var (scene, root) = SceneScaffold.Create(SceneName);
            SceneScaffold.Backdrop(root, "Bg-Result", 0.5f);

            var screen = Ui.Node("ResultScreen", root).Stretch();
            var view = screen.gameObject.AddComponent<ResultScreenView>();

            var songCard = UiWidgets.SongCard(screen, "SongCard", 440f, 400f);
            songCard.Set(Anchor.TopLeft, 60f, -110f, 440f, 820f);
            view.songCard = songCard;

            Ui.Image("HeaderPlate", screen, Ui.Chrome("PanelCut"), UiPalette.Violet.WithAlpha(0.75f))
                .Set(Anchor.TopLeft, CentreX, -96f, CentreWidth, 58f);
            Ui.Text("Header", screen, "PLAY RESULT", 32f, Weight.Black, UiPalette.TextPrimary)
                .Set(Anchor.TopLeft, CentreX + 26f, -108f, 500f, 40f);

            view.ratingRow = BuildRow(screen, "RatingRow", -164f, "RATING");
            view.accuracyRow = BuildRow(screen, "AccuracyRow", -262f, "JUDGE");
            view.scoreRow = BuildRow(screen, "ScoreRow", -360f, "SCORE");

            BuildBreakdown(screen, view);
            BuildRankColumn(screen, view);
            BuildFooter(screen, view);

            return SceneScaffold.Save(scene, SceneName);
        }

        private static ResultRowView BuildRow(RectTransform screen, string name, float y, string caption)
        {
            var bar = Ui.Image(name, screen, Ui.Chrome("BarCut"), UiPalette.PanelRaised)
                .Set(Anchor.TopLeft, CentreX, y, CentreWidth, RowHeight);
            var view = bar.gameObject.AddComponent<ResultRowView>();

            var captionLabel = Ui.Text("Caption", bar.transform, caption, 28f, Weight.ExtraBold, UiPalette.TextSecondary)
                .Set(Anchor.MiddleLeft, 30f, 0f, 320f, 36f);
            var value = Ui.Text("Value", bar.transform, "0", 52f, Weight.Black, UiPalette.TextPrimary,
                TextAlignmentOptions.Right).Set(Anchor.MiddleRight, -32f, -4f, 460f, 60f);

            var ribbon = Ui.Image("RecordRibbon", bar.transform, Ui.Chrome("BarCut"), UiPalette.Rose)
                .Set(Anchor.TopRight, -18f, 8f, 176f, 28f);
            var ribbonLabel = Ui.Text("Label", ribbon.transform, "NEW RECORD", 16f, Weight.Black,
                UiPalette.TextPrimary, TextAlignmentOptions.Center).Stretch();
            ribbon.gameObject.SetActive(false);

            var delta = Ui.Text("Delta", bar.transform, string.Empty, 20f, Weight.Bold, UiPalette.Mint,
                TextAlignmentOptions.Right).Set(Anchor.BottomRight, -32f, 6f, 300f, 24f);
            delta.gameObject.SetActive(false);

            var ticker = bar.gameObject.AddComponent<ValueTicker>();
            ticker.label = value;
            ticker.duration = 0.75f;

            var punch = bar.gameObject.AddComponent<ScalePunch>();
            punch.target = (RectTransform)bar.transform;
            punch.peakScale = 1.04f;

            view.captionLabel = captionLabel;
            view.valueLabel = value;
            view.deltaLabel = delta;
            view.recordRibbon = ribbon.gameObject;
            view.recordRibbonLabel = ribbonLabel;
            view.bar = bar;
            view.ticker = ticker;
            view.punch = punch;
            return view;
        }

        private static void BuildBreakdown(RectTransform screen, ResultScreenView view)
        {
            var panel = Ui.CutPanel("Breakdown", screen, UiPalette.Panel)
                .Set(Anchor.TopLeft, CentreX, -474f, CentreWidth, 440f);
            var body = panel.transform;
            float inner = CentreWidth - 64f;

            Ui.Text("TotalCaption", body, "TOTAL NOTES", 22f, Weight.Bold, UiPalette.TextSecondary)
                .Set(Anchor.TopLeft, 32f, -24f, 320f, 30f);
            var totalNotes = Ui.Text("TotalValue", body, "0", 26f, Weight.Black, UiPalette.TextPrimary,
                TextAlignmentOptions.Right).Set(Anchor.TopRight, -32f, -24f, 300f, 30f);

            Ui.Image("Rule", body, Ui.Chrome("Bar"), UiPalette.Divider)
                .Set(Anchor.TopCentre, 0f, -62f, inner, 2f);

            var captions = new TMP_Text[5];
            var counts = new TMP_Text[5];
            for (int index = 0; index < 5; index++)
            {
                float y = -78f - index * 42f;
                var grade = (JudgementGrade)index;
                captions[index] = Ui.Text("Caption" + index, body, UiNaming.TallyLabel(grade), 22f, Weight.Bold,
                    UiPalette.GradeColor(grade)).Set(Anchor.TopLeft, 32f, y, 340f, 30f);
                counts[index] = Ui.Text("Count" + index, body, "0000", 24f, Weight.Bold, UiPalette.TextPrimary,
                    TextAlignmentOptions.Right).Set(Anchor.TopRight, -32f, y, 240f, 30f);
            }

            Ui.Image("Rule2", body, Ui.Chrome("Bar"), UiPalette.Divider)
                .Set(Anchor.TopCentre, 0f, -296f, inner, 2f);

            Ui.Text("ComboCaption", body, "MAX COMBO", 22f, Weight.Bold, UiPalette.TextSecondary)
                .Set(Anchor.TopLeft, 32f, -312f, 340f, 30f);
            var maxCombo = Ui.Text("ComboValue", body, "0", 30f, Weight.Black, UiPalette.Cyan,
                TextAlignmentOptions.Right).Set(Anchor.TopRight, -32f, -312f, 240f, 34f);

            var fastSlow = BuildFastSlow(body, inner);

            view.totalNotesLabel = totalNotes;
            view.gradeCaptionLabels = captions;
            view.gradeCountLabels = counts;
            view.maxComboLabel = maxCombo;
            view.fastSlowBar = fastSlow;
        }

        private static FastSlowBarView BuildFastSlow(Transform body, float width)
        {
            var holder = Ui.Node("FastSlow", body).Set(Anchor.TopCentre, 0f, -364f, width, 48f);
            var view = holder.gameObject.AddComponent<FastSlowBarView>();

            var fastLabel = Ui.Text("FastLabel", holder, "FAST", 18f, Weight.Bold, UiPalette.Cyan)
                .Set(Anchor.TopLeft, 0f, 0f, 200f, 24f);
            var slowLabel = Ui.Text("SlowLabel", holder, "SLOW", 18f, Weight.Bold, UiPalette.Rose,
                TextAlignmentOptions.Right).Set(Anchor.TopRight, 0f, 0f, 200f, 24f);

            var track = Ui.Image("Track", holder, Ui.Chrome("Bar"), UiPalette.Night)
                .Set(Anchor.BottomCentre, 0f, 0f, width, 14f);
            // Authored as an even split; the view re-anchors them once it has real counts.
            var fast = Ui.Image("FastFill", track.transform, Ui.Chrome("Bar"), UiPalette.Cyan);
            Split((RectTransform)fast.transform, 0f, 0.5f);
            var slow = Ui.Image("SlowFill", track.transform, Ui.Chrome("Bar"), UiPalette.Rose);
            Split((RectTransform)slow.transform, 0.5f, 1f);

            view.fastLabel = fastLabel;
            view.slowLabel = slowLabel;
            view.fastImage = fast;
            view.slowImage = slow;
            view.fastFill = (RectTransform)fast.transform;
            view.slowFill = (RectTransform)slow.transform;
            return view;
        }

        private static void Split(RectTransform rect, float from, float to)
        {
            rect.anchorMin = new Vector2(from, 0f);
            rect.anchorMax = new Vector2(to, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void BuildRankColumn(RectTransform screen, ResultScreenView view)
        {
            var column = Ui.Node("RankColumn", screen).Set(Anchor.TopRight, -60f, -110f, 460f, 860f);

            // Built first so she sits behind the badge and the plates rather than over them.
            var mascot = Ui.Image("Mascot", column, Ui.Art("Mascot-Cheer"), Color.white)
                .Set(Anchor.BottomCentre, 30f, 150f, 380f, 360f);
            mascot.preserveAspect = true;
            mascot.type = Image.Type.Simple;

            var badgeRoot = Ui.Node("RankBadge", column).Set(Anchor.TopCentre, 0f, 0f, 360f, 360f);
            var badge = badgeRoot.gameObject.AddComponent<RankBadgeView>();

            var glow = Ui.Image("Glow", badgeRoot, Ui.Chrome("Glow"), UiPalette.Amber.WithAlpha(0.35f))
                .Stretch(-50f, -50f, -50f, -50f);
            var medallion = Ui.Image("Medallion", badgeRoot, Ui.Art("RankMedallion"), Color.white).Stretch();
            medallion.preserveAspect = true;
            medallion.type = Image.Type.Simple;
            var rank = Ui.Text("Rank", badgeRoot, "S", 132f, Weight.Black, UiPalette.Amber,
                TextAlignmentOptions.Center).Set(Anchor.Centre, 0f, 6f, 300f, 170f);

            // Below the mascot rather than across her, and clear of the player plate at the bottom.
            var captionPlate = Ui.Image("CaptionPlate", column, Ui.Chrome("BarCut"), UiPalette.Cyan.WithAlpha(0.2f))
                .Set(Anchor.BottomCentre, 0f, 92f, 380f, 58f);
            var caption = Ui.Text("Caption", captionPlate.transform, "CLEAR", 30f, Weight.Black, UiPalette.Cyan,
                TextAlignmentOptions.Center).Stretch();

            var punch = badgeRoot.gameObject.AddComponent<ScalePunch>();
            punch.target = badgeRoot;
            punch.peakScale = 1.16f;

            badge.medallion = medallion;
            badge.glow = glow;
            badge.rankLabel = rank;
            badge.captionPlate = captionPlate;
            badge.captionLabel = caption;
            badge.punch = punch;
            view.rankBadge = badge;

            var playerPlate = Ui.Image("PlayerPlate", column, Ui.Chrome("Panel"), UiPalette.Night.WithAlpha(0.8f))
                .Set(Anchor.BottomCentre, 0f, 0f, 420f, 76f);
            var name = Ui.Text("Name", playerPlate.transform, string.Empty, 22f, Weight.Bold, UiPalette.TextPrimary)
                .Set(Anchor.TopLeft, 20f, -10f, 240f, 28f);
            var levelPlate = Ui.Image("LevelPlate", playerPlate.transform, Ui.Chrome("Bar"), UiPalette.Magenta.WithAlpha(0.28f))
                .Set(Anchor.TopRight, -20f, -8f, 72f, 32f);
            var level = Ui.Text("Level", levelPlate.transform, "01", 22f, Weight.Black, UiPalette.Magenta,
                TextAlignmentOptions.Center).Stretch();
            var expTrack = Ui.Image("ExpTrack", playerPlate.transform, Ui.Chrome("Bar"), UiPalette.Ink)
                .Set(Anchor.BottomCentre, 0f, 14f, 380f, 12f);
            // A filled Image ignores the 9-slice and stretches the whole sprite, so the pill would be
            // drawn as one wide ellipse. The plain rectangle is the shape that survives being filled.
            var expFill = Ui.Image("ExpFill", expTrack.transform, Ui.Chrome("Solid"), UiPalette.Magenta).Stretch();
            expFill.type = Image.Type.Filled;
            expFill.fillMethod = Image.FillMethod.Horizontal;
            expFill.fillAmount = 0f;

            view.playerNameLabel = name;
            view.playerLevelLabel = level;
            view.playerExpFill = expFill;
        }

        private static void BuildFooter(RectTransform screen, ResultScreenView view)
        {
            var footer = Ui.Image("Footer", screen, Ui.Chrome("PanelCut"), UiPalette.Ink.WithAlpha(0.82f))
                .Band(Anchor.BottomLeft, 48f, 26f, 52f);
            view.keyGuideLabel = Ui.Text("KeyGuide", footer.transform, string.Empty, 20f, Weight.Medium,
                UiPalette.TextSecondary, TextAlignmentOptions.Center).Stretch(24f, 0f, 24f, 0f);
        }
    }
}
