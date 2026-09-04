using System.Collections;
using System.IO;
using DJMaximusKaiserSoje.App;
using DJMaximusKaiserSoje.Core;
using DJMaximusKaiserSoje.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DJMaximusKaiserSoje.Tests.PlayMode
{
    /// <summary>
    /// Walks the real screens end to end: boot, song select, and a started run. These cover the
    /// wiring that unit tests cannot — scene loading, screen binding, and content resolving through
    /// Addressables — and leave a screenshot of each screen behind for review.
    /// </summary>
    public sealed class ScreenFlowSmokeTests
    {
        private const string ShotFolder = "artifacts/screens";
        private const float Timeout = 30f;

        [UnityTest]
        public IEnumerator Boot_ReachesSongSelect_WithSongsListed()
        {
            yield return EnsureAtTitle();

            var bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
            bootstrap.Services.Flow.ShowSongSelect();
            yield return WaitForScene("SongSelect");

            // The rows are built when the screen binds, one frame after the scene loads.
            yield return null;
            yield return null;

            var select = Object.FindFirstObjectByType<SongSelectScreenView>();
            Assert.That(select, Is.Not.Null, "The song select scene has no screen view.");

            var rows = Object.FindObjectsByType<SongRowView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Assert.That(bootstrap.Services.Songs.Songs.Count, Is.GreaterThan(0), "The catalog resolved no songs.");
            Assert.That(rows.Length, Is.EqualTo(bootstrap.Services.Songs.Songs.Count),
                "The list did not build one row per song.");

            yield return Capture("Runtime-SongSelect");
        }

        [UnityTest]
        public IEnumerator StartingAChart_ReachesGameplay_AndSpawnsNotes()
        {
            yield return EnsureAtTitle();

            var bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
            var services = bootstrap.Services;
            var song = services.Songs.Songs[0];
            var chart = song.Charts[0];

            services.Flow.StartPlay(new PlayRequest(song.Id, chart.Id, PlayStyle.FourKey, 5f, 0.0));
            yield return WaitForScene("Gameplay");

            var gameplay = Object.FindFirstObjectByType<GameplayScreenView>();
            Assert.That(gameplay, Is.Not.Null, "The gameplay scene has no screen view.");

            float waited = 0f;
            while (waited < Timeout && gameplay.Session == null)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            var play = gameplay.Session;
            Assert.That(play, Is.Not.Null, "No play session was bound to the gameplay screen.");

            // Run past the lead-in so notes are on screen when the shot is taken.
            float elapsed = 0f;
            while (elapsed < 4f && play.State != PlaySessionState.Finished)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.That(play.Chart, Is.Not.Null, "The session has no chart loaded.");
            Assert.That(play.PendingNotes.Count, Is.GreaterThan(0), "The chart produced no notes.");

            yield return Capture("Runtime-Gameplay");
        }

        /// <summary>
        /// Gets the game to the title screen. The bootstrap survives scene loads on purpose, so a
        /// second test reuses the one that is already composed rather than booting over the top of
        /// it — reloading Boot would only be refused by its own singleton guard.
        /// </summary>
        private static IEnumerator EnsureAtTitle()
        {
            var bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
            if (bootstrap == null)
            {
                SceneManager.LoadScene("Boot", LoadSceneMode.Single);
                yield return null;
            }

            float elapsed = 0f;
            while (elapsed < Timeout)
            {
                bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
                if (bootstrap != null && bootstrap.Services != null) break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.That(bootstrap, Is.Not.Null, "The boot scene has no bootstrap.");
            Assert.That(bootstrap.Services, Is.Not.Null, "The bootstrap never finished building services.");

            bootstrap.Services.Flow.ShowTitle();
            yield return WaitForScene("Title");
        }

        private static IEnumerator WaitForScene(string sceneName)
        {
            float elapsed = 0f;
            while (elapsed < Timeout && SceneManager.GetActiveScene().name != sceneName)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sceneName),
                $"The flow never reached the {sceneName} scene.");
        }

        private static IEnumerator Capture(string name)
        {
            // Not WaitForEndOfFrame: it never resumes in batch mode, and the render request below
            // does not need to be issued at the end of a frame anyway.
            yield return null;

            var camera = Object.FindFirstObjectByType<Camera>();
            if (camera == null) yield break;

            Directory.CreateDirectory(ShotFolder);
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;

            camera.targetTexture = target;
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
            else camera.Render();

            RenderTexture.active = target;
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, 1920f, 1080f), 0, 0);
            image.Apply();

            string path = ShotFolder + "/" + name + ".png";
            if (File.Exists(path)) File.Delete(path);
            File.WriteAllBytes(path, image.EncodeToPNG());

            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Object.Destroy(image);
            target.Release();
            Object.Destroy(target);
        }
    }
}
