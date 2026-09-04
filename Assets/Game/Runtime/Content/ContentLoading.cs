using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Video;

namespace DJMaximusKaiserSoje.Content
{
    public enum ContentLoadError
    {
        None,
        EmptyAddress,
        AddressableFailed,
        NullAsset,
        Cancelled,
        InvalidRequest,
        ParseFailed
    }

    public sealed class ContentLoadResult<T>
    {
        private ContentLoadResult(bool succeeded, T value, ContentLoadError error, string message)
        {
            Succeeded = succeeded;
            Value = value;
            Error = error;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public bool IsSuccess => Succeeded;
        public T Value { get; }
        public ContentLoadError Error { get; }
        public string Message { get; }

        public static ContentLoadResult<T> Success(T value) => new ContentLoadResult<T>(true, value, ContentLoadError.None, string.Empty);

        public static ContentLoadResult<T> Failure(ContentLoadError error, string message) =>
            new ContentLoadResult<T>(false, default, error, message);
    }

    public sealed class SongContentRequest
    {
        public SongContentRequest(string chartAddress, string audioAddress, string jacketAddress, string videoAddress = null)
        {
            ChartAddress = chartAddress;
            AudioAddress = audioAddress;
            JacketAddress = jacketAddress;
            VideoAddress = videoAddress;
        }

        public string ChartAddress { get; }
        public string AudioAddress { get; }
        public string JacketAddress { get; }
        public string VideoAddress { get; }
    }

    public sealed class LoadedSongContent : IDisposable
    {
        private readonly List<Action> releaseActions;
        private bool disposed;

        internal LoadedSongContent(TextAsset chartText, AudioClip audio, Sprite jacket, VideoClip video, List<Action> releaseActions)
        {
            ChartText = chartText;
            Audio = audio;
            Jacket = jacket;
            Video = video;
            this.releaseActions = releaseActions;
        }

        public TextAsset ChartText { get; }
        public AudioClip Audio { get; }
        public Sprite Jacket { get; }
        public VideoClip Video { get; }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            for (int index = releaseActions.Count - 1; index >= 0; index--)
                releaseActions[index]();
            releaseActions.Clear();
        }
    }

    public interface IContentLoader
    {
        Task<ContentLoadResult<LoadedSongContent>> LoadSongAsync(SongContentRequest request, CancellationToken cancellationToken = default);
    }

    /// <summary>Addressables adapter. Every failure is returned as data so callers can choose UI copy.</summary>
    public sealed class AddressablesContentLoader : IContentLoader
    {
        public async Task<ContentLoadResult<LoadedSongContent>> LoadSongAsync(
            SongContentRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
                return ContentLoadResult<LoadedSongContent>.Failure(ContentLoadError.InvalidRequest, "Song content request is missing.");

            var releases = new List<Action>(4);
            ContentLoadResult<TextAsset> chart = await LoadAssetAsync<TextAsset>(request.ChartAddress, false, cancellationToken, releases);
            if (!chart.Succeeded) return FailAndRelease<LoadedSongContent>(chart.Error, chart.Message, releases);
            ContentLoadResult<AudioClip> audio = await LoadAssetAsync<AudioClip>(request.AudioAddress, false, cancellationToken, releases);
            if (!audio.Succeeded) return FailAndRelease<LoadedSongContent>(audio.Error, audio.Message, releases);
            ContentLoadResult<Sprite> jacket = await LoadAssetAsync<Sprite>(request.JacketAddress, false, cancellationToken, releases);
            if (!jacket.Succeeded) return FailAndRelease<LoadedSongContent>(jacket.Error, jacket.Message, releases);
            ContentLoadResult<VideoClip> video = await LoadAssetAsync<VideoClip>(request.VideoAddress, true, cancellationToken, releases);
            if (!video.Succeeded) return FailAndRelease<LoadedSongContent>(video.Error, video.Message, releases);

            return ContentLoadResult<LoadedSongContent>.Success(
                new LoadedSongContent(chart.Value, audio.Value, jacket.Value, video.Value, releases));
        }

        public IEnumerator LoadSong(SongContentRequest request, Action<ContentLoadResult<LoadedSongContent>> completed,
            CancellationToken cancellationToken = default)
        {
            Task<ContentLoadResult<LoadedSongContent>> task = LoadSongAsync(request, cancellationToken);
            while (!task.IsCompleted) yield return null;

            // LoadSongAsync translates Addressables exceptions to a failed result. The guard is for
            // unexpected task faults so a coroutine never throws in its caller.
            if (task.IsFaulted)
            {
                completed?.Invoke(ContentLoadResult<LoadedSongContent>.Failure(
                    ContentLoadError.AddressableFailed, task.Exception?.GetBaseException().Message));
                yield break;
            }
            completed?.Invoke(task.Result);
        }

        private static async Task<ContentLoadResult<T>> LoadAssetAsync<T>(string address, bool optional,
            CancellationToken cancellationToken, List<Action> releases) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return optional
                    ? ContentLoadResult<T>.Success(null)
                    : ContentLoadResult<T>.Failure(ContentLoadError.EmptyAddress, "A required content address is empty.");
            }
            if (cancellationToken.IsCancellationRequested)
                return ContentLoadResult<T>.Failure(ContentLoadError.Cancelled, "Content loading was cancelled.");

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(address);
            try
            {
                await handle.Task;
            }
            catch (Exception exception)
            {
                if (handle.IsValid()) Addressables.Release(handle);
                return ContentLoadResult<T>.Failure(ContentLoadError.AddressableFailed,
                    "Could not load content at '" + address + "': " + exception.Message);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                if (handle.IsValid()) Addressables.Release(handle);
                return ContentLoadResult<T>.Failure(ContentLoadError.Cancelled, "Content loading was cancelled.");
            }

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                if (handle.IsValid()) Addressables.Release(handle);
                return ContentLoadResult<T>.Failure(ContentLoadError.AddressableFailed,
                    "Could not load content at '" + address + "'.");
            }
            if (handle.Result == null)
            {
                if (handle.IsValid()) Addressables.Release(handle);
                return ContentLoadResult<T>.Failure(ContentLoadError.NullAsset,
                    "Content at '" + address + "' was empty.");
            }

            releases.Add(() =>
            {
                if (handle.IsValid()) Addressables.Release(handle);
            });
            return ContentLoadResult<T>.Success(handle.Result);
        }

        private static ContentLoadResult<T> FailAndRelease<T>(ContentLoadError error, string message, List<Action> releases)
        {
            for (int index = releases.Count - 1; index >= 0; index--) releases[index]();
            releases.Clear();
            return ContentLoadResult<T>.Failure(error, message);
        }
    }

    public static class ThemeMusicAddresses
    {
        public const string Title = "theme.title";
        public const string SongSelect = "theme.song-select";
        public const string Result = "theme.result";

        public const string TitleAssetPath = "Assets/Game/Content/Music/Theme-Title.mp3";
        public const string SongSelectAssetPath = "Assets/Game/Content/Music/Theme-SongSelect.mp3";
        public const string ResultAssetPath = "Assets/Game/Content/Music/Theme-Result.mp3";
    }
}
