using System;
using System.Threading.Tasks;
using DJMaximusKaiserSoje.Content;
using DJMaximusKaiserSoje.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DJMaximusKaiserSoje.App
{
    public static class SceneNames
    {
        public const string Boot = "Boot";
        public const string Title = "Title";
        public const string SongSelect = "SongSelect";
        public const string Options = "Options";
        public const string Gameplay = "Gameplay";
        public const string Result = "Result";
    }

    /// <summary>Loads one authored screen at a time and binds only its frozen screen contract.</summary>
    public sealed class SceneGameFlow : MonoBehaviour, IGameFlow
    {
        private GameServices services;
        private PlaySessionFactory sessionFactory;
        private PlayRequest lastRequest;
        private IPlaySession pendingSession;
        private PlayResult pendingResult;
        private string pendingFocusSongId;
        private string pendingFocusChartId;
        private bool loading;
        private string optionsReturnScene = SceneNames.Title;
        private SceneFadeTransition fadeTransition;

        public bool CanRetry => lastRequest != null;

        public void Configure(GameServices gameServices, PlaySessionFactory factory)
        {
            services = gameServices ?? throw new ArgumentNullException(nameof(gameServices));
            sessionFactory = factory ?? throw new ArgumentNullException(nameof(factory));
            fadeTransition = GetComponent<SceneFadeTransition>() ?? gameObject.AddComponent<SceneFadeTransition>();
        }

        public void ShowTitle() => Load(SceneNames.Title);

        public void ShowSongSelect() => Load(SceneNames.SongSelect);

        public void ShowOptions()
        {
            string current = SceneManager.GetActiveScene().name;
            optionsReturnScene = current == SceneNames.SongSelect ? SceneNames.SongSelect : SceneNames.Title;
            Load(SceneNames.Options);
        }

        public void CloseOptions() => Load(optionsReturnScene);

        public void StartPlay(PlayRequest request)
        {
            if (request == null || loading || sessionFactory == null) return;
            lastRequest = request;
            services?.Music.StopTheme();
            loading = true;
            StartCoroutine(CreateSessionThenLoad(request));
        }

        public void ShowResult(PlayResult result)
        {
            if (result == null || loading) return;
            pendingResult = result;
            pendingSession = null;
            pendingFocusSongId = result.SongId;
            pendingFocusChartId = result.ChartId;
            services?.Records.Submit(result);
            loading = true;
            StartCoroutine(LoadResultWithFade());
        }

        public void RetryLast()
        {
            if (lastRequest != null) StartPlay(lastRequest);
        }

        private System.Collections.IEnumerator CreateSessionThenLoad(PlayRequest request)
        {
            Task<ContentLoadResult<IPlaySession>> task = sessionFactory.CreateAsync(request);
            while (!task.IsCompleted) yield return null;
            loading = false;
            if (task.IsFaulted || !task.Result.Succeeded)
            {
                Load(SceneNames.SongSelect);
                yield break;
            }

            pendingSession = task.Result.Value;
            pendingFocusSongId = null;
            pendingFocusChartId = null;
            Load(SceneNames.Gameplay);
        }

        private System.Collections.IEnumerator LoadResultWithFade()
        {
            if (fadeTransition != null) yield return fadeTransition.FadeToBlack();

            AsyncOperation operation = SceneManager.LoadSceneAsync(SceneNames.Result, LoadSceneMode.Single);
            if (operation == null)
            {
                loading = false;
                if (fadeTransition != null) yield return fadeTransition.FadeFromBlack();
                yield break;
            }

            yield return operation;
            yield return null;
            if (fadeTransition != null) yield return fadeTransition.FadeFromBlack();
            loading = false;
        }

        private void Load(string sceneName)
        {
            if (loading && sceneName != SceneNames.Gameplay) return;
            loading = true;
            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single).completed += _ => loading = false;
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (services == null) return;
            IScreenView screen = FindScreenView();
            if (screen == null) return;
            screen.Bind(services);

            if (screen is IGameplayScreenView gameplay && pendingSession != null)
            {
                gameplay.BindSession(pendingSession);
                pendingSession = null;
            }
            else if (screen is IResultScreenView result && pendingResult != null)
            {
                result.ShowResult(pendingResult);
                pendingResult = null;
            }
            else if (screen is ISongSelectScreenView select && pendingFocusSongId != null)
            {
                select.Focus(pendingFocusSongId, pendingFocusChartId);
                pendingFocusSongId = null;
                pendingFocusChartId = null;
            }
        }

        private static IScreenView FindScreenView()
        {
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>();
            for (int index = 0; index < behaviours.Length; index++)
                if (behaviours[index] is IScreenView view) return view;
            return null;
        }
    }
}
