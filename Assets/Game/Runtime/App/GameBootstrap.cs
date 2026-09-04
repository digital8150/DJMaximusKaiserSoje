using System;
using System.Collections;
using DJMaximusKaiserSoje.Content;
using DJMaximusKaiserSoje.Core;
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

            AsyncOperationHandle<TextAsset> catalogHandle = Addressables.LoadAssetAsync<TextAsset>(catalogAddress);
            yield return catalogHandle;
            if (catalogHandle.Status != AsyncOperationStatus.Succeeded || catalogHandle.Result == null)
            {
                Debug.LogError("The bootstrap song catalog could not be loaded.");
                yield break;
            }

            SongCatalogDocument catalog;
            try
            {
                catalog = SongCatalogParser.Parse(catalogHandle.Result.text);
            }
            catch (SongCatalogException exception)
            {
                Debug.LogError("The bootstrap song catalog is invalid: " + exception.Message);
                yield break;
            }

            var songLibrary = new CatalogSongLibrary(catalog);
            var contentLoader = new AddressablesContentLoader();
            var records = new JsonRecordStore();
            var preferences = new PlayerPrefsPlayPreferences();
            var profile = new LocalPlayerProfile();
            var music = GetComponent<MusicDirector>() ?? gameObject.AddComponent<MusicDirector>();
            var factory = GetComponent<PlaySessionFactory>() ?? gameObject.AddComponent<PlaySessionFactory>();
            Flow = GetComponent<SceneGameFlow>() ?? gameObject.AddComponent<SceneGameFlow>();
            music.Configure(songLibrary, contentLoader);
            Services = new GameServices(songLibrary, records, profile, preferences, Flow, music);
            factory.Configure(songLibrary, contentLoader, records);
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
