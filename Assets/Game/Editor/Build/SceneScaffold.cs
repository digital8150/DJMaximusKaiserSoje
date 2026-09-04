using System.IO;
using DJMaximusKaiserSoje.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Editor
{
    /// <summary>The camera, canvas, and input plumbing every screen scene starts from.</summary>
    internal static class SceneScaffold
    {
        public const string SceneFolder = "Assets/Scenes";

        public static (Scene scene, RectTransform root) Create(string sceneName)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = sceneName;

            // The listener rides the camera: a scene without one plays no sound at all.
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UiPalette.Ink;
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasObject = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasObject.GetComponent<Canvas>();
            // Rendered through the camera rather than as an overlay, so the screens can be captured
            // to a texture for review without entering play mode.
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;
            canvas.pixelPerfect = false;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Ui.ReferenceWidth, Ui.ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return (scene, (RectTransform)canvasObject.transform);
        }

        /// <summary>Background art plus the vignette that keeps text legible over it.</summary>
        public static void Backdrop(RectTransform root, string artName, float dim = 0.55f)
        {
            var art = Ui.Image("Backdrop", root, Ui.Art(artName), Color.white).Stretch();
            art.preserveAspect = false;
            art.type = Image.Type.Simple;

            Ui.Image("BackdropShade", root, null, UiPalette.Ink.WithAlpha(dim)).Stretch();
            Ui.Image("BackdropVignette", root, Ui.Chrome("Vignette"), UiPalette.Ink.WithAlpha(0.85f)).Stretch();
        }

        public static string Save(Scene scene, string sceneName)
        {
            Directory.CreateDirectory(SceneFolder);
            string path = SceneFolder + "/" + sceneName + ".unity";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }
    }
}
