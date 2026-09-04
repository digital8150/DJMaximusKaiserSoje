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
    /// Song list on the right, the highlighted chart and the player's best on the left. Highlighting
    /// a song asks the music director for its preview; the director decides when to start it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SongSelectScreenView : MonoBehaviour, ISongSelectScreenView
    {
        private static readonly DifficultyTier[] TierOrder =
        {
            DifficultyTier.Easy,
            DifficultyTier.Normal,
            DifficultyTier.Hard,
            DifficultyTier.SuperHard,
            DifficultyTier.Over
        };

        [Header("List")]
        [SerializeField] internal ScrollRect listScroll;
        [SerializeField] internal RectTransform listContent;
        [SerializeField] internal SongRowView rowPrefab;

        [Header("Detail")]
        [SerializeField] internal AddressableImage jacket;
        [SerializeField] internal TMP_Text titleLabel;
        [SerializeField] internal TMP_Text artistLabel;
        [SerializeField] internal TMP_Text bpmLabel;
        [SerializeField] internal TMP_Text categoryLabel;
        [SerializeField] internal TMP_Text difficultyNameLabel;
        [SerializeField] internal TMP_Text levelLabel;
        [SerializeField] internal TMP_Text noteCountLabel;
        [SerializeField] internal DifficultyChipView[] tierChips;

        [Header("Settings")]
        [SerializeField] internal TMP_Text speedLabel;
        [SerializeField] internal Button speedDownButton;
        [SerializeField] internal Button speedUpButton;
        [SerializeField] internal TabButtonView[] styleTabs;

        [Header("Actions")]
        [SerializeField] internal Button playButton;
        [SerializeField] internal RecordPanelView recordPanel;
        [SerializeField] internal TMP_Text keyGuideLabel;
        [SerializeField] internal TMP_Text emptyLibraryLabel;

        private readonly List<SongRowView> rows = new List<SongRowView>();
        private readonly Dictionary<string, DifficultyTier> tierBySong = new Dictionary<string, DifficultyTier>();

        private GameServices services;
        private readonly List<SongSummary> songs = new List<SongSummary>();
        private int selectedIndex;
        private DifficultyTier selectedTier = DifficultyTier.Normal;
        private bool starting;
        private bool hasDisplayedStyle;
        private PlayStyle displayedStyle;

        public void Bind(GameServices gameServices)
        {
            services = gameServices;
            starting = false;

            HookButtons();
            services.Preferences.Changed += OnPreferencesChanged;

            if (keyGuideLabel != null)
                keyGuideLabel.text = "↑↓ 곡 고르기    ←→ 난이도    Enter 시작    Tab 키 모드    F1 / F2 노트 속도    Esc 뒤로";

            services.Music.PlayTheme(ScreenTheme.SongSelect);
            OnPreferencesChanged();
        }

        public void Focus(string songId, string chartId)
        {
            if (songs == null) return;
            for (int index = 0; index < songs.Count; index++)
            {
                if (songs[index].Id != songId) continue;
                if (services.Songs.TryGetChart(chartId, out var chart)) selectedTier = chart.Tier;
                Select(index, force: true);
                return;
            }
        }

        private void OnDestroy()
        {
            if (services != null) services.Preferences.Changed -= OnPreferencesChanged;
        }

        private void BuildRows()
        {
            foreach (var row in rows) if (row != null) Destroy(row.gameObject);
            rows.Clear();
            if (rowPrefab == null || listContent == null) return;

            for (int index = 0; index < songs.Count; index++)
            {
                var row = Instantiate(rowPrefab, listContent);
                row.gameObject.SetActive(true);
                row.name = "SongRow" + index;
                row.Bind(index, songs[index], displayedStyle, OnRowSelected, OnRowCommitted);
                rows.Add(row);
            }

            // Positions are read back when keeping the highlighted row in view, so settle the
            // layout now rather than a frame later.
            LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
        }

        private void HookButtons()
        {
            if (playButton != null) playButton.onClick.AddListener(StartSelected);
            if (speedDownButton != null) speedDownButton.onClick.AddListener(() => NudgeSpeed(-1));
            if (speedUpButton != null) speedUpButton.onClick.AddListener(() => NudgeSpeed(1));

            if (styleTabs == null) return;
            for (int index = 0; index < styleTabs.Length; index++)
                styleTabs[index].Bind(index, UiNaming.StyleShortName((PlayStyle)index), OnStyleTabClicked);

            if (tierChips == null) return;
            for (int index = 0; index < tierChips.Length && index < TierOrder.Length; index++)
                tierChips[index].Bind(TierOrder[index], null, SelectTier);
        }

        private void Update()
        {
            if (services == null || starting) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f1Key.wasPressedThisFrame) NudgeSpeed(-1);
            if (keyboard.f2Key.wasPressedThisFrame) NudgeSpeed(1);
            if (keyboard.tabKey.wasPressedThisFrame) CycleStyle();
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                LeaveToTitle();
                return;
            }
            if (songs.Count == 0) return;

            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) Step(-1);
            if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) Step(1);
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) StepTier(-1);
            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) StepTier(1);
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) StartSelected();
        }

        private void Step(int direction)
        {
            int next = (selectedIndex + direction + songs.Count) % songs.Count;
            Select(next, force: false);
        }

        private void StepTier(int direction)
        {
            var song = songs[selectedIndex];
            int start = Mathf.Max(0, Array.IndexOf(TierOrder, selectedTier));
            for (int offset = 1; offset <= TierOrder.Length; offset++)
            {
                int step = start + direction * offset;
                int index = ((step % TierOrder.Length) + TierOrder.Length) % TierOrder.Length;
                if (!song.TryGetChart(TierOrder[index], displayedStyle, out _)) continue;
                SelectTier(TierOrder[index]);
                return;
            }
        }

        private void OnRowSelected(int index) => Select(index, force: false);

        private void OnRowCommitted(int index)
        {
            Select(index, force: false);
            StartSelected();
        }

        private void Select(int index, bool force)
        {
            if (songs == null || songs.Count == 0) return;
            index = Mathf.Clamp(index, 0, songs.Count - 1);
            if (!force && index == selectedIndex)
            {
                RefreshDetail();
                return;
            }

            selectedIndex = index;
            var song = songs[index];

            if (tierBySong.TryGetValue(song.Id, out var remembered) &&
                song.TryGetChart(remembered, displayedStyle, out _))
                selectedTier = remembered;
            else if (!song.TryGetChart(selectedTier, displayedStyle, out _))
                selectedTier = FirstAvailableTier(song);

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                rows[rowIndex].SetSelected(rowIndex == selectedIndex);

            EnsureVisible(selectedIndex);
            RefreshDetail();
            services.Music.RequestSongPreview(song.Id);
        }

        private DifficultyTier FirstAvailableTier(SongSummary song)
        {
            for (int index = 0; index < TierOrder.Length; index++)
                if (song.TryGetChart(TierOrder[index], displayedStyle, out _))
                    return TierOrder[index];
            return DifficultyTier.Normal;
        }

        private void SelectTier(DifficultyTier tier)
        {
            var song = songs[selectedIndex];
            if (!song.TryGetChart(tier, displayedStyle, out _)) return;

            selectedTier = tier;
            tierBySong[song.Id] = tier;
            RefreshDetail();
        }

        private void RefreshDetail()
        {
            if (songs == null || songs.Count == 0) return;
            var song = songs[selectedIndex];
            song.TryGetChart(selectedTier, displayedStyle, out var chart);

            if (jacket != null) jacket.Show(song.JacketAddress);
            if (titleLabel != null) titleLabel.text = song.Title;
            if (artistLabel != null) artistLabel.text = song.Artist;
            if (bpmLabel != null) bpmLabel.text = UiFormat.Bpm(song.Bpm);
            if (categoryLabel != null) categoryLabel.text = song.Category;

            if (difficultyNameLabel != null)
                difficultyNameLabel.text = chart == null ? UiNaming.TierName(selectedTier) : chart.DifficultyName;
            if (levelLabel != null)
            {
                levelLabel.text = chart == null ? "–" : chart.Level.ToString();
                levelLabel.color = UiPalette.TierColor(selectedTier);
            }

            if (noteCountLabel != null)
                noteCountLabel.text = chart == null ? "–" : chart.NoteCount.ToString("N0") + " NOTES";

            if (tierChips != null)
            {
                for (int index = 0; index < tierChips.Length && index < TierOrder.Length; index++)
                {
                    var tier = TierOrder[index];
                    song.TryGetChart(tier, displayedStyle, out var tierChart);
                    tierChips[index].Bind(tier, tierChart, SelectTier);
                    tierChips[index].SetSelected(tier == selectedTier);
                }
            }

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++) rows[rowIndex].HighlightTier(selectedTier);

            if (recordPanel != null)
                recordPanel.Bind(chart == null ? null : services.Records.Get(chart.Id));

            if (playButton != null) playButton.interactable = chart != null;
        }

        private void EnsureVisible(int index)
        {
            if (listScroll == null || listContent == null || rows.Count <= 1) return;

            var viewport = listScroll.viewport == null ? listScroll.GetComponent<RectTransform>() : listScroll.viewport;
            float contentHeight = listContent.rect.height;
            float viewportHeight = viewport.rect.height;
            if (contentHeight <= viewportHeight) return;

            // Row anchored positions are negative going down from the content's top edge.
            float rowCenter = -rows[index].root.anchoredPosition.y + rows[index].root.rect.height * 0.5f;
            float scrollable = contentHeight - viewportHeight;
            float target = Mathf.Clamp01((rowCenter - viewportHeight * 0.5f) / scrollable);
            listScroll.verticalNormalizedPosition = 1f - target;
        }

        private void NudgeSpeed(int steps)
        {
            services.Preferences.ScrollSpeed = ScrollSpeedRange.Stepped(services.Preferences.ScrollSpeed, steps);
        }

        private void CycleStyle()
        {
            int count = Enum.GetValues(typeof(PlayStyle)).Length;
            services.Preferences.PlayStyle = (PlayStyle)(((int)services.Preferences.PlayStyle + 1) % count);
        }

        private void OnStyleTabClicked(int index) => services.Preferences.PlayStyle = (PlayStyle)index;

        private void OnPreferencesChanged()
        {
            if (speedLabel != null) speedLabel.text = UiFormat.Speed(services.Preferences.ScrollSpeed);

            int selected = (int)services.Preferences.PlayStyle;
            if (styleTabs != null)
                for (int index = 0; index < styleTabs.Length; index++)
                    styleTabs[index].SetSelected(index == selected);

            if (!hasDisplayedStyle || displayedStyle != services.Preferences.PlayStyle)
                ApplyStyleFilter(services.Preferences.PlayStyle);
        }

        private void ApplyStyleFilter(PlayStyle style)
        {
            string selectedSongId = songs.Count > 0 && selectedIndex >= 0 && selectedIndex < songs.Count
                ? songs[selectedIndex].Id
                : null;

            displayedStyle = style;
            hasDisplayedStyle = true;
            songs.Clear();
            IReadOnlyList<SongSummary> librarySongs = services.Songs.Songs;
            for (int index = 0; index < librarySongs.Count; index++)
                if (PlayStyleChartCompatibility.IsCompatible(style, librarySongs[index]))
                    songs.Add(librarySongs[index]);

            selectedIndex = 0;
            if (selectedSongId != null)
                for (int index = 0; index < songs.Count; index++)
                    if (songs[index].Id == selectedSongId)
                    {
                        selectedIndex = index;
                        break;
                    }

            BuildRows();
            if (emptyLibraryLabel != null) emptyLibraryLabel.gameObject.SetActive(songs.Count == 0);
            if (songs.Count == 0)
            {
                services.Music.CancelSongPreview();
                if (playButton != null) playButton.interactable = false;
                return;
            }

            Select(selectedIndex, force: true);
        }

        private void StartSelected()
        {
            if (starting || songs == null || songs.Count == 0) return;
            var song = songs[selectedIndex];
            if (!song.TryGetChart(selectedTier, displayedStyle, out var chart)) return;

            starting = true;
            services.Music.CancelSongPreview();
            services.Music.StopTheme();
            services.Flow.StartPlay(new PlayRequest(
                song.Id,
                chart.Id,
                services.Preferences.PlayStyle,
                services.Preferences.ScrollSpeed,
                services.Preferences.JudgementOffsetMs));
        }

        private void LeaveToTitle()
        {
            starting = true;
            services.Music.CancelSongPreview();
            services.Flow.ShowTitle();
        }
    }
}
