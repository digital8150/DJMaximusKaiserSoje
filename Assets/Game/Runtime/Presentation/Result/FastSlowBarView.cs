using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Presentation
{
    /// <summary>
    /// Which side the imprecise hits fell on. A player leaning one way can act on it by moving the
    /// judgement offset, which is why the split is worth showing at all.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FastSlowBarView : MonoBehaviour
    {
        [SerializeField] internal RectTransform fastFill;
        [SerializeField] internal RectTransform slowFill;
        [SerializeField] internal TMP_Text fastLabel;
        [SerializeField] internal TMP_Text slowLabel;
        [SerializeField] internal Image fastImage;
        [SerializeField] internal Image slowImage;

        public void Bind(int fast, int slow)
        {
            if (fastLabel != null) fastLabel.text = fast.ToString();
            if (slowLabel != null) slowLabel.text = slow.ToString();
            if (fastImage != null) fastImage.color = UiPalette.Cyan;
            if (slowImage != null) slowImage.color = UiPalette.Rose;

            int total = fast + slow;
            float fastShare = total == 0 ? 0.5f : (float)fast / total;

            if (fastFill != null)
            {
                fastFill.anchorMin = new Vector2(0f, 0f);
                fastFill.anchorMax = new Vector2(fastShare, 1f);
                fastFill.offsetMin = Vector2.zero;
                fastFill.offsetMax = Vector2.zero;
            }

            if (slowFill == null) return;
            slowFill.anchorMin = new Vector2(fastShare, 0f);
            slowFill.anchorMax = new Vector2(1f, 1f);
            slowFill.offsetMin = Vector2.zero;
            slowFill.offsetMax = Vector2.zero;
        }
    }
}
