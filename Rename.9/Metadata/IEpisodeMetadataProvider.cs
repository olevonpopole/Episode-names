using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Episode_Names.Metadata
{
    /// <summary>A replaceable source of television/anime series and episode metadata.</summary>
    internal interface IEpisodeMetadataProvider
    {
        string Id { get; }
        string DisplayName { get; }
        bool RequiresApiKey { get; }

        Task<IReadOnlyList<SeriesMatch>> SearchSeriesAsync(string query, CancellationToken cancellationToken);
        Task<IReadOnlyList<SeasonMetadata>> GetSeasonsAsync(string seriesId, CancellationToken cancellationToken);
        Task<IReadOnlyList<EpisodeMetadata>> GetEpisodesAsync(string seriesId, bool includeSpecials, CancellationToken cancellationToken);
    }
}
