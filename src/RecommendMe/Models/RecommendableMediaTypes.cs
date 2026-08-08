using System.Collections.Generic;

namespace RecommendMe.Models
{
    internal static class RecommendableMediaTypes
    {
        public const string Movie = "Movie";
        public const string Series = "Series";
        public const string Season = "Season";
        public const string Episode = "Episode";
        public const string Person = "Person";
        public const string MusicArtist = "MusicArtist";
        public const string MusicAlbum = "MusicAlbum";
        public const string Song = "Audio";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Movie, Person, Series, Season, Episode, MusicArtist, MusicAlbum, Song
        };
    }
}
