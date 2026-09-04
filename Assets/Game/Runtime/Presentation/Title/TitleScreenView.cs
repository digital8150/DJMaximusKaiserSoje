using DJMaximusKaiserSoje.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DJMaximusKaiserSoje.Presentation
{
    /// <summary>The attract screen. Anything the player presses moves on.</summary>
    [DisallowMultipleComponent]
    public sealed class TitleScreenView : MonoBehaviour, ITitleScreenView
    {
        [SerializeField] internal TMP_Text titleLabel;
        [SerializeField] internal TMP_Text subtitleLabel;
        [SerializeField] internal TMP_Text promptLabel;
        [SerializeField] internal TMP_Text buildLabel;
        [SerializeField] internal CanvasGroup fader;

        private GameServices services;
        private bool leaving;

        public void Bind(GameServices gameServices)
        {
            services = gameServices;
            leaving = false;
            if (fader != null) fader.alpha = 1f;
            if (buildLabel != null) buildLabel.text = Application.version;
            services.Music.PlayTheme(ScreenTheme.Title);
        }

        private void Update()
        {
            if (services == null || leaving) return;
            if (!AnyInputThisFrame()) return;

            leaving = true;
            services.Flow.ShowSongSelect();
        }

        private static bool AnyInputThisFrame()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;

            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }
    }
}
