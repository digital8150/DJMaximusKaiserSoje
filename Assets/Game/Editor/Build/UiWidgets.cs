using DJMaximusKaiserSoje.Core;
using DJMaximusKaiserSoje.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Editor
{
    /// <summary>Pieces that appear on more than one screen, built the same way every time.</summary>
    internal static class UiWidgets
    {
        /// <summary>A difficulty slot: tier initials over the chart's level.</summary>
        public static DifficultyChipView Chip(Transform parent, DifficultyTier tier, float width = 64f, float height = 64f)
        {
            var rect = Ui.Node("Chip" + tier, parent);
            rect.sizeDelta = new Vector2(width, height);
            var view = rect.gameObject.AddComponent<DifficultyChipView>();

            var plate = Ui.Image("Plate", rect, Ui.Chrome("Panel"), UiPalette.PanelSoft.WithAlpha(0.55f)).Stretch();
            plate.raycastTarget = true;
            var outline = Ui.Image("Outline", rect, Ui.Chrome("PanelOutline"), UiPalette.Cyan).Stretch();
            outline.enabled = false;

            var level = Ui.Text("Level", rect, "–", height * 0.44f, Weight.ExtraBold, UiPalette.TextMuted,
                TextAlignmentOptions.Center).Set(Anchor.TopCentre, 0f, -height * 0.12f, width, height * 0.5f);
            var tierLabel = Ui.Text("Tier", rect, UiNaming.TierShortName(tier), height * 0.2f, Weight.Bold,
                UiPalette.TierColor(tier).WithAlpha(0.75f), TextAlignmentOptions.Center)
                .Set(Anchor.BottomCentre, 0f, height * 0.1f, width, height * 0.24f);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            button.transition = Selectable.Transition.None;

            view.plate = plate;
            view.outline = outline;
            view.levelLabel = level;
            view.tierLabel = tierLabel;
            view.button = button;
            return view;
        }

        public static TabButtonView Tab(Transform parent, string name, float width, float height = 48f)
        {
            var rect = Ui.Node(name, parent);
            rect.sizeDelta = new Vector2(width, height);
            var view = rect.gameObject.AddComponent<TabButtonView>();

            var plate = Ui.Image("Plate", rect, Ui.Chrome("PanelCut"), UiPalette.Panel.WithAlpha(0.4f)).Stretch();
            plate.raycastTarget = true;
            var label = Ui.Text("Label", rect, name, height * 0.42f, Weight.Bold, UiPalette.TextMuted,
                TextAlignmentOptions.Center).Stretch();
            var underline = Ui.Image("Underline", rect, Ui.Chrome("Underline"), UiPalette.Cyan)
                .Set(Anchor.BottomCentre, 0f, 2f, width - 12f, 5f);
            underline.enabled = false;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            button.transition = Selectable.Transition.None;

            view.plate = plate;
            view.label = label;
            view.underline = underline;
            view.button = button;
            return view;
        }

        /// <summary>A caption on the left and its value on the right, the shape most panels repeat.</summary>
        public static TMP_Text StatLine(Transform parent, string name, string caption, float y, float width,
            float captionSize = 20f, float valueSize = 26f, Anchor anchor = Anchor.TopLeft)
        {
            var row = Ui.Node(name, parent).Set(anchor, 0f, y, width, 34f);
            Ui.Text("Caption", row, caption, captionSize, Weight.Medium, UiPalette.TextMuted)
                .Set(Anchor.MiddleLeft, 0f, 0f, width * 0.6f, 30f);
            return Ui.Text("Value", row, "—", valueSize, Weight.Bold, UiPalette.TextPrimary,
                TextAlignmentOptions.Right).Set(Anchor.MiddleRight, 0f, 0f, width * 0.6f, 32f);
        }

        /// <summary>
        /// A framed jacket slot. The placeholder is authored in, not only applied at runtime, so an
        /// unfilled slot reads as an empty plate rather than a white block.
        /// </summary>
        public static AddressableImage Jacket(Transform parent, string name, Color frameColor, float inset = 6f)
        {
            var holder = Ui.Node(name, parent);
            var placeholder = Ui.Chrome("Panel");

            var art = Ui.Image("Art", holder, placeholder, UiPalette.PanelSoft).Stretch(inset, inset, inset, inset);
            art.type = Image.Type.Simple;
            art.preserveAspect = true;
            Ui.Image("Frame", holder, Ui.Chrome("JacketFrame"), frameColor).Stretch();

            var addressable = holder.gameObject.AddComponent<AddressableImage>();
            addressable.target = art;
            addressable.placeholder = placeholder;
            addressable.placeholderTint = UiPalette.PanelSoft;
            return addressable;
        }

        /// <summary>The jacket-and-title block shared by the gameplay and result screens.</summary>
        public static SongCardView SongCard(Transform parent, string name, float width, float jacketSize)
        {
            var rect = Ui.Node(name, parent);
            var view = rect.gameObject.AddComponent<SongCardView>();

            var addressable = Jacket(rect, "Jacket", UiPalette.Cyan.WithAlpha(0.75f))
                .Set(Anchor.TopCentre, 0f, 0f, jacketSize, jacketSize);

            float y = -jacketSize - 18f;
            var category = Ui.Text("Category", rect, string.Empty, 20f, Weight.Bold, UiPalette.Magenta)
                .Set(Anchor.TopLeft, 0f, y, width, 26f);
            var title = Ui.Text("Title", rect, string.Empty, 38f, Weight.ExtraBold, UiPalette.TextPrimary)
                .Set(Anchor.TopLeft, 0f, y - 30f, width, 48f);
            var artist = Ui.Text("Artist", rect, string.Empty, 22f, Weight.Regular, UiPalette.TextSecondary)
                .Set(Anchor.TopLeft, 0f, y - 82f, width, 30f);
            var bpm = Ui.Text("Bpm", rect, string.Empty, 20f, Weight.Medium, UiPalette.TextMuted)
                .Set(Anchor.TopLeft, 0f, y - 116f, width * 0.5f, 28f);
            var style = Ui.Text("Style", rect, string.Empty, 20f, Weight.Bold, UiPalette.Cyan,
                TextAlignmentOptions.Right).Set(Anchor.TopRight, 0f, y - 116f, width * 0.5f, 28f);

            var tierPlate = Ui.Image("TierPlate", rect, Ui.Chrome("PanelCut"), UiPalette.Violet.WithAlpha(0.28f))
                .Set(Anchor.TopLeft, 0f, y - 156f, width, 62f);
            var tier = Ui.Text("Tier", tierPlate.transform, string.Empty, 24f, Weight.Bold, UiPalette.Violet)
                .Set(Anchor.MiddleLeft, 20f, 0f, width * 0.6f, 32f);
            var level = Ui.Text("Level", tierPlate.transform, string.Empty, 40f, Weight.Black, UiPalette.TextPrimary,
                TextAlignmentOptions.Right).Set(Anchor.MiddleRight, -20f, 0f, width * 0.35f, 46f);

            view.jacket = addressable;
            view.categoryLabel = category;
            view.titleLabel = title;
            view.artistLabel = artist;
            view.bpmLabel = bpm;
            view.styleLabel = style;
            view.tierPlate = tierPlate;
            view.tierLabel = tier;
            view.levelLabel = level;
            return view;
        }
    }
}
