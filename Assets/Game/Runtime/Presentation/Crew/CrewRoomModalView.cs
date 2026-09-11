using System;
using System.Collections.Generic;
using DJMaximusKaiserSoje.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Presentation
{
    /// <summary>
    /// Rhythm Crew management and deployment popup modal.
    /// Accessible from both Title and SongSelect screens.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrewRoomModalView : MonoBehaviour
    {
        [Header("Modal Frame")]
        [SerializeField] internal CanvasGroup canvasGroup;
        [SerializeField] internal Button closeButton;
        [SerializeField] internal Button backgroundDismissButton;
        [SerializeField] internal TMP_Text titleLabel;

        [Header("Crew Preview")]
        [SerializeField] internal BattleCrewView previewCrewView;
        [SerializeField] internal TMP_Text crewNameLabel;
        [SerializeField] internal TMP_Text crewRoleLabel;
        [SerializeField] internal Button deployButton;
        [SerializeField] internal TMP_Text deployButtonLabel;

        [Header("Animation Test Buttons")]
        [SerializeField] internal Button excitedTestButton;
        [SerializeField] internal Button badTestButton;
        [SerializeField] internal Button idleTestButton;

        [Header("Roster")]
        [SerializeField] internal Button[] crewSelectButtons;
        [SerializeField] internal TMP_Text[] crewSelectLabels;
        [SerializeField] internal Image[] crewSelectIcons;
        [SerializeField] internal GameObject[] deployedBadges;
        [SerializeField] internal GameObject[] selectedHighlights;

        private GameServices services;
        private BattleCrewCatalog catalog;
        private string previewedCrewId = BattleCrewId.Default;
        private bool isOpen;

        public bool IsOpen => isOpen;
        public string PreviewedCrewId => previewedCrewId;

        public void Bind(GameServices gameServices, BattleCrewCatalog crewCatalog)
        {
            services = gameServices;
            catalog = crewCatalog;

            if (titleLabel != null) titleLabel.text = "리듬 크루";

            UnhookEvents();
            HookEvents();

            if (services != null && services.Preferences != null)
            {
                previewedCrewId = BattleCrewId.Normalize(services.Preferences.SelectedCrewId);
                services.Preferences.Changed -= OnPreferencesChanged;
                services.Preferences.Changed += OnPreferencesChanged;
            }

            Refresh();
            Show(false);
        }

        private void OnDestroy()
        {
            UnhookEvents();
            if (services != null && services.Preferences != null)
            {
                services.Preferences.Changed -= OnPreferencesChanged;
            }
        }

        public void Open()
        {
            if (services != null && services.Preferences != null)
            {
                previewedCrewId = BattleCrewId.Normalize(services.Preferences.SelectedCrewId);
            }
            Show(true);
            Refresh();
        }

        public void Close()
        {
            Show(false);
        }

        public void SelectCrew(string crewId)
        {
            previewedCrewId = BattleCrewId.Normalize(crewId);
            Refresh();
        }

        public void DeployCurrentPreview()
        {
            if (services == null || services.Preferences == null) return;
            services.Preferences.SelectedCrewId = previewedCrewId;
            Refresh();
        }

        private void Show(bool visible)
        {
            isOpen = visible;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }
            else
            {
                gameObject.SetActive(visible);
            }
        }

        private void Update()
        {
            if (!isOpen) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        private void OnPreferencesChanged()
        {
            if (isOpen) Refresh();
        }

        public void Refresh()
        {
            if (catalog == null) return;

            string deployedId = services != null && services.Preferences != null
                ? BattleCrewId.Normalize(services.Preferences.SelectedCrewId)
                : BattleCrewId.Default;

            BattleCrewData previewData = catalog.GetCrewOrDefault(previewedCrewId);
            if (previewData != null)
            {
                if (crewNameLabel != null) crewNameLabel.text = previewData.DisplayName;
                if (crewRoleLabel != null) crewRoleLabel.text = previewData.RoleTitle;
                if (previewCrewView != null) previewCrewView.Bind(previewData);
            }

            bool isCurrentlyDeployed = string.Equals(previewedCrewId, deployedId, StringComparison.OrdinalIgnoreCase);
            if (deployButton != null)
            {
                deployButton.interactable = !isCurrentlyDeployed;
            }
            if (deployButtonLabel != null)
            {
                deployButtonLabel.text = isCurrentlyDeployed ? "출격 중" : "출격";
            }

            UpdateRosterUI(deployedId);
        }

        private void UpdateRosterUI(string deployedId)
        {
            if (crewSelectButtons == null) return;

            for (int i = 0; i < crewSelectButtons.Length; i++)
            {
                if (i >= BattleCrewId.All.Count) break;
                string id = BattleCrewId.All[i];
                BattleCrewData data = catalog.GetCrewOrDefault(id);

                if (crewSelectLabels != null && i < crewSelectLabels.Length && crewSelectLabels[i] != null && data != null)
                {
                    crewSelectLabels[i].text = data.DisplayName;
                }

                if (crewSelectIcons != null && i < crewSelectIcons.Length && crewSelectIcons[i] != null && data != null && data.PreviewIcon != null)
                {
                    crewSelectIcons[i].sprite = data.PreviewIcon;
                }

                bool isDeployed = string.Equals(id, deployedId, StringComparison.OrdinalIgnoreCase);
                if (deployedBadges != null && i < deployedBadges.Length && deployedBadges[i] != null)
                {
                    deployedBadges[i].SetActive(isDeployed);
                }

                bool isSelected = string.Equals(id, previewedCrewId, StringComparison.OrdinalIgnoreCase);
                if (selectedHighlights != null && i < selectedHighlights.Length && selectedHighlights[i] != null)
                {
                    selectedHighlights[i].SetActive(isSelected);
                }
            }
        }

        private void HookEvents()
        {
            closeButton?.onClick.AddListener(Close);
            backgroundDismissButton?.onClick.AddListener(Close);
            deployButton?.onClick.AddListener(DeployCurrentPreview);

            excitedTestButton?.onClick.AddListener(OnExcitedTestClicked);
            badTestButton?.onClick.AddListener(OnBadTestClicked);
            idleTestButton?.onClick.AddListener(OnIdleTestClicked);

            if (crewSelectButtons != null)
            {
                for (int i = 0; i < crewSelectButtons.Length; i++)
                {
                    int index = i;
                    if (index < BattleCrewId.All.Count && crewSelectButtons[i] != null)
                    {
                        string id = BattleCrewId.All[index];
                        crewSelectButtons[i].onClick.AddListener(() => SelectCrew(id));
                    }
                }
            }
        }

        private void UnhookEvents()
        {
            closeButton?.onClick.RemoveListener(Close);
            backgroundDismissButton?.onClick.RemoveListener(Close);
            deployButton?.onClick.RemoveListener(DeployCurrentPreview);

            excitedTestButton?.onClick.RemoveListener(OnExcitedTestClicked);
            badTestButton?.onClick.RemoveListener(OnBadTestClicked);
            idleTestButton?.onClick.RemoveListener(OnIdleTestClicked);

            if (crewSelectButtons != null)
            {
                for (int i = 0; i < crewSelectButtons.Length; i++)
                {
                    if (crewSelectButtons[i] != null)
                    {
                        crewSelectButtons[i].onClick.RemoveAllListeners();
                    }
                }
            }
        }

        private void OnExcitedTestClicked() => previewCrewView?.PlayExcited();
        private void OnBadTestClicked() => previewCrewView?.PlayBad();
        private void OnIdleTestClicked() => previewCrewView?.PlayIdle();
    }
}
