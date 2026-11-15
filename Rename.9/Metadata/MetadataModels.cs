using System;

namespace Episode_Names.Metadata
{
    internal sealed class SeriesMatch
    {
        public string ProviderId { get; private set; }
        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Language { get; private set; }
        public string WebUrl { get; private set; }
        public int? PremiereYear { get; private set; }

        public SeriesMatch(string providerId, string id, string name, string language, string webUrl, int? premiereYear)
        {
            ProviderId = providerId;
            Id = id;
            Name = name;
            Language = language;
            WebUrl = webUrl;
            PremiereYear = premiereYear;
        }
    }

    internal sealed class SeasonMetadata
    {
        public int Number { get; private set; }
        public string ProviderSeasonId { get; private set; }
        public string Name { get; private set; }

        public SeasonMetadata(int number, string providerSeasonId, string name)
        {
            Number = number;
            ProviderSeasonId = providerSeasonId;
            Name = name;
        }
    }

    internal sealed class EpisodeMetadata
    {
        public string ProviderId { get; private set; }
        public string ProviderEpisodeId { get; private set; }
        public int SeasonNumber { get; private set; }
        public int? EpisodeNumber { get; private set; }
        // Only populated if a provider explicitly supplies a reliable absolute number.
        public int? AbsoluteNumber { get; private set; }
        public string Title { get; private set; }
        public string Language { get; private set; }
        public bool IsSpecial { get; private set; }

        public EpisodeMetadata(string providerId, string providerEpisodeId, int seasonNumber,
            int? episodeNumber, int? absoluteNumber, string title, string language, bool isSpecial)
        {
            ProviderId = providerId;
            ProviderEpisodeId = providerEpisodeId;
            SeasonNumber = seasonNumber;
            EpisodeNumber = episodeNumber;
            AbsoluteNumber = absoluteNumber;
            Title = title;
            Language = language;
            IsSpecial = isSpecial;
        }
    }

    internal sealed class MetadataProviderException : Exception
    {
        public MetadataProviderException(string message) : base(message) { }
        public MetadataProviderException(string message, Exception innerException) : base(message, innerException) { }
    }
}
