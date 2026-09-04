namespace DJMaximusKaiserSoje.Core
{
    /// <summary>Who is playing, as shown on the gameplay card and the result screen.</summary>
    public interface IPlayerProfile
    {
        string DisplayName { get; }

        /// <summary>The short handle printed under the name.</summary>
        string Tag { get; }

        int Level { get; }

        /// <summary>Progress toward the next level, 0.0 to 1.0.</summary>
        double ExpProgress01 { get; }
    }
}
