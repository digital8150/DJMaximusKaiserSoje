using System;
using System.Collections.Generic;
using System.IO;
using DJMaximusKaiserSoje.Core;
using UnityEngine;

namespace DJMaximusKaiserSoje.App
{
    /// <summary>JSON persistence with a deliberately forgiving read path for damaged local data.</summary>
    public sealed class JsonRecordStore : IRecordStore
    {
        public const int CurrentSchemaVersion = 2;
        private readonly string filePath;
        private readonly Dictionary<string, ChartRecord> records = new Dictionary<string, ChartRecord>(StringComparer.Ordinal);

        public JsonRecordStore() : this(Path.Combine(Application.persistentDataPath, "chart-records.json"))
        {
        }

        public JsonRecordStore(string filePath)
        {
            this.filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            Load();
        }

        public event Action<ChartRecord> RecordChanged;

        public ChartRecord Get(string chartId)
        {
            if (string.IsNullOrWhiteSpace(chartId)) throw new ArgumentException("Chart id is required.", nameof(chartId));
            return records.TryGetValue(chartId, out ChartRecord record) ? record : ChartRecord.Empty(chartId);
        }

        public void PurgeAll()
        {
            records.Clear();
            Save();
        }

        public ChartRecord Submit(PlayResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            ChartRecord previous = Get(result.ChartId);
            RunScore score = result.Score;
            bool scoreIsBetter = score.Score > previous.BestScore;
            bool accuracyIsBetter = score.Accuracy01 > previous.BestAccuracy01;
            bool ratingIsBetter = score.Rating > previous.BestRating;
            bool comboIsBetter = score.MaxCombo > previous.BestCombo;

            var merged = new ChartRecord(
                result.ChartId,
                scoreIsBetter ? score.Score : previous.BestScore,
                accuracyIsBetter ? score.Accuracy01 : previous.BestAccuracy01,
                ratingIsBetter ? score.Rating : previous.BestRating,
                comboIsBetter ? score.MaxCombo : previous.BestCombo,
                accuracyIsBetter ? result.Rank : previous.BestRank,
                previous.Cleared || result.Outcome == PlayOutcome.Cleared,
                previous.PlayCount + 1);
            records[result.ChartId] = merged;
            Save();
            RecordChanged?.Invoke(merged);
            return merged;
        }

        private void Load()
        {
            records.Clear();
            if (!File.Exists(filePath)) return;
            try
            {
                string json = File.ReadAllText(filePath);
                var document = JsonUtility.FromJson<RecordDocument>(json);
                if (document == null || document.schemaVersion != CurrentSchemaVersion || document.records == null)
                {
                    records.Clear();
                    Save();
                    return;
                }
                for (int index = 0; index < document.records.Length; index++)
                {
                    StoredRecord stored = document.records[index];
                    if (stored == null || string.IsNullOrWhiteSpace(stored.chartId) || stored.playCount < 0) continue;
                    records[stored.chartId] = stored.ToRecord();
                }
            }
            catch (Exception)
            {
                records.Clear();
            }
        }

        private void Save()
        {
            try
            {
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                var document = new RecordDocument
                {
                    schemaVersion = CurrentSchemaVersion,
                    records = new StoredRecord[records.Count]
                };
                int index = 0;
                foreach (ChartRecord record in records.Values)
                    document.records[index++] = StoredRecord.FromRecord(record);
                File.WriteAllText(filePath, JsonUtility.ToJson(document));
            }
            catch (Exception)
            {
                // The in-memory merge remains valid for this session. A later submit can retry.
            }
        }

        [Serializable]
        private sealed class RecordDocument
        {
            public int schemaVersion;
            public StoredRecord[] records;
        }

        [Serializable]
        private sealed class StoredRecord
        {
            public string chartId;
            public long bestScore;
            public double bestAccuracy01;
            public double bestRating;
            public int bestCombo;
            public Rank bestRank;
            public bool cleared;
            public int playCount;

            public ChartRecord ToRecord() => new ChartRecord(chartId, bestScore, bestAccuracy01, bestRating, bestCombo, bestRank, cleared, playCount);

            public static StoredRecord FromRecord(ChartRecord record) => new StoredRecord
            {
                chartId = record.ChartId,
                bestScore = record.BestScore,
                bestAccuracy01 = record.BestAccuracy01,
                bestRating = record.BestRating,
                bestCombo = record.BestCombo,
                bestRank = record.BestRank,
                cleared = record.Cleared,
                playCount = record.PlayCount
            };
        }
    }
}
