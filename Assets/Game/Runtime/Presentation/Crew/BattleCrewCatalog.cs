using System;
using System.Collections.Generic;
using DJMaximusKaiserSoje.Core;
using UnityEngine;

namespace DJMaximusKaiserSoje.Presentation
{
    [CreateAssetMenu(fileName = "BattleCrewCatalog", menuName = "Rhythm/Battle Crew Catalog")]
    public sealed class BattleCrewCatalog : ScriptableObject
    {
        [SerializeField] private BattleCrewData[] crews = Array.Empty<BattleCrewData>();

        public IReadOnlyList<BattleCrewData> Crews => crews ?? Array.Empty<BattleCrewData>();

        public bool TryGetCrew(string id, out BattleCrewData result)
        {
            result = null;
            if (crews == null || string.IsNullOrWhiteSpace(id)) return false;

            for (int i = 0; i < crews.Length; i++)
            {
                if (crews[i] != null && string.Equals(crews[i].Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    result = crews[i];
                    return true;
                }
            }
            return false;
        }

        public BattleCrewData GetCrewOrDefault(string id)
        {
            if (TryGetCrew(id, out var found)) return found;
            if (TryGetCrew(BattleCrewId.Default, out var defaultCrew)) return defaultCrew;
            return crews != null && crews.Length > 0 ? crews[0] : null;
        }

        public void SetCrews(BattleCrewData[] newCrews)
        {
            crews = newCrews;
        }
    }
}
