using DJMaximusKaiserSoje.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Presentation
{
    /// <summary>How far into the song the run is, and which part of it is playing.</summary>
    [DisallowMultipleComponent]
    public sealed class ProgressStripView : MonoBehaviour
    {
        [SerializeField] internal Image fill;
        [SerializeField] internal TMP_Text sectionIndexLabel;
        [SerializeField] internal TMP_Text sectionNameLabel;
        [SerializeField] internal TMP_Text elapsedLabel;

        public void SetProgress(double progress01, double songTimeMs, double songLengthMs)
        {
            if (fill != null) fill.fillAmount = Mathf.Clamp01((float)progress01);
            if (elapsedLabel == null) return;

            double elapsed = Mathf.Max(0f, (float)songTimeMs) / 1000.0;
            elapsedLabel.text = UiFormat.Time(elapsed) + " / " + UiFormat.Time(songLengthMs / 1000.0);
        }

        public void SetSection(SectionMarker section)
        {
            if (sectionIndexLabel != null) sectionIndexLabel.text = (section.Index + 1).ToString();
            if (sectionNameLabel != null) sectionNameLabel.text = section.Name;
        }
    }
}
