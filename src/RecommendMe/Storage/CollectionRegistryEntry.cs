namespace RecommendMe.Storage
{
    internal class CollectionRegistryEntry
    {
        public long UserId { get; set; }

        public long CollectionId { get; set; }

        public string EmbyCollectionId { get; set; } = string.Empty;
    }
}
