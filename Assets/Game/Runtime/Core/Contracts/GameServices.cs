using System;

namespace DJMaximusKaiserSoje.Core
{
    /// <summary>
    /// The services a screen is handed when it loads. This is a parameter object built once by the
    /// bootstrap and passed down explicitly — screens never look services up for themselves.
    /// </summary>
    public sealed class GameServices
    {
        public GameServices(
            ISongLibrary songs,
            IRecordStore records,
            IPlayerProfile profile,
            IPlayPreferences preferences,
            IGameFlow flow,
            IMusicDirector music)
        {
            Songs = songs ?? throw new ArgumentNullException(nameof(songs));
            Records = records ?? throw new ArgumentNullException(nameof(records));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
            Flow = flow ?? throw new ArgumentNullException(nameof(flow));
            Music = music ?? throw new ArgumentNullException(nameof(music));
        }

        public ISongLibrary Songs { get; }
        public IRecordStore Records { get; }
        public IPlayerProfile Profile { get; }
        public IPlayPreferences Preferences { get; }
        public IGameFlow Flow { get; }
        public IMusicDirector Music { get; }
    }

    /// <summary>
    /// Implemented by the root object of every screen scene. The flow service finds it after the
    /// scene loads and binds it.
    /// </summary>
    public interface IScreenView
    {
        void Bind(GameServices services);
    }

    public interface ITitleScreenView : IScreenView
    {
    }

    public interface ISongSelectScreenView : IScreenView
    {
        /// <summary>Focuses a chart when the player comes back from a run they just played.</summary>
        void Focus(string songId, string chartId);
    }

    public interface IGameplayScreenView : IScreenView
    {
        void BindSession(IPlaySession session);
    }

    public interface IResultScreenView : IScreenView
    {
        void ShowResult(PlayResult result);
    }
}
