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
        [SerializeField] internal Button crewButton;
        [SerializeField] internal CrewRoomModalView crewModal;
        [SerializeField] internal BattleCrewCatalog battleCrewCatalog;
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
            if (crewButton != null)
            {
                crewButton.onClick.RemoveListener(OpenCrewRoom);
                crewButton.onClick.AddListener(OpenCrewRoom);
            }
            if (crewModal != null)
            {
                crewModal.Bind(services, battleCrewCatalog);
            }
            services.Music.PlayTheme(ScreenTheme.Title);
        }

        private void OnDestroy()
        {
            if (optionsButton != null) optionsButton.onClick.RemoveListener(OpenOptions);
            if (crewButton != null) crewButton.onClick.RemoveListener(OpenCrewRoom);
        }

        private void Update()
        {
            if (services == null || leaving) return;
            if (crewModal != null && crewModal.IsOpen) return;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.cKey.wasPressedThisFrame)
                {
                    OpenCrewRoom();
                    return;
                }
                if (keyboard.oKey.wasPressedThisFrame)
                {
                    OpenOptions();
                    return;
                }
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

        private void OpenCrewRoom()
        {
            if (services == null || leaving) return;
            if (crewModal != null)
            {
                crewModal.Bind(services, battleCrewCatalog);
                crewModal.Open();
            }
        }

        private void OpenOptions()
        {
            if (services == null || leaving) return;
            leaving = true;
            services.Flow.ShowOptions();
        }
    }
}
