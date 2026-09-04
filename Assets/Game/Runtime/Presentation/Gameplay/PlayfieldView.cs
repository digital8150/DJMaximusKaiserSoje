using System.Collections.Generic;
using DJMaximusKaiserSoje.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Presentation
{
    /// <summary>
    /// Draws the lanes and the notes falling down them. It owns no game state: every frame it reads
    /// the session's pending notes and places a pooled sprite for each one that is on screen.
    ///
    /// Lane geometry is measured from the viewport rather than fixed, because the viewport runs off
    /// the top of the screen so notes arrive from outside it rather than appearing inside a box.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayfieldView : MonoBehaviour
    {
        [Header("Layers")]
        [SerializeField] internal RectTransform laneViewport;
        [SerializeField] internal RectTransform laneLayer;
        [SerializeField] internal RectTransform beamLayer;
        [SerializeField] internal RectTransform noteLayer;
        [SerializeField] internal RectTransform fxNoteLayer;

        [Tooltip("Outside the viewport mask so hit bursts are not clipped at the judgement line.")]
        [SerializeField] internal RectTransform burstLayer;

        [Tooltip("Outside the viewport mask; holds the key caps under the judgement line.")]
        [SerializeField] internal RectTransform deckLayer;

        [SerializeField] internal RectTransform judgementBar;
        [SerializeField] internal RectTransform judgementGlow;

        [Header("Sprites")]
        [SerializeField] internal Sprite normalNoteSprite;
        [SerializeField] internal Sprite fxNoteSprite;
        [SerializeField] internal Sprite keyBeamSprite;
        [SerializeField] internal Sprite keyBurstSprite;
        [SerializeField] internal Sprite lanePlateSprite;
        [SerializeField] internal Sprite laneGuideSprite;
        [SerializeField] internal Sprite keyCapSprite;
        [SerializeField] internal TMP_FontAsset keyFont;

        [Header("Geometry")]
        [SerializeField] internal float notePadding = 10f;
        [SerializeField] internal float noteHeight = 30f;
        [SerializeField] internal float keyCapHeight = 96f;

        [Header("Scroll")]
        [SerializeField] internal float basePixelsPerMillisecond = 0.086f;
        [SerializeField] internal float speedMultiplier = 2f;

        [Header("Effects")]
        [SerializeField] internal float burstDuration = 0.24f;
        [SerializeField] internal float beamDecayPerSecond = 5.5f;

        private readonly Dictionary<int, NoteView> viewsByNote = new Dictionary<int, NoteView>();
        private readonly Stack<NoteView> pool = new Stack<NoteView>();
        private readonly List<int> retired = new List<int>();

        private IPlaySession session;
        private LaneLayout layout;
        private Image[] lanePlates;
        private Image[] laneGuides;
        private Image[] receptors;
        private Image[] beams;
        private Image[] bursts;
        private RectTransform[] keyCaps;
        private float[] beamLevel;
        private float[] burstRemaining;
        private bool[] laneHeld;
        private float scrollSpeed = ScrollSpeedRange.Default;
        private float laidOutWidth;
        private float laidOutHeight;
        private int frameStamp;

        private float LaneWidth => laneViewport == null ? 0f : laneViewport.rect.width;

        private float LaneHeight => laneViewport == null ? 0f : laneViewport.rect.height;

        private float PixelsPerMillisecond => basePixelsPerMillisecond * scrollSpeed * speedMultiplier;

        /// <summary>How far ahead a note can be and still land inside the viewport.</summary>
        private double LookaheadMs => (LaneHeight + 160f) / Mathf.Max(0.0001f, PixelsPerMillisecond);

        private sealed class NoteView
        {
            public GameObject GameObject;
            public RectTransform Rect;
            public Image Image;
            public int Stamp;
        }

        public void Bind(IPlaySession playSession, float speed)
        {
            Unbind();

            session = playSession;
            layout = playSession.Layout;
            scrollSpeed = ScrollSpeedRange.Clamp(speed);

            BuildLanes();
            LayoutLanes();

            session.LanePressed += OnLanePressed;
            session.LaneReleased += OnLaneReleased;
        }

        public void SetScrollSpeed(float speed) => scrollSpeed = ScrollSpeedRange.Clamp(speed);

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (session == null) return;
            session.LanePressed -= OnLanePressed;
            session.LaneReleased -= OnLaneReleased;
            session = null;
        }

        // --- Construction ----------------------------------------------------------------------

        private void BuildLanes()
        {
            ClearChildren(laneLayer);
            ClearChildren(beamLayer);
            ClearChildren(noteLayer);
            ClearChildren(fxNoteLayer);
            ClearChildren(burstLayer);
            ClearChildren(deckLayer);
            viewsByNote.Clear();
            pool.Clear();

            lanePlates = new Image[layout.BaseLaneCount];
            laneGuides = new Image[Mathf.Max(0, layout.BaseLaneCount - 1)];

            for (int baseLane = 0; baseLane < layout.BaseLaneCount; baseLane++)
            {
                var plate = NewImage("LanePlate" + baseLane, laneLayer,
                    baseLane % 2 == 0 ? UiPalette.Night.WithAlpha(0.55f) : UiPalette.Ink.WithAlpha(0.6f));
                plate.sprite = lanePlateSprite;
                plate.type = lanePlateSprite == null ? Image.Type.Simple : Image.Type.Sliced;
                lanePlates[baseLane] = plate;

                if (baseLane <= 0) continue;
                var guide = NewImage("LaneGuide" + baseLane, laneLayer, UiPalette.Cyan.WithAlpha(0.22f));
                guide.sprite = laneGuideSprite;
                guide.type = Image.Type.Simple;
                laneGuides[baseLane - 1] = guide;
            }

            int count = layout.Lanes.Count;
            receptors = new Image[count];
            beams = new Image[count];
            bursts = new Image[count];
            keyCaps = new RectTransform[count];
            beamLevel = new float[count];
            burstRemaining = new float[count];
            laneHeld = new bool[count];

            // FX lanes draw over the core lanes, so lay the core ones down first.
            for (int pass = 0; pass < 2; pass++)
            {
                bool fxPass = pass == 1;
                for (int laneIndex = 0; laneIndex < count; laneIndex++)
                {
                    var spec = layout.Lanes[laneIndex];
                    if (spec.IsFx != fxPass) continue;

                    var beam = NewImage("KeyBeam" + laneIndex, beamLayer, Color.white.WithAlpha(0f));
                    beam.sprite = keyBeamSprite;
                    beams[laneIndex] = beam;

                    var burst = NewImage("KeyBurst" + laneIndex, burstLayer, Color.white.WithAlpha(0f));
                    burst.sprite = keyBurstSprite;
                    burst.preserveAspect = true;
                    bursts[laneIndex] = burst;

                    var receptor = NewImage("Receptor" + laneIndex, laneLayer, RestingReceptorColor(spec.IsFx));
                    receptor.sprite = lanePlateSprite;
                    receptor.type = lanePlateSprite == null ? Image.Type.Simple : Image.Type.Sliced;
                    receptors[laneIndex] = receptor;

                    keyCaps[laneIndex] = BuildKeyCap(laneIndex, spec);
                }
            }
        }

        private RectTransform BuildKeyCap(int laneIndex, LaneSpec spec)
        {
            var cap = NewImage("KeyCap" + laneIndex, deckLayer,
                spec.IsFx ? UiPalette.Magenta.WithAlpha(0.2f) : UiPalette.PanelRaised.WithAlpha(0.9f));
            cap.sprite = keyCapSprite;
            cap.type = keyCapSprite == null ? Image.Type.Simple : Image.Type.Sliced;

            var label = NewText("Label", cap.rectTransform,
                spec.KeyName == "Semicolon" ? ";" : spec.KeyName, spec.IsFx ? 17f : 26f, keyFont);
            label.color = spec.IsFx ? UiPalette.Magenta : UiPalette.TextSecondary;
            Stretch(label.rectTransform);

            return cap.rectTransform;
        }

        // --- Layout ----------------------------------------------------------------------------

        /// <summary>
        /// Places everything that depends on the viewport's measured size. Called again whenever the
        /// window changes shape.
        /// </summary>
        private void LayoutLanes()
        {
            if (layout == null || laneViewport == null) return;

            float width = LaneWidth;
            float height = LaneHeight;
            if (width <= 0f || height <= 0f) return;

            laidOutWidth = width;
            laidOutHeight = height;
            float baseWidth = width / layout.BaseLaneCount;

            for (int baseLane = 0; baseLane < lanePlates.Length; baseLane++)
                Place(lanePlates[baseLane].rectTransform, baseLane * baseWidth, 0f, baseWidth, height);

            for (int index = 0; index < laneGuides.Length; index++)
                Place(laneGuides[index].rectTransform, (index + 1) * baseWidth - 1f, 0f, 2f, height);

            for (int laneIndex = 0; laneIndex < layout.Lanes.Count; laneIndex++)
            {
                var spec = layout.Lanes[laneIndex];
                float x = spec.BaseLaneStart * baseWidth;
                float laneSpan = spec.BaseLaneSpan * baseWidth;

                Place(beams[laneIndex].rectTransform, x, 0f, laneSpan, laneSpan * 1.6f);

                float receptorHeight = spec.IsFx ? 18f : 46f;
                Place(receptors[laneIndex].rectTransform, x + 4f, 0f, laneSpan - 8f, receptorHeight);

                float burstSize = Mathf.Min(spec.IsFx ? laneSpan * 0.95f : laneSpan * 1.5f, 260f);
                Centre(bursts[laneIndex].rectTransform, x + laneSpan * 0.5f, 0f, burstSize, burstSize);

                // FX keys sit on a low strip beneath the core key caps rather than over them.
                float capInset = spec.IsFx ? 10f : 5f;
                Place(keyCaps[laneIndex], x + capInset, spec.IsFx ? 0f : 34f,
                    laneSpan - capInset * 2f, spec.IsFx ? 28f : keyCapHeight);
            }

            if (judgementBar != null)
            {
                judgementBar.anchorMin = new Vector2(0.5f, 0f);
                judgementBar.anchorMax = new Vector2(0.5f, 0f);
                judgementBar.pivot = new Vector2(0.5f, 0.5f);
                judgementBar.anchoredPosition = Vector2.zero;
                judgementBar.sizeDelta = new Vector2(width + 12f, 6f);
            }

            if (judgementGlow == null) return;
            judgementGlow.anchorMin = new Vector2(0.5f, 0f);
            judgementGlow.anchorMax = new Vector2(0.5f, 0f);
            judgementGlow.pivot = new Vector2(0.5f, 0.5f);
            judgementGlow.anchoredPosition = Vector2.zero;
            judgementGlow.sizeDelta = new Vector2(width + 160f, 190f);
        }

        private static Color RestingReceptorColor(bool isFx) =>
            isFx ? UiPalette.Magenta.WithAlpha(0.32f) : UiPalette.Cyan.WithAlpha(0.26f);

        // --- Frame -----------------------------------------------------------------------------

        private void Update()
        {
            if (session == null) return;

            if (!Mathf.Approximately(laidOutWidth, LaneWidth) || !Mathf.Approximately(laidOutHeight, LaneHeight))
                LayoutLanes();

            UpdateEffects(Time.unscaledDeltaTime);
            UpdateNotes(session.SongTimeMs);
        }

        private void UpdateNotes(double songTimeMs)
        {
            frameStamp++;
            var pending = session.PendingNotes;
            float baseWidth = LaneWidth / layout.BaseLaneCount;
            double lookahead = LookaheadMs;

            for (int index = 0; index < pending.Count; index++)
            {
                var note = pending[index];
                double lead = note.StartTimeMs - songTimeMs;
                if (lead > lookahead) break;

                var view = Acquire(note);
                view.Stamp = frameStamp;

                var spec = layout.Lanes[Mathf.Clamp(note.Lane, 0, layout.Lanes.Count - 1)];
                float x = spec.BaseLaneStart * baseWidth + notePadding * 0.5f;
                float y = (float)(lead * PixelsPerMillisecond);
                float height = note.IsHold
                    ? Mathf.Max(noteHeight, (float)((note.EndTimeMs - note.StartTimeMs) * PixelsPerMillisecond) + noteHeight)
                    : noteHeight;

                if (note.IsHold && note.HeadJudged)
                {
                    // A held note stops falling: its head stays pinned to the judgement line.
                    float tail = (float)((note.EndTimeMs - songTimeMs) * PixelsPerMillisecond);
                    y = 0f;
                    height = Mathf.Max(noteHeight, tail + noteHeight);
                    view.Image.color = Color.white.WithAlpha(0.62f);
                }
                else
                {
                    view.Image.color = Color.white;
                }

                Place(view.Rect, x, y, spec.BaseLaneSpan * baseWidth - notePadding, height);
            }

            retired.Clear();
            foreach (var pair in viewsByNote)
                if (pair.Value.Stamp != frameStamp)
                    retired.Add(pair.Key);

            for (int index = 0; index < retired.Count; index++) Release(retired[index]);
        }

        private NoteView Acquire(ActiveNote note)
        {
            if (viewsByNote.TryGetValue(note.Id, out var existing)) return existing;

            bool isFx = layout.Lanes[Mathf.Clamp(note.Lane, 0, layout.Lanes.Count - 1)].IsFx;
            var parent = isFx ? fxNoteLayer : noteLayer;

            NoteView view;
            if (pool.Count > 0)
            {
                view = pool.Pop();
                view.Rect.SetParent(parent, false);
                view.GameObject.SetActive(true);
            }
            else
            {
                var image = NewImage("Note", parent, Color.white);
                view = new NoteView { GameObject = image.gameObject, Rect = image.rectTransform, Image = image };
            }

            view.Image.sprite = isFx ? fxNoteSprite : normalNoteSprite;
            view.Image.type = note.IsHold ? Image.Type.Sliced : Image.Type.Simple;
            view.Image.fillCenter = true;
            view.GameObject.name = isFx ? "FxNote" : "Note";
            viewsByNote[note.Id] = view;
            return view;
        }

        private void Release(int noteId)
        {
            if (!viewsByNote.TryGetValue(noteId, out var view)) return;
            viewsByNote.Remove(noteId);
            view.GameObject.SetActive(false);
            pool.Push(view);
        }

        private void OnLanePressed(int lane)
        {
            if (beams == null || lane < 0 || lane >= beams.Length) return;
            laneHeld[lane] = true;
            beamLevel[lane] = 1f;
            burstRemaining[lane] = burstDuration;

            bool isFx = layout.Lanes[lane].IsFx;
            receptors[lane].color = isFx ? UiPalette.Magenta.WithAlpha(0.95f) : UiPalette.Cyan.WithAlpha(0.92f);
            if (keyCaps[lane] != null)
                keyCaps[lane].GetComponent<Image>().color = isFx
                    ? UiPalette.Magenta.WithAlpha(0.6f)
                    : UiPalette.Cyan.WithAlpha(0.55f);
        }

        private void OnLaneReleased(int lane)
        {
            if (beams == null || lane < 0 || lane >= beams.Length) return;
            laneHeld[lane] = false;

            bool isFx = layout.Lanes[lane].IsFx;
            receptors[lane].color = RestingReceptorColor(isFx);
            if (keyCaps[lane] != null)
                keyCaps[lane].GetComponent<Image>().color = isFx
                    ? UiPalette.Magenta.WithAlpha(0.2f)
                    : UiPalette.PanelRaised.WithAlpha(0.9f);
        }

        private void UpdateEffects(float deltaTime)
        {
            if (beams == null) return;

            for (int lane = 0; lane < beams.Length; lane++)
            {
                beamLevel[lane] = laneHeld[lane]
                    ? 1f
                    : Mathf.MoveTowards(beamLevel[lane], 0f, deltaTime * beamDecayPerSecond);
                float alpha = beamLevel[lane] * (layout.Lanes[lane].IsFx ? 0.5f : 0.72f);
                beams[lane].color = Color.white.WithAlpha(alpha);

                if (burstRemaining[lane] <= 0f)
                {
                    bursts[lane].color = Color.white.WithAlpha(0f);
                    continue;
                }

                burstRemaining[lane] = Mathf.Max(0f, burstRemaining[lane] - deltaTime);
                float progress = 1f - burstRemaining[lane] / burstDuration;
                bursts[lane].color = Color.white.WithAlpha(1f - progress);
                bursts[lane].rectTransform.localScale = Vector3.one * Mathf.Lerp(0.55f, 1.35f, progress);
            }
        }

        // --- Helpers ---------------------------------------------------------------------------

        private static void ClearChildren(RectTransform root)
        {
            if (root == null) return;
            for (int index = root.childCount - 1; index >= 0; index--) Destroy(root.GetChild(index).gameObject);
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.layer = parent.gameObject.layer;
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text NewText(string name, Transform parent, string value, float size, TMP_FontAsset font)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = parent.gameObject.layer;
            gameObject.transform.SetParent(parent, false);
            var text = gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Centre(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
