using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Editor
{
    /// <summary>
    /// Renders each screen scene to a PNG so a layout can be looked at without launching the game.
    /// Screens that only fill in at runtime show their authored state, which is exactly what needs
    /// reviewing after a builder change.
    /// </summary>
    internal static class ScreenshotCapture
    {
        private const string OutputFolder = "artifacts/screens";
        private const int Width = 1920;
        private const int Height = 1080;

        [MenuItem("Tools/DJ Maximus/Capture Screens")]
        public static void CaptureAll()
        {
            Directory.CreateDirectory(OutputFolder);

            string[] scenes =
            {
                BootSceneBuilder.SceneName,
                TitleSceneBuilder.SceneName,
                SongSelectSceneBuilder.SceneName,
                GameplaySceneBuilder.SceneName,
                ResultSceneBuilder.SceneName
            };

            foreach (var sceneName in scenes) Capture(sceneName);
            Debug.Log("Captured " + scenes.Length + " screens into " + OutputFolder);
        }

        private static void Capture(string sceneName)
        {
            string scenePath = SceneScaffold.SceneFolder + "/" + sceneName + ".unity";
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning("Scene not built yet: " + scenePath);
                return;
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var camera = Object.FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                Debug.LogWarning("No camera in " + sceneName);
                return;
            }

            // Text meshes and layout groups are built lazily; force them before rendering or the
            // capture shows an empty screen.
            Canvas.ForceUpdateCanvases();
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                text.ForceMeshUpdate();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);
            Canvas.ForceUpdateCanvases();

            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 2
            };

            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;

            camera.targetTexture = target;
            Render(camera, target);

            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
            image.Apply();

            // Overwriting a PNG that a viewer still has memory-mapped fails on Windows, so replace it.
            string output = OutputFolder + "/" + sceneName + ".png";
            if (File.Exists(output)) File.Delete(output);
            File.WriteAllBytes(output, image.EncodeToPNG());

            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(image);
            target.Release();
            Object.DestroyImmediate(target);
        }

        /// <summary>
        /// Scriptable render pipelines do not honour <c>Camera.Render</c>; they take a render
        /// request instead. Fall back to the direct call when no pipeline is active.
        /// </summary>
        private static void Render(Camera camera, RenderTexture target)
        {
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
            {
                RenderPipeline.SubmitRenderRequest(camera, request);
                return;
            }

            camera.Render();
        }
    }
}
