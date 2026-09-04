using System.IO;
using DJMaximusKaiserSoje.Core;
using DJMaximusKaiserSoje.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Editor
{
    /// <summary>The one row the song list repeats, saved as a prefab the select screen instantiates.</summary>
    internal static class SongRowPrefabBuilder
    {
        public const string PrefabFolder = "Assets/Game/UI/Prefabs";
        public const string PrefabPath = PrefabFolder + "/SongRow.prefab";

        public const float RowHeight = 108f;

        public static SongRowView Build()
        {
            Directory.CreateDirectory(PrefabFolder);

            var rect = Ui.Node("SongRow", null);
            rect.sizeDelta = new Vector2(1040f, RowHeight);
            var view = rect.gameObject.AddComponent<SongRowView>();

            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = RowHeight;
            layout.minHeight = RowHeight;

            var background = Ui.Image("Background", rect, Ui.Chrome("PanelCut"), UiPalette.Panel.WithAlpha(0.72f)).Stretch();
            background.raycastTarget = true;

            var edge = Ui.Image("SelectionEdge", rect, Ui.Chrome("Bar"), UiPalette.Cyan)
                .Set(Anchor.MiddleLeft, 10f, 0f, 6f, 72f);
            edge.enabled = false;

            var jacket = UiWidgets.Jacket(rect, "Jacket", UiPalette.Divider, 3f)
                .Set(Anchor.MiddleLeft, 30f, 0f, 76f, 76f);

            var title = Ui.Text("Title", rect, "TITLE", 28f, Weight.Bold, UiPalette.TextSecondary)
                .Set(Anchor.MiddleLeft, 124f, 18f, 540f, 36f);
            var artist = Ui.Text("Artist", rect, "ARTIST", 19f, Weight.Regular, UiPalette.TextMuted)
                .Set(Anchor.MiddleLeft, 126f, -16f, 360f, 26f);
            var bpm = Ui.Text("Bpm", rect, "BPM", 18f, Weight.Medium, UiPalette.TextMuted)
                .Set(Anchor.MiddleLeft, 500f, -16f, 180f, 26f);

            var chipRow = Ui.Node("Chips", rect).Set(Anchor.MiddleRight, -24f, 0f, 332f, 68f);
            var chips = new DifficultyChipView[5];
            for (int index = 0; index < chips.Length; index++)
            {
                var chip = UiWidgets.Chip(chipRow, (DifficultyTier)index, 60f, 64f);
                chip.Set(Anchor.MiddleLeft, index * 68f, 0f, 60f, 64f);
                if (chip.button != null) chip.button.interactable = false;
                chips[index] = chip;
            }

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;

            view.root = rect;
            view.background = background;
            view.selectionEdge = edge;
            view.jacket = jacket;
            view.titleLabel = title;
            view.artistLabel = artist;
            view.bpmLabel = bpm;
            view.chips = chips;
            view.button = button;

            var prefab = PrefabUtility.SaveAsPrefabAsset(rect.gameObject, PrefabPath);
            Object.DestroyImmediate(rect.gameObject);
            return prefab.GetComponent<SongRowView>();
        }

        public static SongRowView Load() =>
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)?.GetComponent<SongRowView>();
    }
}
