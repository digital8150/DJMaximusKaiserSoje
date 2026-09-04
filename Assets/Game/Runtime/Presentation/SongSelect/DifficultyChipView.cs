using System;
using DJMaximusKaiserSoje.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Presentation
{
    /// <summary>One difficulty slot: its tier and the level of the chart that fills it, if any.</summary>
    [DisallowMultipleComponent]
    public sealed class DifficultyChipView : MonoBehaviour
    {
        [SerializeField] internal Image plate;
        [SerializeField] internal Image outline;
        [SerializeField] internal TMP_Text levelLabel;
        [SerializeField] internal TMP_Text tierLabel;
        [SerializeField] internal Button button;

        private DifficultyTier tier;
        private Action<DifficultyTier> clicked;

        public DifficultyTier Tier => tier;

        public bool HasChart { get; private set; }

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(OnClick);
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(OnClick);
        }

        public void Bind(DifficultyTier chipTier, ChartSummary chart, Action<DifficultyTier> onClicked)
        {
            tier = chipTier;
            clicked = onClicked;
            HasChart = chart != null;

            if (tierLabel != null) tierLabel.text = UiNaming.TierShortName(chipTier);
            if (levelLabel != null) levelLabel.text = HasChart ? chart.Level.ToString() : "–";

            var color = UiPalette.TierColor(chipTier);
            if (levelLabel != null) levelLabel.color = HasChart ? color : UiPalette.TextMuted;
            if (tierLabel != null) tierLabel.color = HasChart ? color.WithAlpha(0.75f) : UiPalette.TextMuted.WithAlpha(0.6f);
            if (button != null) button.interactable = HasChart && clicked != null;

            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (plate != null)
                plate.color = selected && HasChart
                    ? UiPalette.TierColor(tier).WithAlpha(0.22f)
                    : UiPalette.PanelSoft.WithAlpha(selected ? 0.9f : 0.55f);

            if (outline != null)
            {
                outline.enabled = selected && HasChart;
                outline.color = UiPalette.TierColor(tier);
            }
        }

        private void OnClick() => clicked?.Invoke(tier);
    }
}
