using System;
using System.Collections.Generic;
using DJMaximusKaiserSoje.Core;
using DJMaximusKaiserSoje.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Tests.EditMode
{
    /// <summary>
    /// The playfield's lane furniture: what a keypress lights up, how far a beam reaches, and what a
    /// key cap says. These are built from the session and the run's bindings, so they can be checked
    /// without a running game.
    /// </summary>
    public sealed class PlayfieldViewTests
    {
        private const float ScreenHeight = 1080f;
        private const float ViewportHeight = 800f;
        private const float ViewportWidth = 464f;
        private const float DeckHeight = 58f;

        private sealed class FakeSession : IPlaySession
        {
            public FakeSession(PlayStyle style) => Layout = LaneLayout.Create(style);

            public string SongId => "song";
            public string ChartId => "chart";
            public PlaySessionState State => PlaySessionState.Playing;
            public RunScore Score => default;
            public HealthState Health => default;
            public SectionMarker CurrentSection => default;
            public LaneLayout Layout { get; }
            public BeatmapHeader Chart => null;
            public double SongTimeMs => 0.0;
            public double SongLengthMs => 1000.0;
            public double ResumeCountdownRemainingMs => 0.0;
            public double Progress01 => 0.0;
            public IReadOnlyList<ActiveNote> PendingNotes { get; } = Array.Empty<ActiveNote>();

            public event Action<JudgementEvent> Judged;
            public event Action<int> LanePressed;
            public event Action<int> LaneReleased;

            // The playfield ignores these, so the fake only has to satisfy the contract.
            public event Action<RunScore> ScoreChanged { add { } remove { } }
            public event Action<HealthState> HealthChanged { add { } remove { } }
            public event Action<SectionMarker> SectionChanged { add { } remove { } }
            public event Action<PlaySessionState> StateChanged { add { } remove { } }
            public event Action<PlayResult> Finished { add { } remove { } }

            public void Press(int lane) => LanePressed?.Invoke(lane);

            public void Release(int lane) => LaneReleased?.Invoke(lane);

            public void Judge(int lane, JudgementGrade grade) =>
                Judged?.Invoke(new JudgementEvent(lane, grade, JudgementTiming.Exact, 0.0, 1, false));

            public void Pause() { }
            public void Resume() { }
            public void Restart() { }
            public void Abort() { }
        }

        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            root = null;
        }

        [Test]
        public void PressingAnEmptyLane_LightsTheBeamButNotTheBurst()
        {
            var session = new FakeSession(PlayStyle.FourKey);
            PlayfieldView view = BuildPlayfield(session, null);

            session.Press(1);
            view.UpdateEffects(0.016f);

            Assert.That(Alpha(view, "KeyBeam1"), Is.GreaterThan(0f),
                "A keypress should always light its lane so the player sees the input land.");
            Assert.That(Alpha(view, "KeyBurst1"), Is.EqualTo(0f),
                "A swing at an empty lane must not fire the hit burst.");
        }

        [Test]
        public void JudgingANote_FiresTheBurstOnItsLane()
        {
            var session = new FakeSession(PlayStyle.FourKey);
            PlayfieldView view = BuildPlayfield(session, null);

            session.Press(2);
            session.Judge(2, JudgementGrade.Perfect);
            view.UpdateEffects(0.016f);

            Assert.That(Alpha(view, "KeyBurst2"), Is.GreaterThan(0f));
        }

        [Test]
        public void MissingANote_LeavesTheBurstDark()
        {
            var session = new FakeSession(PlayStyle.FourKey);
            PlayfieldView view = BuildPlayfield(session, null);

            session.Judge(0, JudgementGrade.Miss);
            view.UpdateEffects(0.016f);

            Assert.That(Alpha(view, "KeyBurst0"), Is.EqualTo(0f));
        }

        [Test]
        public void KeyBeam_FillsItsLaneAndReachesHalfTheScreen()
        {
            var session = new FakeSession(PlayStyle.FourKey);
            PlayfieldView view = BuildPlayfield(session, null);

            var beam = Find(view, "KeyBeam0");
            Assert.That(beam.rect.width, Is.EqualTo(ViewportWidth / 4f).Within(0.01f));
            Assert.That(beam.rect.height, Is.EqualTo(ScreenHeight * 0.5f).Within(0.01f));
        }

        [Test]
        public void KeyCaps_ShowTheKeysTheRunIsPlayedWith()
        {
            var session = new FakeSession(PlayStyle.FourKey);
            PlayfieldView view = BuildPlayfield(session, new[] { "z", "x", "n", "semicolon" });

            Assert.That(CapLabel(view, 0), Is.EqualTo("Z"));
            Assert.That(CapLabel(view, 1), Is.EqualTo("X"));
            Assert.That(CapLabel(view, 2), Is.EqualTo("N"));
            Assert.That(CapLabel(view, 3), Is.EqualTo(";"));
        }

        [Test]
        public void KeyCaps_FollowARebindWithoutRestartingTheRun()
        {
            var session = new FakeSession(PlayStyle.FourKey);
            PlayfieldView view = BuildPlayfield(session, new[] { "d", "f", "j", "k" });

            view.SetKeyBindings(new[] { "d", "f", "j", "space" });

            Assert.That(CapLabel(view, 3), Is.EqualTo("SPACE"));
        }

        [Test]
        public void KeyCaps_WithoutBindings_FallBackToTheLayoutDefaults()
        {
            var session = new FakeSession(PlayStyle.FourKey);
            PlayfieldView view = BuildPlayfield(session, null);

            Assert.That(CapLabel(view, 0), Is.EqualTo("D"));
        }

        [Test]
        public void KeyCaps_StayInsideTheDeckBand()
        {
            var session = new FakeSession(PlayStyle.SixKeyFx);
            PlayfieldView view = BuildPlayfield(session, null);

            for (int laneIndex = 0; laneIndex < session.Layout.Lanes.Count; laneIndex++)
            {
                RectTransform cap = Find(view, "KeyCap" + laneIndex);
                float top = cap.anchoredPosition.y + cap.rect.height;
                Assert.That(top, Is.LessThanOrEqualTo(DeckHeight + 0.01f),
                    "Key cap " + laneIndex + " runs past the deck and over the play bar.");
            }
        }

        private PlayfieldView BuildPlayfield(IPlaySession session, IReadOnlyList<string> bindings)
        {
            root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            // World space keeps the canvas rect at the size the test sets rather than the game view's.
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var canvasRect = (RectTransform)root.transform;
            canvasRect.sizeDelta = new Vector2(1920f, ScreenHeight);

            var gear = NewRect("Gear", canvasRect, Vector2.zero, new Vector2(540f, ScreenHeight));
            var view = gear.gameObject.AddComponent<PlayfieldView>();

            var viewport = NewRect("LaneViewport", gear, new Vector2(0f, 280f),
                new Vector2(ViewportWidth, ViewportHeight));
            view.laneViewport = viewport;
            view.laneLayer = NewRect("LaneLayer", viewport, Vector2.zero, Vector2.zero);
            view.beamLayer = NewRect("BeamLayer", viewport, Vector2.zero, Vector2.zero);
            view.noteLayer = NewRect("NoteLayer", viewport, Vector2.zero, Vector2.zero);
            view.fxNoteLayer = NewRect("FxNoteLayer", viewport, Vector2.zero, Vector2.zero);
            view.burstLayer = NewRect("BurstLayer", gear, Vector2.zero, Vector2.zero);
            view.deckLayer = NewRect("DeckLayer", gear, new Vector2(0f, 208f),
                new Vector2(ViewportWidth, DeckHeight));

            view.Bind(session, 5f, bindings);
            return view;
        }

        private static RectTransform NewRect(string name, RectTransform parent, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform Find(PlayfieldView view, string name)
        {
            foreach (RectTransform rect in view.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == name) return rect;

            throw new AssertionException("The playfield built no " + name + ".");
        }

        private static float Alpha(PlayfieldView view, string name) =>
            Find(view, name).GetComponent<Image>().color.a;

        private static string CapLabel(PlayfieldView view, int laneIndex) =>
            Find(view, "KeyCap" + laneIndex).GetComponentInChildren<TMP_Text>(true).text;
    }
}
