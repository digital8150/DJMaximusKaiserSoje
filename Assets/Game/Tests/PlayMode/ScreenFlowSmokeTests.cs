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
            int compatibleSongs = 0;
            foreach (SongSummary song in bootstrap.Services.Songs.Songs)
                if (PlayStyleChartCompatibility.IsCompatible(bootstrap.Services.Preferences.PlayStyle, song))
                    compatibleSongs++;
            Assert.That(rows.Length, Is.EqualTo(compatibleSongs),
                "The list did not build one row per song compatible with the selected key mode.");

            yield return Capture("Runtime-SongSelect");

            Assert.That(select.optionsButton, Is.Not.Null, "Song select has no visible settings button.");
            select.optionsButton.onClick.Invoke();
            yield return WaitForScene("Options");
            var options = Object.FindFirstObjectByType<OptionsScreenView>();
            Assert.That(options, Is.Not.Null);
            options.backButton.onClick.Invoke();
            yield return WaitForScene("SongSelect");
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
            Assert.That(GameObject.Find("OffsetChip"), Is.Null,
                "Judgement offset belongs in options, not the gameplay HUD.");
            Assert.That(gameplay.playfield.fxNoteLayer.GetSiblingIndex(),
                Is.LessThan(gameplay.playfield.noteLayer.GetSiblingIndex()),
                "FX notes must render behind regular notes.");

            float waited = 0f;
            while (waited < Timeout && gameplay.Session == null)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            var play = gameplay.Session;
            Assert.That(play, Is.Not.Null, "No play session was bound to the gameplay screen.");

            const float expectedGearScale = 1080f / 1571f;
            var gearRect = GameObject.Find("Gear").GetComponent<RectTransform>();
            var judgementBar = GameObject.Find("JudgementBar").GetComponent<RectTransform>();
            var receptor = GameObject.Find("Receptor0").GetComponent<RectTransform>();
            Assert.That(gearRect.rect.height, Is.EqualTo(1080f).Within(0.1f),
                "The complete gear should fit within the reference-height screen.");
            Assert.That(gearRect.rect.width, Is.LessThan(540f),
                "The source gear should be uniformly scaled instead of retaining the old wide playfield.");
            Assert.That(gameplay.playfield.geometryScale, Is.EqualTo(expectedGearScale).Within(0.001f));
            Assert.That(judgementBar.rect.height, Is.EqualTo(6f * expectedGearScale).Within(0.1f));
            Assert.That(receptor.rect.height, Is.EqualTo(46f * expectedGearScale).Within(0.1f));

            // Run past the lead-in so notes are on screen when the shot is taken.
            float elapsed = 0f;
            while (elapsed < 4f && play.State != PlaySessionState.Finished)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.That(play.Chart, Is.Not.Null, "The session has no chart loaded.");
            Assert.That(play.PendingNotes.Count, Is.GreaterThan(0), "The chart produced no notes.");
            var bga = Object.FindFirstObjectByType<DJMaximusKaiserSoje.Gameplay.BgaPlayback>();
            Assert.That(bga, Is.Not.Null, "The song video was loaded but no BGA playback was created.");
            Assert.That(bga.Clip, Is.Not.Null);

            yield return Capture("Runtime-Gameplay");

            play.Pause();
            yield return null;
            Assert.That(gameplay.pauseOverlay.alpha, Is.EqualTo(1f));
            Assert.That(gameplay.resumeButton, Is.Not.Null);
            Assert.That(gameplay.restartButton, Is.Not.Null);
            Assert.That(gameplay.quitButton, Is.Not.Null);

            gameplay.resumeButton.onClick.Invoke();
            Assert.That(play.State, Is.EqualTo(PlaySessionState.Resuming));
            Assert.That(play.ResumeCountdownRemainingMs, Is.EqualTo(3000.0).Within(20.0));
        }

        [UnityTest]
        public IEnumerator StartingAChart_WithoutVideo_UsesJacketAsBackdrop()
        {
            yield return EnsureAtTitle();

            var bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
            var services = bootstrap.Services;
            SongSummary song = null;
            foreach (SongSummary candidate in services.Songs.Songs)
                if (candidate.Id == "last-fortune")
                {
                    song = candidate;
                    break;
                }

            Assert.That(song, Is.Not.Null, "The no-video fixture song is missing from the catalog.");
            var chart = song.Charts[0];
            services.Flow.StartPlay(new PlayRequest(song.Id, chart.Id, PlayStyle.FourKey, 5f, 0.0));
            yield return WaitForScene("Gameplay");
            yield return null;
            yield return null;

            var bga = Object.FindFirstObjectByType<DJMaximusKaiserSoje.Gameplay.BgaPlayback>();
            Assert.That(bga, Is.Not.Null, "No gameplay backdrop was created.");
            Assert.That(bga.Clip, Is.Null, "The fixture unexpectedly loaded a video.");
            Assert.That(bga.Jacket, Is.Not.Null, "The chart jacket was not loaded for fallback.");
            Assert.That(bga.VisualMode, Is.EqualTo(DJMaximusKaiserSoje.Gameplay.BgaVisualMode.Jacket));

            yield return Capture("Runtime-Gameplay-JacketFallback");
        }

        [UnityTest]
        public IEnumerator Options_FromTitle_BindsSettingsAndReturnsToTitle()
        {
            yield return EnsureAtTitle();

            var bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
            double originalOffset = bootstrap.Services.Preferences.JudgementOffsetMs;
            bootstrap.Services.Preferences.JudgementOffsetMs = 0.0;
            var title = Object.FindFirstObjectByType<TitleScreenView>();
            Assert.That(title, Is.Not.Null);
            Assert.That(title.optionsButton, Is.Not.Null, "The title screen has no visible settings button.");
            title.optionsButton.onClick.Invoke();
            yield return WaitForScene("Options");
            yield return null;

            var options = Object.FindFirstObjectByType<OptionsScreenView>();
            Assert.That(options, Is.Not.Null, "The options scene has no screen view.");
            Assert.That(options.styleTabs.Length, Is.EqualTo(4));
            Assert.That(options.keyButtons.Length, Is.EqualTo(8));
            Assert.That(options.judgementOffsetLabel, Is.Not.Null);
            Assert.That(options.judgementOffsetDownButton, Is.Not.Null);
            Assert.That(options.judgementOffsetUpButton, Is.Not.Null);
            options.judgementOffsetUpButton.onClick.Invoke();
            Assert.That(bootstrap.Services.Preferences.JudgementOffsetMs, Is.EqualTo(5.0));
            Assert.That(options.judgementOffsetLabel.text, Is.EqualTo("+5 ms"));

            options.styleTabs[(int)PlayStyle.FourKeyFx].button.onClick.Invoke();
            Assert.That(options.keyRoleLabels[0].text, Is.EqualTo("왼쪽"));
            Assert.That(options.keyRoleLabels[5].text, Is.EqualTo("오른쪽"));
            Assert.That(options.keyButtons[0].GetComponent<RectTransform>().anchoredPosition.y,
                Is.LessThan(options.keyButtons[1].GetComponent<RectTransform>().anchoredPosition.y),
                "FX bindings must be displayed separately below regular lanes.");

            options.styleTabs[(int)PlayStyle.SixKeyFx].button.onClick.Invoke();
            Assert.That(options.keyRoleLabels[0].text, Is.EqualTo("왼쪽"));
            Assert.That(options.keyRoleLabels[7].text, Is.EqualTo("오른쪽"));
            Assert.That(options.keyButtons[0].GetComponent<RectTransform>().anchoredPosition.y,
                Is.LessThan(options.keyButtons[4].GetComponent<RectTransform>().anchoredPosition.y));

            yield return Capture("Runtime-Options");

            bootstrap.Services.Preferences.JudgementOffsetMs = originalOffset;
            options.backButton.onClick.Invoke();
            yield return WaitForScene("Title");
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
            // The scene name changes before LoadSceneAsync.completed clears the flow's loading flag.
            yield return null;
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
