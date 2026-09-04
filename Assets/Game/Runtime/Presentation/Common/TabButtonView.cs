using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Presentation
{
    /// <summary>A tab in a strip of mutually exclusive choices.</summary>
    [DisallowMultipleComponent]
    public sealed class TabButtonView : MonoBehaviour
    {
        [SerializeField] internal Image plate;
        [SerializeField] internal Image underline;
        [SerializeField] internal TMP_Text label;
        [SerializeField] internal Button button;

        private int index;
        private Action<int> clicked;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(OnClick);
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(OnClick);
        }

        public void Bind(int tabIndex, string text, Action<int> onClicked)
        {
            index = tabIndex;
            clicked = onClicked;
            if (label != null) label.text = text;
        }

        public void SetSelected(bool selected)
        {
            if (label != null) label.color = selected ? UiPalette.TextPrimary : UiPalette.TextMuted;
            if (underline != null) underline.enabled = selected;
            if (plate != null) plate.color = selected ? UiPalette.PanelRaised : UiPalette.Panel.WithAlpha(0.4f);
        }

        private void OnClick() => clicked?.Invoke(index);
    }
}
