using System;

namespace RecommendMe.UI.Admin
{
    internal static class AdminCommands
    {
        public const string PageSave = "PageSave";

        private const string SendScopeModePrefix = "sendscopemode:";
        private const string SendScopeUserPrefix = "sendscopeuser:";
        private const string ReceiveScopeModePrefix = "receivescopemode:";
        private const string ReceiveScopeUserPrefix = "receivescopeuser:";
        private const string MediaTypePrefix = "mediatype:";
        private const string DefaultSendingPrefix = "defaultsending";
        private const string DefaultReceivingPrefix = "defaultreceiving";
        private const string DefaultMediaTypePrefix = "defaultmediatype:";
        private const string UserSendingPrefix = "usersending:";
        private const string UserReceivingPrefix = "userreceiving:";
        private const string UserMediaTypeSeparator = "|||";
        private const string UserMediaTypePrefix = "usermediatype:";

        public static string BuildSendScopeModeToggle() => SendScopeModePrefix;

        public static string BuildSendScopeUserToggle(long userId) => $"{SendScopeUserPrefix}{userId}";

        public static string BuildReceiveScopeModeToggle() => ReceiveScopeModePrefix;

        public static string BuildReceiveScopeUserToggle(long userId) => $"{ReceiveScopeUserPrefix}{userId}";

        public static string BuildMediaTypeToggle(string mediaType) => $"{MediaTypePrefix}{mediaType}";

        public static string BuildDefaultMediaTypeToggle(string mediaType) => $"{DefaultMediaTypePrefix}{mediaType}";

        public static string BuildUserSendingToggle(long userId) => $"{UserSendingPrefix}{userId}";

        public static string BuildUserReceivingToggle(long userId) => $"{UserReceivingPrefix}{userId}";

        public static string BuildUserMediaTypeToggle(long userId, string mediaType) =>
            $"{UserMediaTypePrefix}{userId}{UserMediaTypeSeparator}{mediaType}";

        public static bool IsSendScopeModeToggle(string commandId) => commandId == SendScopeModePrefix;

        public static bool IsReceiveScopeModeToggle(string commandId) => commandId == ReceiveScopeModePrefix;

        public static bool IsDefaultSendingToggle(string commandId) => commandId == DefaultSendingPrefix;

        public static bool IsDefaultReceivingToggle(string commandId) => commandId == DefaultReceivingPrefix;

        public static bool TryParsePrefixed(string commandId, string prefix, out long id)
        {
            id = 0;
            if (commandId == null || !commandId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return long.TryParse(commandId.Substring(prefix.Length), out id);
        }

        public static bool TryParseSendScopeUser(string commandId, out long userId) =>
            TryParsePrefixed(commandId, SendScopeUserPrefix, out userId);

        public static bool TryParseReceiveScopeUser(string commandId, out long userId) =>
            TryParsePrefixed(commandId, ReceiveScopeUserPrefix, out userId);

        public static bool TryParseUserSending(string commandId, out long userId) =>
            TryParsePrefixed(commandId, UserSendingPrefix, out userId);

        public static bool TryParseUserReceiving(string commandId, out long userId) =>
            TryParsePrefixed(commandId, UserReceivingPrefix, out userId);

        public static bool TryParseMediaType(string commandId, out string mediaType)
        {
            mediaType = null;
            if (commandId == null || !commandId.StartsWith(MediaTypePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            mediaType = commandId.Substring(MediaTypePrefix.Length);
            return true;
        }

        public static bool TryParseDefaultMediaType(string commandId, out string mediaType)
        {
            mediaType = null;
            if (commandId == null || !commandId.StartsWith(DefaultMediaTypePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            mediaType = commandId.Substring(DefaultMediaTypePrefix.Length);
            return true;
        }

        public static bool TryParseUserMediaType(string commandId, out long userId, out string mediaType)
        {
            userId = 0;
            mediaType = null;

            if (commandId == null || !commandId.StartsWith(UserMediaTypePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var payload = commandId.Substring(UserMediaTypePrefix.Length);
            var parts = payload.Split(new[] { UserMediaTypeSeparator }, StringSplitOptions.None);
            if (parts.Length != 2 || !long.TryParse(parts[0], out userId))
            {
                return false;
            }

            mediaType = parts[1];
            return true;
        }
    }
}
