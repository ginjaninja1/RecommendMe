namespace RecommendMe.Services
{
    internal enum SendPermissionResult
    {
        Allowed,
        AdminBlocked,
        RecipientBlockedSender,
        RecipientOptedOutMediaType
    }
}
