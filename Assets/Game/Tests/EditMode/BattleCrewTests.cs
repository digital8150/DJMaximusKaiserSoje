using System;
using DJMaximusKaiserSoje.Core;
using DJMaximusKaiserSoje.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Tests.EditMode
{
    public sealed class BattleCrewTests
    {
        private static Sprite CreateDummySprite(string name)
        {
            var texture = new Texture2D(2, 2);
            return Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
        }

        private static BattleCrewData CreateTestData(string id = "aira")
        {
            var idle = new[] { CreateDummySprite("idle0"), CreateDummySprite("idle1") };
            var excited = new[] { CreateDummySprite("excited0"), CreateDummySprite("excited1") };
            var bad = new[] { CreateDummySprite("bad0"), CreateDummySprite("bad1") };
            return new BattleCrewData(id, "아이라", "비트마스터", idle[0], idle, excited, bad, 10f);
        }

        [Test]
        public void BattleCrewId_ValidationAndNormalization()
        {
            Assert.That(BattleCrewId.IsValid(BattleCrewId.Aira), Is.True);
            Assert.That(BattleCrewId.IsValid(BattleCrewId.Kai), Is.True);
            Assert.That(BattleCrewId.IsValid(BattleCrewId.Lena), Is.True);
            Assert.That(BattleCrewId.IsValid(BattleCrewId.Ren), Is.True);
            Assert.That(BattleCrewId.IsValid("unknown"), Is.False);
            Assert.That(BattleCrewId.IsValid(null), Is.False);

            Assert.That(BattleCrewId.Normalize("KAI"), Is.EqualTo(BattleCrewId.Kai));
            Assert.That(BattleCrewId.Normalize("invalid"), Is.EqualTo(BattleCrewId.Default));
            Assert.That(BattleCrewId.Normalize(null), Is.EqualTo(BattleCrewId.Default));
        }

        [Test]
        public void BattleCrewCatalog_FindsCrewsOrDefault()
        {
            var catalog = ScriptableObject.CreateInstance<BattleCrewCatalog>();
            var aira = CreateTestData(BattleCrewId.Aira);
            var kai = CreateTestData(BattleCrewId.Kai);
            catalog.SetCrews(new[] { aira, kai });

            Assert.That(catalog.TryGetCrew(BattleCrewId.Kai, out var foundKai), Is.True);
            Assert.That(foundKai.Id, Is.EqualTo(BattleCrewId.Kai));

            Assert.That(catalog.GetCrewOrDefault("nonexistent").Id, Is.EqualTo(BattleCrewId.Aira));
        }

        [Test]
        public void BattleCrewView_InitialState_IsIdle()
        {
            var go = new GameObject("BattleCrewTest");
            var img = go.AddComponent<Image>();
            var view = go.AddComponent<BattleCrewView>();

            var data = CreateTestData();
            view.Bind(data);

            Assert.That(view.CurrentState, Is.EqualTo(CrewAnimationState.Idle));
            Assert.That(view.CurrentFrameIndex, Is.EqualTo(0));
            Assert.That(img.sprite, Is.EqualTo(data.IdleFrames[0]));

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void BattleCrewView_Reaching125Combo_TriggersExcited()
        {
            var go = new GameObject("BattleCrewTest");
            go.AddComponent<Image>();
            var view = go.AddComponent<BattleCrewView>();
            view.Bind(CreateTestData());

            view.NotifyCombo(124, 123);
            Assert.That(view.CurrentState, Is.EqualTo(CrewAnimationState.Idle));

            view.NotifyCombo(125, 124);
            Assert.That(view.CurrentState, Is.EqualTo(CrewAnimationState.Excited));
            Assert.That(view.LastExcitedMilestone, Is.EqualTo(1));

            // Another note in the same milestone stays excited / doesn't re-trigger
            view.NotifyCombo(126, 125);
            Assert.That(view.LastExcitedMilestone, Is.EqualTo(1));

            // Next milestone 250 triggers excited again
            view.NotifyCombo(250, 249);
            Assert.That(view.CurrentState, Is.EqualTo(CrewAnimationState.Excited));
            Assert.That(view.LastExcitedMilestone, Is.EqualTo(2));

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void BattleCrewView_ComboBreak_TriggersBad()
        {
            var go = new GameObject("BattleCrewTest");
            go.AddComponent<Image>();
            var view = go.AddComponent<BattleCrewView>();
            view.Bind(CreateTestData());

            view.NotifyCombo(50, 49);
            Assert.That(view.CurrentState, Is.EqualTo(CrewAnimationState.Idle));

            // Miss drops combo to 0
            view.NotifyCombo(0, 50);
            Assert.That(view.CurrentState, Is.EqualTo(CrewAnimationState.Bad));
            Assert.That(view.LastExcitedMilestone, Is.EqualTo(0));

            // Subsequent misses when already at 0 do not retrigger Bad
            view.PlayIdle();
            view.NotifyCombo(0, 0);
            Assert.That(view.CurrentState, Is.EqualTo(CrewAnimationState.Idle));

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void BattleCrewView_AnimationLifecycle_ReturnsToIdleAfterOneShot()
        {
            var go = new GameObject("BattleCrewTest");
            go.AddComponent<Image>();
            var view = go.AddComponent<BattleCrewView>();
            var data = CreateTestData(); // 2 frames at 10 fps -> 0.1s per frame
            view.Bind(data);

            view.PlayExcited();
            Assert.That(view.CurrentState, Is.EqualTo(CrewAnimationState.Excited));
            Assert.That(view.CurrentFrameIndex, Is.EqualTo(0));

            // Advance 0.1s -> frame 1
            view.UpdateAnimation(0.1f);
            Assert.That(view.CurrentState, Is.EqualTo(CrewAnimationState.Excited));
            Assert.That(view.CurrentFrameIndex, Is.EqualTo(1));

            // Advance another 0.1s -> one-shot finishes and returns to Idle!
            view.UpdateAnimation(0.1f);
            Assert.That(view.CurrentState, Is.EqualTo(CrewAnimationState.Idle));
            Assert.That(view.CurrentFrameIndex, Is.EqualTo(0));

            // Idle loops continuously
            view.UpdateAnimation(0.1f);
            Assert.That(view.CurrentState, Is.EqualTo(CrewAnimationState.Idle));
            Assert.That(view.CurrentFrameIndex, Is.EqualTo(1));

            view.UpdateAnimation(0.1f);
            Assert.That(view.CurrentState, Is.EqualTo(CrewAnimationState.Idle));
            Assert.That(view.CurrentFrameIndex, Is.EqualTo(0));

            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
