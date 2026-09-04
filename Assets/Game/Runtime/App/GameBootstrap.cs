using System;
using System.Collections;
using System.Collections.Generic;
using DJMaximusKaiserSoje.Content;
using DJMaximusKaiserSoje.Core;
using DJMaximusKaiserSoje.Gameplay;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DJMaximusKaiserSoje.App
{
    /// <summary>Boot-scene composition root. It is the only object that constructs game services.</summary>
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        public const string BootstrapCatalogAddress = "catalog.rhythm.bootstrap";
        public const string RemoteCatalogAddress = "catalog.rhythm.remote";

        [SerializeField] private string catalogAddress = BootstrapCatalogAddress;

        public GameServices Services { get; private set; }
        public SceneGameFlow Flow { get; private set; }

        private bool initialized;

        private void Awake()
        {
            GameBootstrap[] existing = FindObjectsByType<GameBootstrap>();
            if (existing.Length > 1)
            {
                Destroy(gameObject);
                return;
            }
            DontDestroyOnLoad(gameObject);
        }

        private IEnumerator Start()
        {
            // Without autoReleaseHandle: false the handle is disposed the moment it completes, and
            // reading its status afterwards throws.
            var initialization = Addressables.InitializeAsync(false);
            yield return initialization;
            bool contentReady = initialization.Status == AsyncOperationStatus.Succeeded;
            Addressables.Release(initialization);
            if (!contentReady)
            {
                Debug.LogError("Addressable content could not be initialized.");
                yield break;
            }

            AsyncOperationHandle<IList<UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation>> locations =
                Addressables.LoadResourceLocationsAsync(RemoteCatalogAddress, typeof(TextAsset));
            yield return locations;
            bool hasRemoteCatalog = locations.Status == AsyncOperationStatus.Succeeded &&
                                    locations.Result != null && locations.Result.Count > 0;
            if (locations.IsValid()) Addressables.Release(locations);

            AsyncOperationHandle<TextAsset> catalogHandle = hasRemoteCatalog
                ? Addressables.LoadAssetAsync<TextAsset>(RemoteCatalogAddress)
                : Addressables.LoadAssetAsync<TextAsset>(catalogAddress);
            yield return catalogHandle;
            if (catalogHandle.Status != AsyncOperationStatus.Succeeded || catalogHandle.Result == null)
            {
                if (catalogHandle.IsValid()) Addressables.Release(catalogHandle);
                if (!hasRemoteCatalog)
                {
                    Debug.LogError("The song catalog could not be loaded.");
                    yield break;
                }
                catalogHandle = Addressables.LoadAssetAsync<TextAsset>(catalogAddress);
                yield return catalogHandle;
                if (catalogHandle.Status != AsyncOperationStatus.Succeeded || catalogHandle.Result == null)
                {
                    if (catalogHandle.IsValid()) Addressables.Release(catalogHandle);
                    Debug.LogError("The song catalog could not be loaded.");
                    yield break;
                }
            }

            SongCatalogDocument catalog;
            try
            {
                catalog = SongCatalogParser.Parse(catalogHandle.Result.text);
            }
            catch (SongCatalogException exception)
            {
                Debug.LogError("The bootstrap song catalog is invalid: " + exception.Message);
                if (catalogHandle.IsValid()) Addressables.Release(catalogHandle);
                yield break;
            }

            if (catalogHandle.IsValid()) Addressables.Release(catalogHandle);

            var songLibrary = new CatalogSongLibrary(catalog);
            var contentLoader = new AddressablesContentLoader();
            var records = new JsonRecordStore();

            // The mixer opens at the size the player already chose, so the first song is heard at
            // the latency they settled on rather than at a default the options then rebuild.
            var audioDevice = GetComponent<FmodAudioDevice>() ?? gameObject.AddComponent<FmodAudioDevice>();
            audioDevice.SetBufferLength(PlayerPrefsPlayPreferences.StoredAudioBufferSize);

            var preferences = new PlayerPrefsPlayPreferences(audioDevice);
            var profile = new LocalPlayerProfile();
            var music = GetComponent<MusicDirector>() ?? gameObject.AddComponent<MusicDirector>();
            var factory = GetComponent<PlaySessionFactory>() ?? gameObject.AddComponent<PlaySessionFactory>();
            Flow = GetComponent<SceneGameFlow>() ?? gameObject.AddComponent<SceneGameFlow>();
            music.Configure(songLibrary, contentLoader, audioDevice);
            Services = new GameServices(songLibrary, records, profile, preferences, Flow, music);
            factory.Configure(songLibrary, contentLoader, records, audioDevice);
            Flow.Configure(Services, factory);
            initialized = true;
            Flow.ShowTitle();
        }

        public void RestartComposition()
        {
            if (!initialized || Flow == null) return;
            Flow.ShowTitle();
        }
    }
}
