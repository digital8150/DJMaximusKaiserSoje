using System;
using System.Collections.Generic;

namespace DJMaximusKaiserSoje.Core
{
    /// <summary>
    /// Identifiers for the selectable battle rhythm crew members.
    /// </summary>
    public static class BattleCrewId
    {
        public const string Aira = "aira";
        public const string Kai = "kai";
        public const string Lena = "lena";
        public const string Ren = "ren";

        public const string Default = Aira;

        public static readonly IReadOnlyList<string> All = new[]
        {
            Aira,
            Kai,
            Lena,
            Ren
        };

        public static bool IsValid(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            for (int i = 0; i < All.Count; i++)
            {
                if (string.Equals(All[i], id, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static string Normalize(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return Default;
            for (int i = 0; i < All.Count; i++)
            {
                if (string.Equals(All[i], id, StringComparison.OrdinalIgnoreCase)) return All[i];
            }
            return Default;
        }
    }
}
