using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DJMaximusKaiserSoje.Content;
using DJMaximusKaiserSoje.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DJMaximusKaiserSoje.App
{
    [DisallowMultipleComponent]
    public sealed class MusicDirector : MonoBehaviour, IMusicDirector
    {
        private readonly Dictionary<ScreenTheme, AudioClip> themeClips = new Dictionary<ScreenTheme, AudioClip>();
        private readonly List<AsyncOperationHandle<AudioClip>> themeHandles = new List<AsyncOperationHandle<AudioClip>>();
        private PreviewDwellDebouncer previewDwell;
        private IPreviewClock previewClock;
        private CatalogSongLibrary songs;
        private IContentLoader contentLoader;
        private AudioSource[] sources;
        private int activeSource;
        private int requestVersion;
        private Coroutine crossfade;
        private LoadedSongContent previewContent;
        private string pendingPreviewSong;
        private double previewStartSeconds;

        public ScreenTheme CurrentTheme { get; private set; }
        public string PreviewingSongId { get; private set; }

        public event Action<string> PreviewStarted;
        public event Action PreviewStopped;

        public void Configure(CatalogSongLibrary library, IContentLoader loader, IPreviewClock clock = null)
        {
            songs = library ?? throw new ArgumentNullException(nameof(library));
            contentLoader = loader ?? throw new ArgumentNullException(nameof(loader));
            previewClock = clock ?? new UnityPreviewClock();
            previewDwell = new PreviewDwellDebouncer(previewClock);
            EnsureSources();
        }

        public void PlayTheme(ScreenTheme theme)
        {
            EnsureConfigured();
            requestVersion++;
            previewDwell.Cancel();
            StopPreview(true);
            CurrentTheme = theme;
            if (theme == ScreenTheme.None)
            {
                StopSources();
                return;
            }
            StartCoroutine(LoadAndPlayTheme(theme, requestVersion));
        }

        public void StopTheme()
        {
            requestVersion++;
            previewDwell.Cancel();
            CurrentTheme = ScreenTheme.None;
            StopPreview(true);
            StopSources();
        }

        public void RequestSongPreview(string songId)
        {
            EnsureConfigured();
            if (!songs.TryGetSong(songId, out _)) return;
            pendingPreviewSong = songId;
            previewDwell.Request(songId);
        }

        public void CancelSongPreview()
        {
            requestVersion++;
            pendingPreviewSong = null;
            previewDwell.Cancel();
            StopPreview(true);
            if (CurrentTheme != ScreenTheme.None)
                StartCoroutine(LoadAndPlayTheme(CurrentTheme, requestVersion));
        }

        private void Awake()
        {
            EnsureSources();
            previewClock = new UnityPreviewClock();
            previewDwell = new PreviewDwellDebouncer(previewClock);
        }

        private void Update()
        {
            if (previewDwell == null) return;
            if (previewDwell.TryTake(out string songId))
            {
                pendingPreviewSong = null;
                int version = ++requestVersion;
                StartCoroutine(LoadAndPlayPreview(songId, version));
            }
        }

        private IEnumerator LoadAndPlayTheme(ScreenTheme theme, int version)
        {
            Task<ContentLoadResult<AudioClip>> task = LoadThemeClipAsync(theme);
            while (!task.IsCompleted) yield return null;
            if (version != requestVersion || task.IsFaulted || !task.Result.Succeeded)
                yield break;
            PlayClip(task.Result.Value, 0.0, true);
        }

        private IEnumerator LoadAndPlayPreview(string songId, int version)
        {
            if (!songs.TryGetSong(songId, out SongSummary song) || song.Charts.Count == 0) yield break;
            ChartSummary chart = song.Charts[0];
            if (!songs.TryGetContent(chart.Id, out SongContentAddresses addresses)) yield break;

            Task<ContentLoadResult<LoadedSongContent>> task = contentLoader.LoadSongAsync(
                new SongContentRequest(addresses.ChartAddress, addresses.AudioAddress, addresses.JacketAddress, addresses.VideoAddress),
                CancellationToken.None);
            while (!task.IsCompleted) yield return null;
            if (version != requestVersion || task.IsFaulted || !task.Result.Succeeded)
            {
                if (!task.IsFaulted && task.Result.Succeeded) task.Result.Value.Dispose();
                yield break;
            }

            previewContent?.Dispose();
            previewContent = task.Result.Value;
            try
            {
                Beatmap chartData = new OsuManiaBeatmapParser().Parse(previewContent.ChartText.text);
                previewStartSeconds = PreviewPointResolver.Resolve(chartData, previewContent.Audio.length * 1000.0) / 1000.0;
            }
            catch (BeatmapParseException)
            {
                previewStartSeconds = previewContent.Audio.length * MusicTiming.PreviewFallbackPosition01;
            }

            PreviewingSongId = songId;
            PlayClip(previewContent.Audio, previewStartSeconds, false);
            PreviewStarted?.Invoke(songId);
        }

        private async Task<ContentLoadResult<AudioClip>> LoadThemeClipAsync(ScreenTheme theme)
        {
            if (themeClips.TryGetValue(theme, out AudioClip cachedClip) && cachedClip != null)
                return ContentLoadResult<AudioClip>.Success(cachedClip);
            string address = AddressFor(theme);
            if (string.IsNullOrWhiteSpace(address))
                return ContentLoadResult<AudioClip>.Failure(ContentLoadError.InvalidRequest, "No theme is selected.");
            AsyncOperationHandle<AudioClip> handle = Addressables.LoadAssetAsync<AudioClip>(address);
            try
            {
                await handle.Task;
            }
            catch (Exception exception)
            {
                if (handle.IsValid()) Addressables.Release(handle);
                return ContentLoadResult<AudioClip>.Failure(ContentLoadError.AddressableFailed, exception.Message);
            }
            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                if (handle.IsValid()) Addressables.Release(handle);
                return ContentLoadResult<AudioClip>.Failure(ContentLoadError.AddressableFailed, "Theme audio could not be loaded.");
            }
            themeHandles.Add(handle);
            themeClips[theme] = handle.Result;
            return ContentLoadResult<AudioClip>.Success(handle.Result);
        }

        private void PlayClip(AudioClip clip, double startSeconds, bool loop)
        {
            if (clip == null) return;
            EnsureSources();
            int next = 1 - activeSource;
            AudioSource incoming = sources[next];
            incoming.Stop();
            incoming.clip = clip;
            incoming.loop = loop;
            incoming.time = (float)Math.Max(0.0, Math.Min(clip.length - 0.01, startSeconds));
            incoming.volume = 0.0f;
            incoming.Play();
            if (crossfade != null) StopCoroutine(crossfade);
            crossfade = StartCoroutine(Crossfade(activeSource, next));
            activeSource = next;
        }

        private IEnumerator Crossfade(int outgoingIndex, int incomingIndex)
        {
            AudioSource outgoing = sources[outgoingIndex];
            AudioSource incoming = sources[incomingIndex];
            float elapsed = 0.0f;
            while (elapsed < MusicTiming.CrossfadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / (float)MusicTiming.CrossfadeSeconds);
                outgoing.volume = 1.0f - progress;
                incoming.volume = progress;
                yield return null;
            }
            outgoing.volume = 0.0f;
            outgoing.Stop();
            incoming.volume = 1.0f;
            crossfade = null;
        }

        private void StopPreview(bool notify)
        {
            bool hadPreview = !string.IsNullOrEmpty(PreviewingSongId);
            PreviewingSongId = null;
            previewContent?.Dispose();
            previewContent = null;
            if (notify && hadPreview) PreviewStopped?.Invoke();
        }

        private void StopSources()
        {
            if (sources == null) return;
            for (int index = 0; index < sources.Length; index++)
            {
                sources[index].Stop();
                sources[index].clip = null;
                sources[index].volume = 0.0f;
            }
        }

        private void EnsureConfigured()
        {
            if (previewDwell == null) previewDwell = new PreviewDwellDebouncer(previewClock ?? new UnityPreviewClock());
            if (songs == null || contentLoader == null) throw new InvalidOperationException("MusicDirector has not been configured.");
        }

        private void EnsureSources()
        {
            if (sources != null) return;
            sources = new[] { gameObject.AddComponent<AudioSource>(), gameObject.AddComponent<AudioSource>() };
            for (int index = 0; index < sources.Length; index++)
            {
                sources[index].playOnAwake = false;
                sources[index].loop = false;
                sources[index].spatialBlend = 0.0f;
                sources[index].volume = 0.0f;
            }
        }

        private static string AddressFor(ScreenTheme theme)
        {
            switch (theme)
            {
                case ScreenTheme.Title: return ThemeMusicAddresses.Title;
                case ScreenTheme.SongSelect: return ThemeMusicAddresses.SongSelect;
                case ScreenTheme.Result: return ThemeMusicAddresses.Result;
                default: return null;
            }
        }

        private void OnDestroy()
        {
            previewContent?.Dispose();
            for (int index = 0; index < themeHandles.Count; index++)
                if (themeHandles[index].IsValid()) Addressables.Release(themeHandles[index]);
            themeHandles.Clear();
        }
    }
}
