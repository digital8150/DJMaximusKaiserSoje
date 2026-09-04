using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DJMaximusKaiserSoje.Editor
{
    /// <summary>
    /// Names imported content. Ids reach the shipped catalog and the Addressables keys built from
    /// it, so they have to stay stable across re-imports: a set keeps the id derived from its osu!
    /// set number, and only the tracks inside a pack get a suffix.
    /// </summary>
    public static class OszSongId
    {
        /// <summary>
        /// Prefix shared by everything one archive yields. A single-song set uses it unchanged, so
        /// re-importing one does not renumber content that is already published.
        /// </summary>
        public static string ForArchive(string oszPath, string artist, string title)
        {
            Match setNumber = Regex.Match(Path.GetFileNameWithoutExtension(oszPath) ?? string.Empty, @"^\s*(\d+)");
            return setNumber.Success
                ? "osu-" + setNumber.Groups[1].Value
                : Slug(artist + "-" + title, "imported-song");
        }

        /// <summary>Picks an unused id and marks it taken.</summary>
        public static string Reserve(string setId, string title, bool isPack, ISet<string> takenIds)
        {
            string candidate = isPack ? setId + "-" + Slug(title, "track") : setId;
            string songId = candidate;
            for (int suffix = 2; !takenIds.Add(songId); suffix++)
                songId = candidate + "-" + suffix;
            return songId;
        }

        /// <summary>True for the set's own id and for any pack track imported from it.</summary>
        public static bool BelongsToSet(string songId, string setId) =>
            string.Equals(songId, setId, StringComparison.Ordinal) ||
            (songId != null && songId.StartsWith(setId + "-", StringComparison.Ordinal));

        public static string Slug(string value, string fallback)
        {
            string slug = Regex.Replace((value ?? string.Empty).ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
            return string.IsNullOrEmpty(slug) ? fallback : slug;
        }
    }
}
