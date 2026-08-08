namespace RecommendMe.Services
{
    internal enum RecommendationSendResult
    {
        Success,
        NotPermitted,
        RecipientBlockedSender,
        RecipientOptedOutMediaType,
        AlreadyWatchedByRecipient,
        AlreadyInRecipientCollection
    }
}
