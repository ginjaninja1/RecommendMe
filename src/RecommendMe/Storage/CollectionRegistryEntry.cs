namespace RecommendMe.Storage
{
    internal class CollectionRegistryEntry
    {
        public long UserId { get; set; }

        public string UserName { get; set; } = string.Empty;

        public long CollectionId { get; set; }

        public string CollectionName { get; set; } = string.Empty;

        public string EmbyCollectionId { get; set; } = string.Empty;
    }
}
