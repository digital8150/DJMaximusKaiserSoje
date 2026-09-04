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
        [SerializeField] internal float geometryScale = 1f;
        [SerializeField] internal float notePadding = 10f;
        [SerializeField] internal float noteHeight = 30f;

        [Tooltip("Where the lettered caps start, measured up from the bottom of the deck band.")]
        [SerializeField] internal float keyCapBottom = 24f;

        [Tooltip("Fallback cap height for when there is no deck band to measure.")]
        [SerializeField] internal float keyCapHeight = 60f;

        [SerializeField] internal float fxKeyCapHeight = 22f;

        [Tooltip("How far up the screen a held key's beam reaches.")]
        [SerializeField] internal float beamScreenFraction = 0.5f;

        [Header("Scroll")]
        [SerializeField] internal float basePixelsPerMillisecond = 0.086f;
        [SerializeField] internal float speedMultiplier = 2f;

        [Header("Effects")]
        [SerializeField] internal float burstDuration = 0.24f;
        [SerializeField] internal float beamDecayPerSecond = 5.5f;
        [SerializeField] internal float holdTickBeamPulseDuration = 0.14f;

        private readonly Dictionary<int, NoteView> viewsByNote = new Dictionary<int, NoteView>();
        private readonly Stack<NoteView> pool = new Stack<NoteView>();
        private readonly List<int> retired = new List<int>();

        private IPlaySession session;
        private LaneLayout layout;
        private IReadOnlyList<string> keyBindings;
        private Image[] lanePlates;
        private Image[] laneGuides;
        private Image[] receptors;
        private Image[] beams;
        private Image[] bursts;
        private RectTransform[] keyCaps;
        private TMP_Text[] keyCapLabels;
        private float[] beamLevel;
        private float[] beamPulseRemaining;
        private float[] burstRemaining;
        private bool[] laneHeld;
        private float scrollSpeed = ScrollSpeedRange.Default;
        private float laidOutWidth;
        private float laidOutHeight;
        private int frameStamp;
        private double lastRenderedSongTimeMs;
        private double rewindInitialOffsetMs;
        private float rewindElapsedSeconds;

        private const float RewindTransitionSeconds = 0.45f;

        private float LaneWidth => laneViewport == null ? 0f : laneViewport.rect.width;

        private float LaneHeight => laneViewport == null ? 0f : laneViewport.rect.height;

        private float GeometryScale => Mathf.Max(0.01f, geometryScale);

        private float PixelsPerMillisecond =>
            basePixelsPerMillisecond * scrollSpeed * speedMultiplier * GeometryScale;

        /// <summary>How far ahead a note can be and still land inside the viewport.</summary>
        private double LookaheadMs =>
            (LaneHeight + 160f * GeometryScale) / Mathf.Max(0.0001f, PixelsPerMillisecond);

        /// <summary>The screen the gear sits on, so a beam can be sized against it rather than the lanes.</summary>
        private float ScreenHeight
        {
            get
            {
                var canvas = laneViewport == null ? null : laneViewport.GetComponentInParent<Canvas>();
                var root = canvas == null ? null : canvas.rootCanvas;
                return root == null ? LaneHeight : ((RectTransform)root.transform).rect.height;
            }
        }

        private sealed class NoteView
        {
            public GameObject GameObject;
            public RectTransform Rect;
            public Image Image;
            public SlicedImageFit Fit;
            public int Stamp;
        }

        /// <param name="bindings">
        /// The keys this run is played with, so a cap shows the key that actually fires its lane.
        /// Falls back to the layout's own defaults when omitted.
        /// </param>
        public void Bind(IPlaySession playSession, float speed, IReadOnlyList<string> bindings = null)
        {
            Unbind();

            session = playSession;
            layout = playSession.Layout;
            keyBindings = bindings;
            scrollSpeed = ScrollSpeedRange.Clamp(speed);

            BuildLanes();
            LayoutLanes();

            session.LanePressed += OnLanePressed;
            session.LaneReleased += OnLaneReleased;
            session.StateChanged += OnSessionStateChanged;
            session.Judged += OnJudged;
            session.HoldTicked += OnHoldTicked;
            lastRenderedSongTimeMs = session.SongTimeMs;
        }

        public void SetScrollSpeed(float speed) => scrollSpeed = ScrollSpeedRange.Clamp(speed);

        public void SetKeyBindings(IReadOnlyList<string> bindings)
        {
            keyBindings = bindings;
            if (keyCapLabels == null) return;
            for (int laneIndex = 0; laneIndex < keyCapLabels.Length; laneIndex++)
                if (keyCapLabels[laneIndex] != null)
                    keyCapLabels[laneIndex].text = KeyLabel(laneIndex);
        }

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (session == null) return;
            session.LanePressed -= OnLanePressed;
            session.LaneReleased -= OnLaneReleased;
            session.StateChanged -= OnSessionStateChanged;
            session.Judged -= OnJudged;
            session.HoldTicked -= OnHoldTicked;
            session = null;
        }

        private string KeyLabel(int laneIndex)
        {
            string bound = keyBindings != null && laneIndex < keyBindings.Count ? keyBindings[laneIndex] : null;
            return UiNaming.KeyLabel(string.IsNullOrWhiteSpace(bound) ? layout.Lanes[laneIndex].KeyName : bound);
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
                FitSlices(plate);
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
            keyCapLabels = new TMP_Text[count];
            beamLevel = new float[count];
            beamPulseRemaining = new float[count];
            burstRemaining = new float[count];
            laneHeld = new bool[count];

            // FX controls still sit over the core controls; note draw order is handled by their layers.
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
                    FitSlices(receptor);
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
            FitSlices(cap);

            var label = NewText("Label", cap.rectTransform, KeyLabel(laneIndex),
                (spec.IsFx ? 17f : 26f) * GeometryScale, keyFont);
            label.color = spec.IsFx ? UiPalette.Magenta : UiPalette.TextSecondary;
            Stretch(label.rectTransform);
            keyCapLabels[laneIndex] = label;

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

            // The caps are measured against the deck band they sit in, so they can never grow past it
            // and over the play bar underneath.
            float deckHeight = deckLayer == null ? keyCapHeight * GeometryScale : deckLayer.rect.height;
            float fxCapHeight = fxKeyCapHeight * GeometryScale;
            float coreCapBottom = keyCapBottom * GeometryScale;
            float coreCapHeight = Mathf.Max(1f, deckHeight - coreCapBottom);
            float beamHeight = ScreenHeight * Mathf.Max(0.01f, beamScreenFraction);

            for (int laneIndex = 0; laneIndex < layout.Lanes.Count; laneIndex++)
            {
                var spec = layout.Lanes[laneIndex];
                float x = spec.BaseLaneStart * baseWidth;
                float laneSpan = spec.BaseLaneSpan * baseWidth;

                Place(beams[laneIndex].rectTransform, x, 0f, laneSpan, beamHeight);

                float receptorInset = 4f * GeometryScale;
                float receptorHeight = (spec.IsFx ? 18f : 46f) * GeometryScale;
                Place(receptors[laneIndex].rectTransform, x + receptorInset, 0f,
                    laneSpan - receptorInset * 2f, receptorHeight);

                float burstSize = Mathf.Min(spec.IsFx ? laneSpan * 0.95f : laneSpan * 1.5f,
                    260f * GeometryScale);
                Centre(bursts[laneIndex].rectTransform, x + laneSpan * 0.5f, 0f, burstSize, burstSize);

                // FX keys sit on a low strip beneath the core key caps rather than over them.
                float capInset = (spec.IsFx ? 10f : 5f) * GeometryScale;
                Place(keyCaps[laneIndex], x + capInset, spec.IsFx ? 0f : coreCapBottom,
                    laneSpan - capInset * 2f, spec.IsFx ? fxCapHeight : coreCapHeight);
            }

            if (judgementBar != null)
            {
                judgementBar.anchorMin = new Vector2(0.5f, 0f);
                judgementBar.anchorMax = new Vector2(0.5f, 0f);
                judgementBar.pivot = new Vector2(0.5f, 0.5f);
                judgementBar.anchoredPosition = Vector2.zero;
                judgementBar.sizeDelta = new Vector2(width + 12f * GeometryScale, 6f * GeometryScale);
            }

            if (judgementGlow == null) return;
            judgementGlow.anchorMin = new Vector2(0.5f, 0f);
            judgementGlow.anchorMax = new Vector2(0.5f, 0f);
            judgementGlow.pivot = new Vector2(0.5f, 0.5f);
            judgementGlow.anchoredPosition = Vector2.zero;
            judgementGlow.sizeDelta = new Vector2(width + 160f * GeometryScale, 190f * GeometryScale);
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
            double renderedSongTimeMs = session.SongTimeMs;
            if (session.State == PlaySessionState.Resuming && rewindInitialOffsetMs > 0.0)
            {
                rewindElapsedSeconds = Mathf.Min(RewindTransitionSeconds,
                    rewindElapsedSeconds + Time.unscaledDeltaTime);
                float progress = rewindElapsedSeconds / RewindTransitionSeconds;
                float eased = progress * progress * (3f - 2f * progress);
                renderedSongTimeMs += rewindInitialOffsetMs * (1.0 - eased);
            }

            lastRenderedSongTimeMs = renderedSongTimeMs;
            UpdateNotes(renderedSongTimeMs);
        }

        private void OnSessionStateChanged(PlaySessionState state)
        {
            if (state == PlaySessionState.Resuming)
            {
                rewindInitialOffsetMs = System.Math.Max(0.0, lastRenderedSongTimeMs - session.SongTimeMs);
                rewindElapsedSeconds = 0f;
                return;
            }

            rewindInitialOffsetMs = 0.0;
            rewindElapsedSeconds = 0f;
        }

        internal void UpdateNotes(double songTimeMs)
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
                float scaledNotePadding = notePadding * GeometryScale;
                float scaledNoteHeight = noteHeight * GeometryScale;
                float x = spec.BaseLaneStart * baseWidth + scaledNotePadding * 0.5f;
                float y = (float)(lead * PixelsPerMillisecond);
                float height = note.IsHold
                    ? Mathf.Max(scaledNoteHeight,
                        (float)((note.EndTimeMs - note.StartTimeMs) * PixelsPerMillisecond) + scaledNoteHeight)
                    : scaledNoteHeight;

                Color noteColor = spec.IsFx
                    ? UiPalette.FxRed
                    : spec.UsesBlueColor ? UiPalette.Cyan : Color.white;
                if (note.IsHold && note.HeadJudged)
                {
                    // A held note stops falling: its head stays pinned to the judgement line.
                    float tail = (float)((note.EndTimeMs - songTimeMs) * PixelsPerMillisecond);
                    y = 0f;
                    height = Mathf.Max(scaledNoteHeight, tail + scaledNoteHeight);
                }
                view.Image.color = noteColor;

                Place(view.Rect, x, y, spec.BaseLaneSpan * baseWidth - scaledNotePadding, height);
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
                view = new NoteView
                {
                    GameObject = image.gameObject,
                    Rect = image.rectTransform,
                    Image = image,
                    Fit = FitSlices(image)
                };
            }

            view.Image.sprite = isFx ? fxNoteSprite : normalNoteSprite;
            // Sliced for a tap as well as a hold: the note art is a third as tall as a lane is wide, so
            // stretching the whole of it into a tap's height pulls its round caps into ovals.
            view.Image.type = Image.Type.Sliced;
            view.Image.fillCenter = true;
            // A pooled view can come back carrying the other note's sprite, whose border is its own.
            view.Fit.SetArtScale(GeometryScale);
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

        /// <summary>
        /// The burst belongs to a note, not to a keypress: pressing an empty lane lights the beam and
        /// nothing else, so a player can tell a hit from a swing at thin air.
        /// </summary>
        private void OnJudged(JudgementEvent judgement)
        {
            if (bursts == null || judgement.Grade == JudgementGrade.Miss) return;
            TriggerHitEffect(judgement.Lane, pulseBeam: false);
        }

        private void OnHoldTicked(HoldTickEvent tick) => TriggerHitEffect(tick.Lane, pulseBeam: true);

        private void TriggerHitEffect(int lane, bool pulseBeam)
        {
            if (bursts == null || lane < 0 || lane >= bursts.Length) return;
            burstRemaining[lane] = burstDuration;
            if (pulseBeam) beamPulseRemaining[lane] = holdTickBeamPulseDuration;
        }

        private void OnLanePressed(int lane)
        {
            if (beams == null || lane < 0 || lane >= beams.Length) return;
            laneHeld[lane] = true;
            beamLevel[lane] = 1f;

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

        /// <summary>Advances the lit beams and hit bursts by one frame.</summary>
        internal void UpdateEffects(float deltaTime)
        {
            if (beams == null) return;

            for (int lane = 0; lane < beams.Length; lane++)
            {
                beamLevel[lane] = laneHeld[lane]
                    ? 1f
                    : Mathf.MoveTowards(beamLevel[lane], 0f, deltaTime * beamDecayPerSecond);
                beamPulseRemaining[lane] = Mathf.Max(0f, beamPulseRemaining[lane] - deltaTime);
                float pulse = holdTickBeamPulseDuration <= 0f
                    ? 0f
                    : beamPulseRemaining[lane] / holdTickBeamPulseDuration;
                float restingAlpha = beamLevel[lane] * (layout.Lanes[lane].IsFx ? 0.5f : 0.72f);
                float alpha = Mathf.Lerp(restingAlpha, 1f, pulse);
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

        /// <summary>
        /// Lane furniture is sized from the viewport every frame, and every one of these sprites is
        /// drawn far smaller than it was authored. Left alone their borders would not fit, and Unity
        /// flattens a border that does not fit rather than scaling it.
        /// </summary>
        private SlicedImageFit FitSlices(Image image)
        {
            var fit = image.gameObject.GetComponent<SlicedImageFit>();
            if (fit == null) fit = image.gameObject.AddComponent<SlicedImageFit>();
            fit.SetArtScale(GeometryScale);
            return fit;
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
