using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Presentation
{
    /// <summary>
    /// One headline number on the result screen, with the ribbon and delta that appear only when the
    /// run actually beat the stored best.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResultRowView : MonoBehaviour
    {
        [SerializeField] internal TMP_Text captionLabel;
        [SerializeField] internal TMP_Text valueLabel;
        [SerializeField] internal TMP_Text deltaLabel;
        [SerializeField] internal GameObject recordRibbon;
        [SerializeField] internal TMP_Text recordRibbonLabel;
        [SerializeField] internal Image bar;
        [SerializeField] internal ValueTicker ticker;
        [SerializeField] internal ScalePunch punch;

        public void Bind(string caption, double value, Func<double, string> format, bool isNewRecord, string deltaText, float delay)
        {
            if (captionLabel != null) captionLabel.text = caption;

            if (ticker != null && ticker.label != null) ticker.Play(value, format, delay);
            else if (valueLabel != null) valueLabel.text = format(value);

            if (recordRibbon != null) recordRibbon.SetActive(isNewRecord);
            if (recordRibbonLabel != null) recordRibbonLabel.text = "NEW RECORD";

            if (deltaLabel != null)
            {
                deltaLabel.gameObject.SetActive(isNewRecord && !string.IsNullOrEmpty(deltaText));
                deltaLabel.text = "▲ " + deltaText;
                deltaLabel.color = UiPalette.Mint;
            }

            if (bar != null) bar.color = isNewRecord ? UiPalette.Amber : UiPalette.PanelRaised;
            if (isNewRecord && punch != null) punch.Punch();
        }
    }
}
