using System;
using DJMaximusKaiserSoje.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Presentation
{
    /// <summary>A single song in the list, with the level of each difficulty it offers.</summary>
    [DisallowMultipleComponent]
    public sealed class SongRowView : MonoBehaviour, IPointerEnterHandler
    {
        [SerializeField] internal RectTransform root;
        [SerializeField] internal Image background;
        [SerializeField] internal Image selectionEdge;
        [SerializeField] internal AddressableImage jacket;
        [SerializeField] internal TMP_Text titleLabel;
        [SerializeField] internal TMP_Text artistLabel;
        [SerializeField] internal TMP_Text bpmLabel;
        [SerializeField] internal DifficultyChipView[] chips;
        [SerializeField] internal Button button;

        private int index;
        private Action<int> selected;
        private Action<int> committed;
        private bool isSelected;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(OnClick);
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(OnClick);
        }

        public void Bind(int rowIndex, SongSummary song, Action<int> onSelected, Action<int> onCommitted)
        {
            index = rowIndex;
            selected = onSelected;
            committed = onCommitted;

            if (titleLabel != null) titleLabel.text = song.Title;
            if (artistLabel != null) artistLabel.text = song.Artist;
            if (bpmLabel != null) bpmLabel.text = UiFormat.Bpm(song.Bpm);
            if (jacket != null) jacket.Show(song.JacketAddress);

            if (chips == null) return;
            for (int chipIndex = 0; chipIndex < chips.Length; chipIndex++)
            {
                var tier = (DifficultyTier)chipIndex;
                song.TryGetChart(tier, out var chart);
                chips[chipIndex].Bind(tier, chart, null);
            }
        }

        public void SetSelected(bool value)
        {
            isSelected = value;
            if (background != null)
                background.color = value ? UiPalette.PanelRaised : UiPalette.Panel.WithAlpha(0.72f);
            if (selectionEdge != null) selectionEdge.enabled = value;
            if (titleLabel != null) titleLabel.color = value ? UiPalette.TextPrimary : UiPalette.TextSecondary;
        }

        public void HighlightTier(DifficultyTier tier)
        {
            if (chips == null) return;
            for (int chipIndex = 0; chipIndex < chips.Length; chipIndex++)
                chips[chipIndex].SetSelected(isSelected && chips[chipIndex].Tier == tier);
        }

        public void OnPointerEnter(PointerEventData eventData) => selected?.Invoke(index);

        private void OnClick()
        {
            if (!isSelected)
            {
                selected?.Invoke(index);
                return;
            }

            committed?.Invoke(index);
        }
    }
}
