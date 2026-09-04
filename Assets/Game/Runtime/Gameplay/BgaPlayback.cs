using System;
using DJMaximusKaiserSoje.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace DJMaximusKaiserSoje.Gameplay
{
    public enum BgaVisualMode
    {
        None,
        Jacket,
        Video
    }

    public static class BgaTimeline
    {
        public static bool TryResolveTime(double songTimeMs, double startTimeMs, double durationSeconds,
            out double videoTimeSeconds)
        {
            videoTimeSeconds = Math.Max(0.0, (songTimeMs - startTimeMs) / 1000.0);
            return songTimeMs >= startTimeMs && durationSeconds > 0.0 && videoTimeSeconds < durationSeconds;
        }

        public static BgaVisualMode ResolveVisual(bool hasVideo, bool hasJacket)
        {
            if (hasVideo) return BgaVisualMode.Video;
            return hasJacket ? BgaVisualMode.Jacket : BgaVisualMode.None;
        }

        public static Vector2 CalculateJacketShakeOffset(
            double songTimeMs, double bpm, float maximumDistance = 8f)
        {
            if (songTimeMs < 0.0 || bpm <= 0.0 || double.IsNaN(bpm) || double.IsInfinity(bpm) ||
                maximumDistance <= 0f)
                return Vector2.zero;

            double beatMs = 60000.0 / bpm;
            double beatPosition = songTimeMs / beatMs;
            int beatIndex = (int)Math.Floor(beatPosition);
            float phase = (float)(beatPosition - beatIndex);
            float decay = 1f - phase;
            float strength = maximumDistance * decay * decay;
            return new Vector2(SignedHash(beatIndex * 2 + 1), SignedHash(beatIndex * 2 + 2)) * strength;
        }

        private static float SignedHash(int value)
        {
            double wave = Math.Sin(value * 12.9898) * 43758.5453;
            return (float)((wave - Math.Floor(wave)) * 2.0 - 1.0);
        }
    }

    /// <summary>Draws song video, or its jacket when no video exists, behind the gameplay UI.</summary>
    [DisallowMultipleComponent]
    public sealed class BgaPlayback : MonoBehaviour
    {
        private const string GameplaySceneName = "Gameplay";

        private IPlaySession session;
        private VideoPlayer player;
        private Sprite jacket;
        private Canvas backdropCanvas;
        private Image jacketImage;
        private RectTransform jacketRect;
        private RawImage videoImage;
        private RenderTexture videoTexture;
        private Camera targetCamera;
        private double lastVideoTime = -1.0;

        public VideoClip Clip => player == null ? null : player.clip;
        public Sprite Jacket => jacket;
        public BgaVisualMode VisualMode => BgaTimeline.ResolveVisual(Clip != null, jacket != null);

        public void Bind(IPlaySession playSession, VideoClip clip, Sprite jacketSprite)
        {
            session = playSession ?? throw new ArgumentNullException(nameof(playSession));
            jacket = jacketSprite;
            BuildBackdrop();

            if (clip == null) return;

            videoTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32)
            {
                name = "BgaRenderTexture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            videoTexture.Create();
            videoImage.texture = videoTexture;
            SetCoverAspect(videoImage.gameObject, clip.width, clip.height);

            player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.isLooping = false;
            player.skipOnDrop = true;
            player.waitForFirstFrame = true;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = videoTexture;
            player.clip = clip;
            player.timeReference = VideoTimeReference.ExternalTime;
            player.Prepare();
        }

        public void Tick()
        {
            if (session == null || backdropCanvas == null) return;

            bool inGameplay = SceneManager.GetActiveScene().name == GameplaySceneName &&
                              session.State != PlaySessionState.Finished;
            backdropCanvas.enabled = inGameplay && VisualMode != BgaVisualMode.None;
            if (!backdropCanvas.enabled)
            {
                PauseVideo();
                return;
            }

            AttachGameplayCamera();
            if (player == null)
            {
                jacketImage.enabled = jacket != null;
                videoImage.enabled = false;
                UpdateJacketShake();
                return;
            }

            ResetJacketShake();

            bool onVideoTimeline = BgaTimeline.TryResolveTime(session.SongTimeMs,
                session.Chart.VideoStartTimeMs, player.length, out double videoTime);
            bool videoReady = onVideoTimeline && player.isPrepared;
            videoImage.enabled = videoReady;
            jacketImage.enabled = !videoReady && jacket != null;

            if (!onVideoTimeline)
            {
                PauseVideo();
                lastVideoTime = videoTime;
                return;
            }

            if (lastVideoTime >= 0.0 && videoTime + 0.1 < lastVideoTime)
                player.time = videoTime;
            player.externalReferenceTime = videoTime;
            lastVideoTime = videoTime;

            if (session.State == PlaySessionState.Playing)
            {
                if (!player.isPlaying) player.Play();
            }
            else
            {
                PauseVideo();
            }
        }

        private void BuildBackdrop()
        {
            var canvasObject = new GameObject("GameplayBackdrop", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.layer = LayerMask.NameToLayer("UI");

            backdropCanvas = canvasObject.GetComponent<Canvas>();
            backdropCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            backdropCanvas.planeDistance = 50f;
            backdropCanvas.sortingOrder = -100;
            backdropCanvas.enabled = false;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            jacketImage = CreateFullscreenGraphic<Image>("Jacket", canvasObject.transform);
            jacketRect = jacketImage.rectTransform;
            jacketImage.sprite = jacket;
            jacketImage.color = Color.white;
            jacketImage.raycastTarget = false;
            if (jacket != null)
                SetCoverAspect(jacketImage.gameObject, jacket.rect.width, jacket.rect.height);

            videoImage = CreateFullscreenGraphic<RawImage>("Video", canvasObject.transform);
            videoImage.color = Color.white;
            videoImage.raycastTarget = false;
            videoImage.enabled = false;
        }

        private static T CreateFullscreenGraphic<T>(string objectName, Transform parent) where T : Graphic
        {
            var graphicObject = new GameObject(objectName, typeof(RectTransform), typeof(T));
            graphicObject.transform.SetParent(parent, false);
            graphicObject.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)graphicObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return graphicObject.GetComponent<T>();
        }

        private static void SetCoverAspect(GameObject graphicObject, double width, double height)
        {
            if (width <= 0.0 || height <= 0.0) return;
            var fitter = graphicObject.GetComponent<AspectRatioFitter>() ??
                         graphicObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = (float)(width / height);
        }

        private void AttachGameplayCamera()
        {
            Camera camera = Camera.main;
            if (camera == null || camera == targetCamera) return;
            targetCamera = camera;
            backdropCanvas.worldCamera = camera;
        }

        private void PauseVideo()
        {
            if (player != null && player.isPlaying) player.Pause();
        }

        private void UpdateJacketShake()
        {
            if (jacketRect == null || jacket == null) return;
            jacketRect.localScale = Vector3.one * 1.04f;
            jacketRect.anchoredPosition = BgaTimeline.CalculateJacketShakeOffset(
                session.SongTimeMs, session.Chart.Bpm);
        }

        private void ResetJacketShake()
        {
            if (jacketRect == null) return;
            jacketRect.localScale = Vector3.one;
            jacketRect.anchoredPosition = Vector2.zero;
        }

        private void OnDestroy()
        {
            if (player != null)
            {
                player.Stop();
                player.targetTexture = null;
            }

            if (videoTexture == null) return;
            videoTexture.Release();
            Destroy(videoTexture);
        }
    }
}
