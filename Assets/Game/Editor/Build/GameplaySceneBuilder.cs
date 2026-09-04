using DJMaximusKaiserSoje.Core;
using DJMaximusKaiserSoje.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Editor
{
    internal static class GameplaySceneBuilder
    {
        public const string SceneName = "Gameplay";

        private const float GearHalfWidth = 390f;
        private const float GearBottom = 116f;

        /// <summary>How far the gear runs past the top of the screen, so notes fall in from outside it.</summary>
        private const float GearOverhang = 260f;

        private const float RailWidth = 46f;
        private const float DeckHeight = 140f;
        private const float LaneInset = 48f;
        private const float ColumnWidth = 430f;

        public static string Build()
        {
            var (scene, root) = SceneScaffold.Create(SceneName);

            // The camera far plane carries an optional song video. This tint keeps the lanes legible
            // while remaining a complete backdrop for songs without one.
            Ui.Image("Background", root, null, UiPalette.Ink.WithAlpha(0.58f)).Stretch();
            // Tiled at the sprite's own 64 px, not stretched, or the weave becomes giant stripes.
            var hatch = Ui.Image("BackgroundHatch", root, Ui.Chrome("Hatch"), UiPalette.Violet.WithAlpha(0.02f)).Stretch();
            hatch.type = Image.Type.Tiled;

            var screen = Ui.Node("GameplayScreen", root).Stretch();
            var view = screen.gameObject.AddComponent<GameplayScreenView>();

            view.playfield = BuildPlayfield(screen, out var feedback);
            view.feedback = feedback;

            BuildLeftColumn(screen, view);
            BuildRightColumn(screen, view);
            BuildBottom(screen, view);
            BuildPauseOverlay(screen, view);

            return SceneScaffold.Save(scene, SceneName);
        }

        /// <summary>
        /// The gear: side rails, the lane viewport, and the deck the keys sit on. The viewport runs
        /// past the top of the screen, so notes are already moving when they come into view instead
        /// of appearing inside a box.
        /// </summary>
        private static PlayfieldView BuildPlayfield(RectTransform screen, out JudgementFeedbackView feedback)
        {
            var gear = Ui.Node("Gear", screen).Column(0.5f, -GearHalfWidth, GearHalfWidth, GearBottom, GearOverhang);
            var view = gear.gameObject.AddComponent<PlayfieldView>();

            Ui.Image("GearBody", gear, null, UiPalette.Ink.WithAlpha(0.9f)).Stretch();

            var railLeft = Ui.Image("RailLeft", gear, Ui.Chrome("RailEdge"), UiPalette.Cyan.WithAlpha(0.4f))
                .Column(0f, 0f, RailWidth, 0f, 0f);
            railLeft.type = Image.Type.Simple;
            var railRight = Ui.Image("RailRight", gear, Ui.Chrome("RailEdge"), UiPalette.Cyan.WithAlpha(0.4f))
                .Column(1f, -RailWidth, 0f, 0f, 0f);
            railRight.type = Image.Type.Simple;
            // Mirrored so both rails light their inner edge.
            railRight.transform.localScale = new Vector3(-1f, 1f, 1f);

            var viewport = Ui.Image("LaneViewport", gear, null, UiPalette.Night.WithAlpha(0.55f))
                .Column(0.5f, -GearHalfWidth + LaneInset, GearHalfWidth - LaneInset, DeckHeight, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var laneLayer = Ui.Node("LaneLayer", viewport.transform).Stretch();
            var beamLayer = Ui.Node("BeamLayer", viewport.transform).Stretch();
            // FX notes span half the gear. Keep them behind regular notes at overlap points.
            var fxNoteLayer = Ui.Node("FxNoteLayer", viewport.transform).Stretch();
            var noteLayer = Ui.Node("NoteLayer", viewport.transform).Stretch();

            float laneSpan = (GearHalfWidth - LaneInset) * 2f;

            var deck = Ui.Image("Deck", gear, Ui.Chrome("PanelCut"), UiPalette.Panel)
                .Set(Anchor.BottomCentre, 0f, 0f, GearHalfWidth * 2f - 12f, DeckHeight + 12f);
            deck.transform.SetSiblingIndex(gear.childCount - 1);

            var deckLayer = Ui.Node("DeckLayer", gear).Set(Anchor.BottomCentre, 0f, 0f, laneSpan, DeckHeight);

            var judgementAnchor = Ui.Node("JudgementAnchor", gear).Set(Anchor.BottomCentre, 0f, DeckHeight, 0f, 0f);
            var glow = Ui.Image("JudgementGlow", judgementAnchor, Ui.Chrome("Glow"), UiPalette.Cyan.WithAlpha(0.32f))
                .Set(Anchor.Centre, 0f, 0f, laneSpan + 160f, 190f);
            glow.type = Image.Type.Simple;
            var bar = Ui.Image("JudgementBar", judgementAnchor, Ui.Chrome("Bar"), UiPalette.Cyan)
                .Set(Anchor.Centre, 0f, 0f, laneSpan + 12f, 6f);

            // Outside the mask so a hit burst is not sliced off at the judgement line.
            var burstLayer = Ui.Node("BurstLayer", gear).Set(Anchor.BottomCentre, 0f, DeckHeight, laneSpan, 420f);

            // Anchored to the screen, not the gear: the gear's own top edge is off-screen.
            feedback = BuildFeedback(screen);

            view.laneViewport = (RectTransform)viewport.transform;
            view.laneLayer = laneLayer;
            view.beamLayer = beamLayer;
            view.noteLayer = noteLayer;
            view.fxNoteLayer = fxNoteLayer;
            view.burstLayer = burstLayer;
            view.deckLayer = deckLayer;
            view.judgementBar = (RectTransform)bar.transform;
            view.judgementGlow = (RectTransform)glow.transform;
            view.normalNoteSprite = Ui.Chrome("NoteNormal");
            view.fxNoteSprite = Ui.Chrome("NoteFx");
            view.keyBeamSprite = Ui.Chrome("KeyBeam");
            view.keyBurstSprite = Ui.Chrome("KeyBurst");
            view.lanePlateSprite = Ui.Chrome("Panel");
            view.laneGuideSprite = Ui.Chrome("LaneGuide");
            view.keyCapSprite = Ui.Chrome("KeyCap");
            view.keyFont = Ui.Font(Weight.Bold);
            return view;
        }

        private static JudgementFeedbackView BuildFeedback(RectTransform screen)
        {
            var holder = Ui.Node("Feedback", screen).Stretch();
            var view = holder.gameObject.AddComponent<JudgementFeedbackView>();

            var comboGroup = Ui.Group("Combo", holder).Set(Anchor.TopCentre, 0f, -70f, 560f, 170f);
            var comboCaption = Ui.Text("Caption", comboGroup.transform, "COMBO", 24f, Weight.Bold,
                UiPalette.TextSecondary, TextAlignmentOptions.Center).Set(Anchor.TopCentre, 0f, 0f, 560f, 30f);
            var comboLabel = Ui.Text("Value", comboGroup.transform, "0", 110f, Weight.Black,
                UiPalette.TextPrimary, TextAlignmentOptions.Center).Set(Anchor.TopCentre, 0f, -28f, 560f, 130f);
            var comboPunch = comboLabel.gameObject.AddComponent<ScalePunch>();
            comboPunch.target = (RectTransform)comboLabel.transform;
            comboPunch.peakScale = 1.12f;

            var judgementGroup = Ui.Group("Judgement", holder).Set(Anchor.BottomCentre, 0f, 272f, 620f, 150f);
            var grade = Ui.Text("Grade", judgementGroup.transform, "PERFECT", 62f, Weight.Black,
                UiPalette.Cyan, TextAlignmentOptions.Center).Set(Anchor.TopCentre, 0f, 0f, 620f, 76f);
            var suffix = Ui.Text("Suffix", judgementGroup.transform, "HIGH", 26f, Weight.Bold,
                UiPalette.Cyan, TextAlignmentOptions.Center).Set(Anchor.TopCentre, 0f, -72f, 620f, 32f);
            var timing = Ui.Text("Timing", judgementGroup.transform, string.Empty, 22f, Weight.Bold,
                UiPalette.TextMuted, TextAlignmentOptions.Center).Set(Anchor.TopCentre, 0f, -106f, 620f, 28f);
            var gradePunch = grade.gameObject.AddComponent<ScalePunch>();
            gradePunch.target = (RectTransform)grade.transform;

            var banner = Ui.Text("Banner", holder, string.Empty, 120f, Weight.Black, UiPalette.Magenta,
                TextAlignmentOptions.Center).Set(Anchor.Centre, 0f, 40f, 600f, 150f);

            view.comboGroup = comboGroup;
            view.comboLabel = comboLabel;
            view.comboCaptionLabel = comboCaption;
            view.comboPunch = comboPunch;
            view.judgementGroup = judgementGroup;
            view.gradeLabel = grade;
            view.gradeSuffixLabel = suffix;
            view.timingLabel = timing;
            view.gradePunch = gradePunch;
            view.bannerLabel = banner;
            return view;
        }

        private static void BuildLeftColumn(RectTransform screen, GameplayScreenView view)
        {
            var card = Ui.CutPanel("PlayerCard", screen, UiPalette.Panel)
                .Set(Anchor.TopLeft, 40f, -40f, ColumnWidth, 112f);
            var avatar = Ui.Image("Avatar", card.transform, Ui.Art("Mascot-Bust"), Color.white)
                .Set(Anchor.MiddleLeft, 18f, 0f, 76f, 76f);
            avatar.preserveAspect = true;
            avatar.type = Image.Type.Simple;
            var name = Ui.Text("Name", card.transform, string.Empty, 26f, Weight.Bold, UiPalette.TextPrimary)
                .Set(Anchor.MiddleLeft, 108f, 20f, 300f, 32f);
            var tag = Ui.Text("Tag", card.transform, string.Empty, 17f, Weight.Regular, UiPalette.TextMuted)
                .Set(Anchor.MiddleLeft, 110f, -6f, 300f, 22f);
            Ui.Text("ScoreCaption", card.transform, "SCORE", 14f, Weight.Medium, UiPalette.TextMuted)
                .Set(Anchor.MiddleLeft, 110f, -34f, 120f, 20f);
            var score = Ui.Text("Score", card.transform, "0", 34f, Weight.Black, UiPalette.TextPrimary,
                TextAlignmentOptions.Right).Set(Anchor.MiddleRight, -20f, -14f, 260f, 40f);

            Ui.CutPanel("SongPanel", screen, UiPalette.Panel.WithAlpha(0.82f))
                .Set(Anchor.TopLeft, 40f, -164f, ColumnWidth, 440f);
            var songCard = UiWidgets.SongCard(screen, "SongCard", ColumnWidth - 40f, 180f);
            songCard.Set(Anchor.TopLeft, 60f, -180f, ColumnWidth - 40f, 420f);

            var tally = BuildTally(screen);
            var speed = BuildSpeedChip(screen);

            view.playerAvatar = avatar;
            view.playerNameLabel = name;
            view.playerTagLabel = tag;
            view.scoreLabel = score;
            view.songCard = songCard;
            view.tally = tally;
            view.speedLabel = speed;
        }

        private static TallyPanelView BuildTally(RectTransform screen)
        {
            var panel = Ui.CutPanel("TallyPanel", screen, UiPalette.Panel)
                .Set(Anchor.TopLeft, 40f, -620f, ColumnWidth, 330f);
            var view = panel.gameObject.AddComponent<TallyPanelView>();

            var captions = new TMP_Text[5];
            var counts = new TMP_Text[5];
            for (int index = 0; index < 5; index++)
            {
                float y = -22f - index * 38f;
                var grade = (JudgementGrade)index;
                captions[index] = Ui.Text("Caption" + index, panel.transform, UiNaming.TallyLabel(grade), 21f,
                    Weight.Bold, UiPalette.GradeColor(grade)).Set(Anchor.TopLeft, 26f, y, 260f, 28f);
                counts[index] = Ui.Text("Count" + index, panel.transform, "0000", 24f, Weight.Bold,
                    UiPalette.TextPrimary, TextAlignmentOptions.Right).Set(Anchor.TopRight, -26f, y, 160f, 28f);
            }

            Ui.Image("Rule", panel.transform, Ui.Chrome("Bar"), UiPalette.Divider)
                .Set(Anchor.TopCentre, 0f, -218f, ColumnWidth - 52f, 2f);

            Ui.Text("TotalCaption", panel.transform, "TOTAL", 20f, Weight.Medium, UiPalette.TextMuted)
                .Set(Anchor.TopLeft, 26f, -238f, 200f, 26f);
            var accuracy = Ui.Text("Accuracy", panel.transform, "100.0000%", 44f, Weight.Black,
                UiPalette.TextPrimary, TextAlignmentOptions.Right).Set(Anchor.TopRight, -26f, -240f, 320f, 52f);

            Ui.Text("RatingCaption", panel.transform, "RATING", 18f, Weight.Medium, UiPalette.TextMuted)
                .Set(Anchor.TopLeft, 26f, -292f, 200f, 24f);
            var rating = Ui.Text("Rating", panel.transform, "0.00", 26f, Weight.Bold, UiPalette.Magenta,
                TextAlignmentOptions.Right).Set(Anchor.TopRight, -26f, -292f, 220f, 30f);

            view.captionLabels = captions;
            view.countLabels = counts;
            view.accuracyLabel = accuracy;
            view.ratingLabel = rating;
            return view;
        }

        private static TMP_Text BuildSpeedChip(RectTransform screen)
        {
            var row = Ui.Node("Chips", screen).Set(Anchor.TopLeft, 40f, -962f, ColumnWidth, 58f);

            var speedChip = Ui.Image("SpeedChip", row, Ui.Chrome("BarCut"), UiPalette.PanelSoft)
                .Set(Anchor.MiddleLeft, 0f, 0f, 200f, 54f);
            Ui.Text("Caption", speedChip.transform, "SPEED", 16f, Weight.Medium, UiPalette.TextMuted)
                .Set(Anchor.MiddleLeft, 18f, 0f, 90f, 24f);
            var speed = Ui.Text("Value", speedChip.transform, "5.0", 28f, Weight.Black, UiPalette.Cyan,
                TextAlignmentOptions.Right).Set(Anchor.MiddleRight, -18f, 0f, 100f, 32f);

            return speed;
        }

        private static void BuildRightColumn(RectTransform screen, GameplayScreenView view)
        {
            // Hugging the gear's right rail, rising from the judgement line.
            var gaugeRoot = Ui.Node("HealthGauge", screen)
                .Set(Anchor.BottomCentre, GearHalfWidth + 40f, GearBottom + DeckHeight, 30f, 660f);
            var gauge = gaugeRoot.gameObject.AddComponent<HealthGaugeView>();

            Ui.Image("Track", gaugeRoot, Ui.Chrome("Bar"), UiPalette.Night.WithAlpha(0.9f)).Stretch();
            var glow = Ui.Image("Glow", gaugeRoot, Ui.Chrome("Glow"), UiPalette.Cyan.WithAlpha(0.22f))
                .Stretch(-10f, -6f, -10f, -6f);
            var fill = Ui.Image("Fill", gaugeRoot, Ui.Chrome("Bar"), UiPalette.Cyan).Stretch(5f, 5f, 5f, 5f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            fill.fillAmount = 1f;

            var smooth = gaugeRoot.gameObject.AddComponent<SmoothFill>();
            smooth.target = fill;

            Ui.Text("Caption", gaugeRoot, "HP", 15f, Weight.Bold, UiPalette.TextMuted, TextAlignmentOptions.Center)
                .Set(Anchor.BottomCentre, 0f, -26f, 60f, 22f);

            gauge.fill = fill;
            gauge.glow = glow;
            gauge.smoothFill = smooth;
            view.health = gauge;

            var mascot = Ui.Image("Mascot", screen, Ui.Art("Mascot-Idle"), Color.white)
                .Set(Anchor.MiddleRight, -40f, -20f, 500f, 900f);
            mascot.preserveAspect = true;
            mascot.type = Image.Type.Simple;
            mascot.color = Color.white.WithAlpha(0.95f);
        }

        private static void BuildBottom(RectTransform screen, GameplayScreenView view)
        {
            var strip = Ui.Image("ProgressStrip", screen, Ui.Chrome("PanelCut"), UiPalette.Panel)
                .Set(Anchor.BottomCentre, 0f, 34f, 780f, 66f);
            var progress = strip.gameObject.AddComponent<ProgressStripView>();

            var badge = Ui.Image("SectionBadge", strip.transform, Ui.Chrome("Bar"), UiPalette.Cyan.WithAlpha(0.22f))
                .Set(Anchor.MiddleLeft, 12f, 0f, 92f, 46f);
            Ui.Text("Caption", badge.transform, "SECTION", 12f, Weight.Medium, UiPalette.TextMuted,
                TextAlignmentOptions.Center).Set(Anchor.TopCentre, 0f, -4f, 92f, 16f);
            var sectionIndex = Ui.Text("Index", badge.transform, "1", 24f, Weight.Black, UiPalette.Cyan,
                TextAlignmentOptions.Center).Set(Anchor.TopCentre, 0f, -18f, 92f, 28f);

            var sectionName = Ui.Text("SectionName", strip.transform, string.Empty, 22f, Weight.Bold,
                UiPalette.TextSecondary).Set(Anchor.MiddleLeft, 118f, 12f, 400f, 28f);

            var track = Ui.Image("Track", strip.transform, Ui.Chrome("Bar"), UiPalette.Night)
                .Set(Anchor.MiddleLeft, 118f, -16f, 520f, 10f);
            var fill = Ui.Image("Fill", track.transform, Ui.Chrome("Bar"), UiPalette.Magenta).Stretch();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;

            var elapsed = Ui.Text("Elapsed", strip.transform, "00:00 / 00:00", 20f, Weight.Medium,
                UiPalette.TextMuted, TextAlignmentOptions.Right).Set(Anchor.MiddleRight, -16f, 0f, 190f, 28f);

            progress.fill = fill;
            progress.sectionIndexLabel = sectionIndex;
            progress.sectionNameLabel = sectionName;
            progress.elapsedLabel = elapsed;
            view.progress = progress;

            view.keyGuideLabel = Ui.Text("KeyGuide", screen, string.Empty, 18f, Weight.Medium, UiPalette.TextMuted,
                TextAlignmentOptions.Right).Set(Anchor.BottomRight, -44f, 44f, 620f, 26f);
        }

        private static void BuildPauseOverlay(RectTransform screen, GameplayScreenView view)
        {
            var overlay = Ui.Group("PauseOverlay", screen).Stretch();
            overlay.alpha = 0f;
            overlay.blocksRaycasts = false;
            overlay.interactable = false;

            Ui.Image("Shade", overlay.transform, null, UiPalette.Ink.WithAlpha(0.86f)).Stretch();
            var panel = Ui.Panel("Menu", overlay.transform, UiPalette.PanelRaised.WithAlpha(0.98f))
                .Set(Anchor.Centre, 0f, 0f, 560f, 600f);
            var title = Ui.Text("Title", panel.transform, "일시정지", 64f, Weight.Black, UiPalette.TextPrimary,
                TextAlignmentOptions.Center).Set(Anchor.TopCentre, 0f, -54f, 480f, 82f);
            var guide = Ui.Text("Guide", panel.transform, string.Empty, 20f, Weight.Medium,
                UiPalette.TextSecondary, TextAlignmentOptions.Center).Set(Anchor.TopCentre, 0f, -140f, 480f, 34f);

            var resume = Ui.Button("ResumeButton", panel.transform, Ui.Chrome("PanelCut"), UiPalette.Cyan)
                .Set(Anchor.TopCentre, 0f, -210f, 400f, 82f);
            Ui.Text("Label", resume.transform, "재개", 30f, Weight.Bold, UiPalette.Ink,
                TextAlignmentOptions.Center).Stretch();

            var restart = Ui.Button("RestartButton", panel.transform, Ui.Chrome("PanelCut"), UiPalette.Magenta)
                .Set(Anchor.TopCentre, 0f, -310f, 400f, 82f);
            Ui.Text("Label", restart.transform, "처음부터", 30f, Weight.Bold, UiPalette.TextPrimary,
                TextAlignmentOptions.Center).Stretch();

            var quit = Ui.Button("QuitButton", panel.transform, Ui.Chrome("PanelCut"), UiPalette.PanelSoft)
                .Set(Anchor.TopCentre, 0f, -410f, 400f, 82f);
            Ui.Text("Label", quit.transform, "나가기", 30f, Weight.Bold, UiPalette.TextPrimary,
                TextAlignmentOptions.Center).Stretch();

            view.pauseOverlay = overlay;
            view.pauseTitleLabel = title;
            view.pauseGuideLabel = guide;
            view.resumeButton = resume;
            view.restartButton = restart;
            view.quitButton = quit;
        }
    }
}
