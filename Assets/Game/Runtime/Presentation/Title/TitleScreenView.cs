using DJMaximusKaiserSoje.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
        [SerializeField] internal Button optionsButton;
        [SerializeField] internal CanvasGroup fader;

        private GameServices services;
        private bool leaving;

        public void Bind(GameServices gameServices)
        {
            services = gameServices;
            leaving = false;
            if (fader != null) fader.alpha = 1f;
            if (promptLabel != null) promptLabel.text = "아무 키나 눌러 시작";
            if (buildLabel != null) buildLabel.text = Application.version;
            if (optionsButton != null)
            {
                optionsButton.onClick.RemoveListener(OpenOptions);
                optionsButton.onClick.AddListener(OpenOptions);
            }
            services.Music.PlayTheme(ScreenTheme.Title);
        }

        private void OnDestroy()
        {
            if (optionsButton != null) optionsButton.onClick.RemoveListener(OpenOptions);
        }

        private void Update()
        {
            if (services == null || leaving) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.oKey.wasPressedThisFrame)
            {
                OpenOptions();
                return;
            }
            if (!AnyInputThisFrame()) return;

            leaving = true;
            services.Flow.ShowSongSelect();
        }

        private static bool AnyInputThisFrame()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;

            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame &&
                   (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());
        }

        private void OpenOptions()
        {
            if (services == null || leaving) return;
            leaving = true;
            services.Flow.ShowOptions();
        }
    }
}
