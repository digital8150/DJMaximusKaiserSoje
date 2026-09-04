using System;

namespace DJMaximusKaiserSoje.Core
{
    /// <summary>Personal bests, kept per chart and survived between sessions.</summary>
    public interface IRecordStore
    {
        /// <summary>Never null; an unplayed chart yields <see cref="ChartRecord.Empty"/>.</summary>
        ChartRecord Get(string chartId);

        /// <summary>Merges a finished run into the stored best and returns the merged record.</summary>
        ChartRecord Submit(PlayResult result);

        event Action<ChartRecord> RecordChanged;
    }
}
