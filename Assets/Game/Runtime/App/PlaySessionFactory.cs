using System;
using System.Threading;
using System.Threading.Tasks;
using DJMaximusKaiserSoje.Content;
using DJMaximusKaiserSoje.Core;
using DJMaximusKaiserSoje.Gameplay;
using UnityEngine;

namespace DJMaximusKaiserSoje.App
{
    /// <summary>Turns a select-screen request into a content-backed session with its own driver.</summary>
    public sealed class PlaySessionFactory : MonoBehaviour
    {
        private CatalogSongLibrary songs;
        private IContentLoader contentLoader;
        private IRecordStore records;
        private FmodAudioDevice audioDevice;
        private PlaySessionDriver activeDriver;

        public void Configure(CatalogSongLibrary library, IContentLoader loader, IRecordStore recordStore,
            FmodAudioDevice device)
        {
            songs = library ?? throw new ArgumentNullException(nameof(library));
            contentLoader = loader ?? throw new ArgumentNullException(nameof(loader));
            records = recordStore ?? throw new ArgumentNullException(nameof(recordStore));
            if (audioDevice != null) audioDevice.Closing -= DestroyActiveSession;
            audioDevice = device ?? throw new ArgumentNullException(nameof(device));
            // A run cannot outlive the mixer its song was created on.
            audioDevice.Closing += DestroyActiveSession;
        }

        private void OnDestroy()
        {
            if (audioDevice != null) audioDevice.Closing -= DestroyActiveSession;
        }

        public async Task<ContentLoadResult<IPlaySession>> CreateAsync(
            PlayRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
                return ContentLoadResult<IPlaySession>.Failure(ContentLoadError.InvalidRequest, "Play request is missing.");
            if (songs == null || contentLoader == null || records == null || audioDevice == null)
                return ContentLoadResult<IPlaySession>.Failure(ContentLoadError.InvalidRequest, "Play session factory is not configured.");
            if (audioDevice.Output == null)
                return ContentLoadResult<IPlaySession>.Failure(ContentLoadError.InvalidRequest, "게임 소리를 사용할 수 없습니다.");
            if (!songs.TryGetContent(request.ChartId, out SongContentAddresses addresses))
                return ContentLoadResult<IPlaySession>.Failure(ContentLoadError.InvalidRequest, "The selected chart is not available.");

            ContentLoadResult<LoadedSongContent> loaded;
            try
            {
                loaded = await contentLoader.LoadSongAsync(new SongContentRequest(
                    addresses.ChartAddress, addresses.AudioAddress, addresses.JacketAddress, addresses.VideoAddress), cancellationToken);
            }
            catch (Exception exception)
            {
                return ContentLoadResult<IPlaySession>.Failure(ContentLoadError.AddressableFailed, exception.Message);
            }
            if (!loaded.Succeeded) return ContentLoadResult<IPlaySession>.Failure(loaded.Error, loaded.Message);

            if (!songs.TryGetChart(request.ChartId, out ChartSummary chart))
            {
                loaded.Value.Dispose();
                return ContentLoadResult<IPlaySession>.Failure(ContentLoadError.InvalidRequest, "The selected chart is not available.");
            }
            if (!PlayStyleChartCompatibility.IsCompatible(request.Style, chart))
            {
                loaded.Value.Dispose();
                return ContentLoadResult<IPlaySession>.Failure(ContentLoadError.InvalidRequest,
                    "The selected chart does not match the selected key mode.");
            }
            if (!string.Equals(chart.SongId, request.SongId, StringComparison.Ordinal))
            {
                loaded.Value.Dispose();
                return ContentLoadResult<IPlaySession>.Failure(ContentLoadError.InvalidRequest, "The selected song and chart do not match.");
            }

            DestroyActiveSession();
            var runtimeObject = new GameObject("PlaySessionRuntime");
            runtimeObject.transform.SetParent(transform, false);

            IAudioPlayback audio;
            IDspTimeSource dspTime;
            try
            {
                FmodOutput output = audioDevice.Output;
                audio = FmodAudioPlayback.Create(loaded.Value.AudioBytes, output);
                dspTime = new FmodDspTimeSource(output);
            }
            catch (Exception exception)
            {
                loaded.Value.Dispose();
                Destroy(runtimeObject);
                return ContentLoadResult<IPlaySession>.Failure(ContentLoadError.InvalidRequest, exception.Message);
            }

            var input = new LaneInput(LaneLayout.Create(request.Style), request.KeyBindings);
            PlaySession session;
            try
            {
                session = new PlaySession(
                    request.SongId,
                    chart.Id,
                    request.Style,
                    new OsuManiaBeatmapParser().Parse(loaded.Value.ChartText.text),
                    audio,
                    input,
                    dspTime,
                    records,
                    request.JudgementOffsetMs,
                    inputTime: new InputSystemTimeSource());
            }
            catch (BeatmapParseException exception)
            {
                input.Dispose();
                audio.Dispose();
                loaded.Value.Dispose();
                Destroy(runtimeObject);
                return ContentLoadResult<IPlaySession>.Failure(ContentLoadError.ParseFailed, exception.Message);
            }
            catch (Exception exception)
            {
                input.Dispose();
                audio.Dispose();
                loaded.Value.Dispose();
                Destroy(runtimeObject);
                return ContentLoadResult<IPlaySession>.Failure(ContentLoadError.InvalidRequest, exception.Message);
            }

            try
            {
                activeDriver = runtimeObject.AddComponent<PlaySessionDriver>();
                activeDriver.Bind(session, loaded.Value);
                session.Start();
                return ContentLoadResult<IPlaySession>.Success(session);
            }
            catch (Exception exception)
            {
                session.Dispose();
                loaded.Value.Dispose();
                Destroy(runtimeObject);
                activeDriver = null;
                return ContentLoadResult<IPlaySession>.Failure(ContentLoadError.InvalidRequest, exception.Message);
            }
        }

        public void DestroyActiveSession()
        {
            if (activeDriver == null) return;
            activeDriver.ReleaseNow();
            Destroy(activeDriver.gameObject);
            activeDriver = null;
        }
    }
}
