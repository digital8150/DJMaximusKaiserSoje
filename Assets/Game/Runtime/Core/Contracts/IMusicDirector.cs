using System;

namespace DJMaximusKaiserSoje.Core
{
    public enum ScreenTheme
    {
        None,
        Title,
        SongSelect,
        Result
    }

    public static class MusicTiming
    {
        /// <summary>How long a song stays highlighted before its preview replaces the theme.</summary>
        public const double PreviewDwellSeconds = 0.5;

        public const double CrossfadeSeconds = 0.35;

        public const double PreviewLengthSeconds = 25.0;

        /// <summary>Where a preview starts in a chart that declares no preview point.</summary>
        public const double PreviewFallbackPosition01 = 0.4;
    }

    /// <summary>
    /// Owns whatever is audible outside gameplay. The select screen reports which song is
    /// highlighted and this decides when a preview is worth interrupting the theme for.
    /// </summary>
    public interface IMusicDirector
    {
        ScreenTheme CurrentTheme { get; }

        /// <summary>The song being previewed, or null while the screen theme is playing.</summary>
        string PreviewingSongId { get; }

        void PlayTheme(ScreenTheme theme);

        void StopTheme();

        /// <summary>
        /// Asks for a preview of a song. Held for <see cref="MusicTiming.PreviewDwellSeconds"/> so
        /// scrolling past a song does not start it; a newer request replaces an unstarted one.
        /// </summary>
        void RequestSongPreview(string songId);

        void CancelSongPreview();

        event Action<string> PreviewStarted;

        event Action PreviewStopped;
    }
}
