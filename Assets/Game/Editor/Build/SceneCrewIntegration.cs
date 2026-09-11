using System;
using System.IO;
using System.Linq;
using DJMaximusKaiserSoje.Core;
using DJMaximusKaiserSoje.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Editor
{
    public static class SceneCrewIntegration
    {
        public const string CatalogPath = "Assets/Game/Content/BattleCrewCatalog.asset";
        public const string ModalPrefabPath = "Assets/Game/UI/Prefabs/CrewRoomModal.prefab";

        private const string BoldFontPath = "Assets/Game/UI/Fonts/TMP/Pretendard-Bold SDF.asset";
        private const string MediumFontPath = "Assets/Game/UI/Fonts/TMP/Pretendard-Medium SDF.asset";
        private const string PanelSpritePath = "Assets/Game/UI/Art/Chrome/Panel.png";
        private const string SolidSpritePath = "Assets/Game/UI/Art/Chrome/Solid.png";
        private const string BarSpritePath = "Assets/Game/UI/Art/Chrome/Bar.png";

        [MenuItem("Tools/DJ Maximus/Integrate All Crew UI and Scenes")]
        public static void IntegrateAll()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BattleCrewCatalog>(CatalogPath);
            if (catalog == null)
            {
                Content.BattleCrewCatalogGenerator.Generate();
                catalog = AssetDatabase.LoadAssetAtPath<BattleCrewCatalog>(CatalogPath);
            }

            var modalPrefab = BuildCrewModalPrefab(catalog);
            IntegrateGameplay(catalog);
            IntegrateSongSelect(catalog, modalPrefab);
            IntegrateTitle(catalog, modalPrefab);

            AssetDatabase.SaveAssets();
            Debug.Log("[SceneCrewIntegration] Completed full integration of Battle Crew into Gameplay, SongSelect, and Title scenes.");
        }

        public static void IntegrateGameplay(BattleCrewCatalog catalog)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Gameplay.unity", OpenSceneMode.Single);
            var screen = UnityEngine.Object.FindAnyObjectByType<GameplayScreenView>();
            if (screen == null)
            {
                Debug.LogError("GameplayScreenView not found in Gameplay.unity");
                return;
            }

            var mascot = GameObject.Find("Mascot");
            if (mascot != null)
            {
                var crewView = mascot.GetComponent<BattleCrewView>();
                if (crewView == null) crewView = mascot.AddComponent<BattleCrewView>();
                crewView.displayImage = mascot.GetComponent<Image>();

                var defaultCrew = catalog != null ? catalog.GetCrewOrDefault(BattleCrewId.Default) : null;
                if (defaultCrew != null) crewView.Bind(defaultCrew);

                screen.battleCrewView = crewView;
                screen.battleCrewCatalog = catalog;
                EditorUtility.SetDirty(mascot);
            }

            EditorUtility.SetDirty(screen);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SceneCrewIntegration] Gameplay.unity updated with BattleCrewView.");
        }

        public static GameObject BuildCrewModalPrefab(BattleCrewCatalog catalog)
        {
            var boldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);
            var medFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MediumFontPath);
            var panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSpritePath);
            var solidSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SolidSpritePath);
            var barSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BarSpritePath);

            var rootGo = new GameObject("CrewRoomModal", typeof(RectTransform), typeof(CanvasGroup), typeof(CrewRoomModalView));
            var rootRt = rootGo.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var cg = rootGo.GetComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;

            var modalView = rootGo.GetComponent<CrewRoomModalView>();
            modalView.canvasGroup = cg;

            // Background backdrop
            var bgGo = new GameObject("Backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            bgGo.transform.SetParent(rootGo.transform, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGo.GetComponent<Image>();
            bgImg.sprite = solidSprite;
            bgImg.color = new Color32(0x06, 0x08, 0x16, 0xDD); // Semi-transparent dark
            modalView.backgroundDismissButton = bgGo.GetComponent<Button>();

            // Center Dialog Window
            var winGo = new GameObject("Window", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            winGo.transform.SetParent(rootGo.transform, false);
            var winRt = winGo.GetComponent<RectTransform>();
            winRt.anchorMin = new Vector2(0.5f, 0.5f);
            winRt.anchorMax = new Vector2(0.5f, 0.5f);
            winRt.pivot = new Vector2(0.5f, 0.5f);
            winRt.sizeDelta = new Vector2(1040f, 620f);
            var winImg = winGo.GetComponent<Image>();
            winImg.sprite = panelSprite;
            winImg.type = Image.Type.Sliced;
            winImg.color = UiPalette.Panel;

            // Header Title
            var titleGo = new GameObject("TitleLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(winGo.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(0f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.anchoredPosition = new Vector2(36f, -24f);
            titleRt.sizeDelta = new Vector2(300f, 40f);
            var titleText = titleGo.GetComponent<TextMeshProUGUI>();
            titleText.font = boldFont;
            titleText.fontSize = 24f;
            titleText.color = UiPalette.Cyan;
            titleText.text = "리듬 크루";
            modalView.titleLabel = titleText;

            // Close Button
            var closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(winGo.transform, false);
            var closeRt = closeGo.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-28f, -24f);
            closeRt.sizeDelta = new Vector2(90f, 36f);
            var closeImg = closeGo.GetComponent<Image>();
            closeImg.sprite = barSprite;
            closeImg.type = Image.Type.Sliced;
            closeImg.color = UiPalette.PanelRaised;
            modalView.closeButton = closeGo.GetComponent<Button>();

            var closeLblGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            closeLblGo.transform.SetParent(closeGo.transform, false);
            var closeLblRt = closeLblGo.GetComponent<RectTransform>();
            closeLblRt.anchorMin = Vector2.zero;
            closeLblRt.anchorMax = Vector2.one;
            closeLblRt.offsetMin = Vector2.zero;
            closeLblRt.offsetMax = Vector2.zero;
            var closeLbl = closeLblGo.GetComponent<TextMeshProUGUI>();
            closeLbl.font = medFont;
            closeLbl.fontSize = 14f;
            closeLbl.alignment = TextAlignmentOptions.Center;
            closeLbl.color = UiPalette.TextSecondary;
            closeLbl.text = "닫기";

            // Left Panel: Crew Preview Area
            var leftGo = new GameObject("PreviewSection", typeof(RectTransform));
            leftGo.transform.SetParent(winGo.transform, false);
            var leftRt = leftGo.GetComponent<RectTransform>();
            leftRt.anchorMin = new Vector2(0f, 0f);
            leftRt.anchorMax = new Vector2(0.5f, 1f);
            leftRt.offsetMin = new Vector2(30f, 20f);
            leftRt.offsetMax = new Vector2(-15f, -70f);

            // Animated character image
            var charGo = new GameObject("CharacterImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(BattleCrewView));
            charGo.transform.SetParent(leftGo.transform, false);
            var charRt = charGo.GetComponent<RectTransform>();
            charRt.anchorMin = new Vector2(0.5f, 0.5f);
            charRt.anchorMax = new Vector2(0.5f, 0.5f);
            charRt.pivot = new Vector2(0.5f, 0.5f);
            charRt.anchoredPosition = new Vector2(0f, 40f);
            charRt.sizeDelta = new Vector2(300f, 360f);
            var charImg = charGo.GetComponent<Image>();
            charImg.preserveAspect = true;
            var charCrewView = charGo.GetComponent<BattleCrewView>();
            charCrewView.displayImage = charImg;
            modalView.previewCrewView = charCrewView;

            // Character Name Label
            var nameGo = new GameObject("CrewName", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(leftGo.transform, false);
            var nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 0f);
            nameRt.anchorMax = new Vector2(1f, 0f);
            nameRt.pivot = new Vector2(0.5f, 0f);
            nameRt.anchoredPosition = new Vector2(0f, 108f);
            nameRt.sizeDelta = new Vector2(0f, 32f);
            var nameText = nameGo.GetComponent<TextMeshProUGUI>();
            nameText.font = boldFont;
            nameText.fontSize = 22f;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.color = UiPalette.TextPrimary;
            nameText.text = "아이라";
            modalView.crewNameLabel = nameText;

            // Character Role Label
            var roleGo = new GameObject("CrewRole", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            roleGo.transform.SetParent(leftGo.transform, false);
            var roleRt = roleGo.GetComponent<RectTransform>();
            roleRt.anchorMin = new Vector2(0f, 0f);
            roleRt.anchorMax = new Vector2(1f, 0f);
            roleRt.pivot = new Vector2(0.5f, 0f);
            roleRt.anchoredPosition = new Vector2(0f, 82f);
            roleRt.sizeDelta = new Vector2(0f, 24f);
            var roleText = roleGo.GetComponent<TextMeshProUGUI>();
            roleText.font = medFont;
            roleText.fontSize = 14f;
            roleText.alignment = TextAlignmentOptions.Center;
            roleText.color = UiPalette.TextSecondary;
            roleText.text = "신디사이저 비트마스터";
            modalView.crewRoleLabel = roleText;

            // Deploy Button
            var depGo = new GameObject("DeployButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            depGo.transform.SetParent(leftGo.transform, false);
            var depRt = depGo.GetComponent<RectTransform>();
            depRt.anchorMin = new Vector2(0.5f, 0f);
            depRt.anchorMax = new Vector2(0.5f, 0f);
            depRt.pivot = new Vector2(0.5f, 0f);
            depRt.anchoredPosition = new Vector2(0f, 22f);
            depRt.sizeDelta = new Vector2(220f, 48f);
            var depImg = depGo.GetComponent<Image>();
            depImg.sprite = barSprite;
            depImg.type = Image.Type.Sliced;
            depImg.color = UiPalette.Cyan;
            modalView.deployButton = depGo.GetComponent<Button>();

            var depLblGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            depLblGo.transform.SetParent(depGo.transform, false);
            var depLblRt = depLblGo.GetComponent<RectTransform>();
            depLblRt.anchorMin = Vector2.zero;
            depLblRt.anchorMax = Vector2.one;
            depLblRt.offsetMin = Vector2.zero;
            depLblRt.offsetMax = Vector2.zero;
            var depLbl = depLblGo.GetComponent<TextMeshProUGUI>();
            depLbl.font = boldFont;
            depLbl.fontSize = 18f;
            depLbl.alignment = TextAlignmentOptions.Center;
            depLbl.color = UiPalette.Night;
            depLbl.text = "출격";
            modalView.deployButtonLabel = depLbl;

            // Right Panel: Roster Selection List
            var rightGo = new GameObject("RosterSection", typeof(RectTransform));
            rightGo.transform.SetParent(winGo.transform, false);
            var rightRt = rightGo.GetComponent<RectTransform>();
            rightRt.anchorMin = new Vector2(0.5f, 0f);
            rightRt.anchorMax = new Vector2(1f, 1f);
            rightRt.offsetMin = new Vector2(15f, 20f);
            rightRt.offsetMax = new Vector2(-30f, -70f);

            var crewButtons = new Button[4];
            var crewLabels = new TMP_Text[4];
            var crewIcons = new Image[4];
            var deployedBadges = new GameObject[4];
            var selectedHighlights = new GameObject[4];

            string[] names = { "아이라", "카이", "레나", "렌" };
            float rowHeight = 96f;
            float rowSpacing = 16f;

            for (int i = 0; i < 4; i++)
            {
                var cardGo = new GameObject("CrewCard_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                cardGo.transform.SetParent(rightGo.transform, false);
                var cardRt = cardGo.GetComponent<RectTransform>();
                cardRt.anchorMin = new Vector2(0f, 1f);
                cardRt.anchorMax = new Vector2(1f, 1f);
                cardRt.pivot = new Vector2(0.5f, 1f);
                cardRt.anchoredPosition = new Vector2(0f, -(i * (rowHeight + rowSpacing)));
                cardRt.sizeDelta = new Vector2(0f, rowHeight);

                var cardImg = cardGo.GetComponent<Image>();
                cardImg.sprite = barSprite;
                cardImg.type = Image.Type.Sliced;
                cardImg.color = UiPalette.PanelSoft;
                crewButtons[i] = cardGo.GetComponent<Button>();

                // Highlight border
                var hlGo = new GameObject("Highlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                hlGo.transform.SetParent(cardGo.transform, false);
                var hlRt = hlGo.GetComponent<RectTransform>();
                hlRt.anchorMin = Vector2.zero;
                hlRt.anchorMax = Vector2.one;
                hlRt.offsetMin = new Vector2(-2f, -2f);
                hlRt.offsetMax = new Vector2(2f, 2f);
                var hlImg = hlGo.GetComponent<Image>();
                hlImg.sprite = barSprite;
                hlImg.type = Image.Type.Sliced;
                hlImg.color = UiPalette.Cyan;
                hlGo.SetActive(i == 0);
                selectedHighlights[i] = hlGo;

                // Avatar Icon
                var iconGo = new GameObject("AvatarIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGo.transform.SetParent(cardGo.transform, false);
                var iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0f, 0.5f);
                iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.pivot = new Vector2(0f, 0.5f);
                iconRt.anchoredPosition = new Vector2(16f, 0f);
                iconRt.sizeDelta = new Vector2(72f, 72f);
                var iconImg = iconGo.GetComponent<Image>();
                iconImg.preserveAspect = true;
                crewIcons[i] = iconImg;

                // Name
                var cNameGo = new GameObject("Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                cNameGo.transform.SetParent(cardGo.transform, false);
                var cNameRt = cNameGo.GetComponent<RectTransform>();
                cNameRt.anchorMin = new Vector2(0f, 0.5f);
                cNameRt.anchorMax = new Vector2(0f, 0.5f);
                cNameRt.pivot = new Vector2(0f, 0.5f);
                cNameRt.anchoredPosition = new Vector2(104f, 0f);
                cNameRt.sizeDelta = new Vector2(180f, 32f);
                var cNameText = cNameGo.GetComponent<TextMeshProUGUI>();
                cNameText.font = boldFont;
                cNameText.fontSize = 20f;
                cNameText.color = UiPalette.TextPrimary;
                cNameText.text = names[i];
                crewLabels[i] = cNameText;

                // Deployed Badge
                var badgeGo = new GameObject("DeployedBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                badgeGo.transform.SetParent(cardGo.transform, false);
                var badgeRt = badgeGo.GetComponent<RectTransform>();
                badgeRt.anchorMin = new Vector2(1f, 0.5f);
                badgeRt.anchorMax = new Vector2(1f, 0.5f);
                badgeRt.pivot = new Vector2(1f, 0.5f);
                badgeRt.anchoredPosition = new Vector2(-20f, 0f);
                badgeRt.sizeDelta = new Vector2(90f, 32f);
                var badgeImg = badgeGo.GetComponent<Image>();
                badgeImg.sprite = barSprite;
                badgeImg.type = Image.Type.Sliced;
                badgeImg.color = UiPalette.Mint;

                var badgeLblGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                badgeLblGo.transform.SetParent(badgeGo.transform, false);
                var badgeLblRt = badgeLblGo.GetComponent<RectTransform>();
                badgeLblRt.anchorMin = Vector2.zero;
                badgeLblRt.anchorMax = Vector2.one;
                badgeLblRt.offsetMin = Vector2.zero;
                badgeLblRt.offsetMax = Vector2.zero;
                var badgeLbl = badgeLblGo.GetComponent<TextMeshProUGUI>();
                badgeLbl.font = boldFont;
                badgeLbl.fontSize = 14f;
                badgeLbl.alignment = TextAlignmentOptions.Center;
                badgeLbl.color = UiPalette.Night;
                badgeLbl.text = "출격 중";

                badgeGo.SetActive(i == 0);
                deployedBadges[i] = badgeGo;
            }

            modalView.crewSelectButtons = crewButtons;
            modalView.crewSelectLabels = crewLabels;
            modalView.crewSelectIcons = crewIcons;
            modalView.deployedBadges = deployedBadges;
            modalView.selectedHighlights = selectedHighlights;

            Directory.CreateDirectory("Assets/Game/UI/Prefabs");
            var prefab = PrefabUtility.SaveAsPrefabAsset(rootGo, ModalPrefabPath);
            UnityEngine.Object.DestroyImmediate(rootGo);
            Debug.Log("[SceneCrewIntegration] Created prefab " + ModalPrefabPath);
            return prefab;
        }

        public static void IntegrateSongSelect(BattleCrewCatalog catalog, GameObject modalPrefab)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SongSelect.unity", OpenSceneMode.Single);
            var screen = UnityEngine.Object.FindAnyObjectByType<SongSelectScreenView>();
            if (screen == null)
            {
                Debug.LogError("SongSelectScreenView not found in SongSelect.unity");
                return;
            }

            screen.battleCrewCatalog = catalog;

            // Ensure modal instance exists
            var canvas = screen.GetComponentInParent<Canvas>() ?? UnityEngine.Object.FindAnyObjectByType<Canvas>();
            var existingModal = screen.GetComponentInChildren<CrewRoomModalView>(true) ?? canvas?.GetComponentInChildren<CrewRoomModalView>(true);
            if (existingModal == null && modalPrefab != null && canvas != null)
            {
                var modalInstance = (GameObject)PrefabUtility.InstantiatePrefab(modalPrefab, canvas.transform);
                modalInstance.name = "CrewRoomModal";
                existingModal = modalInstance.GetComponent<CrewRoomModalView>();
            }
            screen.crewModal = existingModal;

            // Add Crew button alongside OptionsButton if not present
            if (screen.optionsButton != null)
            {
                var optionsGo = screen.optionsButton.gameObject;
                var parent = optionsGo.transform.parent;
                var existingCrewBtn = parent.Find("CrewButton");
                GameObject crewBtnGo;
                if (existingCrewBtn == null)
                {
                    crewBtnGo = UnityEngine.Object.Instantiate(optionsGo, parent);
                    crewBtnGo.name = "CrewButton";
                    var rt = crewBtnGo.GetComponent<RectTransform>();
                    var optRt = optionsGo.GetComponent<RectTransform>();
                    rt.anchoredPosition = new Vector2(optRt.anchoredPosition.x - optRt.sizeDelta.x - 14f, optRt.anchoredPosition.y);
                }
                else
                {
                    crewBtnGo = existingCrewBtn.gameObject;
                }

                var btnText = crewBtnGo.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null) btnText.text = "크루";
                screen.crewButton = crewBtnGo.GetComponent<Button>();
            }

            EditorUtility.SetDirty(screen);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SceneCrewIntegration] SongSelect.unity updated with CrewButton and CrewRoomModalView.");
        }

        public static void IntegrateTitle(BattleCrewCatalog catalog, GameObject modalPrefab)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Title.unity", OpenSceneMode.Single);
            var screen = UnityEngine.Object.FindAnyObjectByType<TitleScreenView>();
            if (screen == null)
            {
                Debug.LogError("TitleScreenView not found in Title.unity");
                return;
            }

            screen.battleCrewCatalog = catalog;

            var canvas = screen.GetComponentInParent<Canvas>() ?? UnityEngine.Object.FindAnyObjectByType<Canvas>();
            var existingModal = screen.GetComponentInChildren<CrewRoomModalView>(true) ?? canvas?.GetComponentInChildren<CrewRoomModalView>(true);
            if (existingModal == null && modalPrefab != null && canvas != null)
            {
                var modalInstance = (GameObject)PrefabUtility.InstantiatePrefab(modalPrefab, canvas.transform);
                modalInstance.name = "CrewRoomModal";
                existingModal = modalInstance.GetComponent<CrewRoomModalView>();
            }
            screen.crewModal = existingModal;

            if (screen.optionsButton != null)
            {
                var optionsGo = screen.optionsButton.gameObject;
                var parent = optionsGo.transform.parent;
                var existingCrewBtn = parent.Find("CrewButton");
                GameObject crewBtnGo;
                if (existingCrewBtn == null)
                {
                    crewBtnGo = UnityEngine.Object.Instantiate(optionsGo, parent);
                    crewBtnGo.name = "CrewButton";
                    var rt = crewBtnGo.GetComponent<RectTransform>();
                    var optRt = optionsGo.GetComponent<RectTransform>();
                    rt.anchoredPosition = new Vector2(optRt.anchoredPosition.x - optRt.sizeDelta.x - 14f, optRt.anchoredPosition.y);
                }
                else
                {
                    crewBtnGo = existingCrewBtn.gameObject;
                }

                var btnText = crewBtnGo.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null) btnText.text = "크루 룸";
                screen.crewButton = crewBtnGo.GetComponent<Button>();
            }

            EditorUtility.SetDirty(screen);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SceneCrewIntegration] Title.unity updated with CrewButton and CrewRoomModalView.");
        }
    }
}
