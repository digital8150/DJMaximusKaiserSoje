using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DJMaximusKaiserSoje.Editor
{
    /// <summary>
    /// A pack .osz stores one shared set name in [Metadata] ("Various Artists - 6K Starter Pack")
    /// and hides each real track name in the difficulty's Version line. Mappers write that line in a
    /// few community conventions:
    /// <c>Artist - Title (Mapper's 9)</c>, <c>Artist - Title [17]</c>, <c>[10] Artist - Title</c>.
    /// This splits the line back into artist, title, and difficulty so a pack imports as separate
    /// songs instead of one blob. Only the difficulty marker is stripped: a bracketed part that
    /// belongs to the title, such as "(Cut Ver.)", stays where it is.
    /// </summary>
    public sealed class OszTrackLabel
    {
        private const string ArtistSeparator = " - ";

        private static readonly Regex LeadingLevel = new Regex(@"^\[\s*(\d{1,2})\s*\]\s*", RegexOptions.Compiled);
        private static readonly Regex TrailingGroup = new Regex(@"\s*[(\[]([^()\[\]]*)[)\]]\s*$", RegexOptions.Compiled);
        private static readonly Regex NumericTag = new Regex(@"^\d{1,2}$", RegexOptions.Compiled);

        /// <summary>Trailing group ends in a number ("Arkman's 11") or is a chart-style marker.</summary>
        private static readonly Regex RatedTag = new Regex(@"\d\s*$", RegexOptions.Compiled);

        private static readonly HashSet<string> DifficultyWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ln", "sv", "rc", "marathon", "beginner", "easy", "normal", "hard",
            "hyper", "another", "insane", "expert", "maximum", "extra"
        };

        private OszTrackLabel(string artist, string title, string difficulty)
        {
            Artist = artist;
            Title = title;
            Difficulty = difficulty;
        }

        /// <summary>Artist part of the label, or null when the line names no artist.</summary>
        public string Artist { get; }

        /// <summary>Track name with the difficulty marker removed. Never null.</summary>
        public string Title { get; }

        /// <summary>Difficulty marker taken off the line, or null when it carried none.</summary>
        public string Difficulty { get; }

        public static OszTrackLabel Parse(string version)
        {
            string value = (version ?? string.Empty).Trim();
            var tags = new List<string>();

            Match leading = LeadingLevel.Match(value);
            if (leading.Success)
            {
                tags.Add(leading.Groups[1].Value);
                value = value.Substring(leading.Length).Trim();
            }

            while (true)
            {
                Match trailing = TrailingGroup.Match(value);
                if (!trailing.Success) break;
                string tag = trailing.Groups[1].Value.Trim();
                if (!IsDifficultyTag(tag)) break;
                tags.Insert(0, tag);
                value = value.Substring(0, trailing.Index).TrimEnd();
            }

            int separator = value.IndexOf(ArtistSeparator, StringComparison.Ordinal);
            string artist = separator > 0 ? value.Substring(0, separator).Trim() : null;
            string title = separator > 0 ? value.Substring(separator + ArtistSeparator.Length).Trim() : value;
            return new OszTrackLabel(EmptyToNull(artist), title, FormatDifficulty(tags));
        }

        private static bool IsDifficultyTag(string tag) =>
            tag.Length > 0 && (RatedTag.IsMatch(tag) || DifficultyWords.Contains(tag));

        private static string FormatDifficulty(IReadOnlyList<string> tags)
        {
            if (tags.Count == 0) return null;
            var parts = new List<string>(tags.Count);
            foreach (string tag in tags)
                parts.Add(NumericTag.IsMatch(tag) ? "Lv." + tag : tag);
            return string.Join(" ", parts.ToArray());
        }

        private static string EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
