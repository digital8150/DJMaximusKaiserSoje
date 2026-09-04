using System;
using DJMaximusKaiserSoje.Core;
using DJMaximusKaiserSoje.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DJMaximusKaiserSoje.Tests.EditMode
{
    public sealed class SongRowViewTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void SongRow_DoesNotHandlePointerHoverSelection()
        {
            root = new GameObject("SongRow");
            var view = root.AddComponent<SongRowView>();

            Assert.That(view, Is.Not.InstanceOf<IPointerEnterHandler>());
        }

        [Test]
        public void SongRow_FirstClickSelectsAndSecondClickCommits()
        {
            root = new GameObject("SongRow");
            var view = root.AddComponent<SongRowView>();
            int selected = -1;
            int committed = -1;
            var song = new SongSummary("song", "Title", "Artist", 120.0, "Pack", null,
                Array.Empty<ChartSummary>());
            view.Bind(3, song, PlayStyle.FourKey, index => selected = index, index => committed = index);
            view.SetSelected(true);

            view.OnClick();

            Assert.That(selected, Is.EqualTo(3));
            Assert.That(committed, Is.EqualTo(-1));

            view.OnClick();

            Assert.That(committed, Is.EqualTo(3));
        }
    }
}
