using System;
using RecommendMe.Models;

namespace RecommendMe.UI.Admin
{
    internal static class AdminCommands
    {
        public const string PageSave = "PageSave";

        private const string MediaTypePrefix = "mediatype:";
        private const string NewUserDefaultSendModePrefix = "newuserdefaultsendmode";
        private const string AutoGrantPrefix = "autogrant";
        private const string UserSuspendedPrefix = "usersuspended:";
        private const string UserSendModeSeparator = "|||";
        private const string UserSendModePrefix = "usersendmode:";
        private const string UserTargetSeparator = "|||";
        private const string UserTargetPrefix = "usertarget:";

        public static string BuildMediaTypeToggle(string mediaType) => $"{MediaTypePrefix}{mediaType}";

        public static string BuildNewUserDefaultSendModeToggle() => NewUserDefaultSendModePrefix;

        public static string BuildAutoGrantToggle() => AutoGrantPrefix;

        public static string BuildUserSuspendedToggle(long userId) => $"{UserSuspendedPrefix}{userId}";

        public static string BuildUserSendModeCommand(long userId, SendMode mode) =>
            $"{UserSendModePrefix}{userId}{UserSendModeSeparator}{mode}";

        public static string BuildUserTargetToggle(long userId, long targetUserId) =>
            $"{UserTargetPrefix}{userId}{UserTargetSeparator}{targetUserId}";

        public static bool IsNewUserDefaultSendModeToggle(string commandId) => commandId == NewUserDefaultSendModePrefix;

        public static bool IsAutoGrantToggle(string commandId) => commandId == AutoGrantPrefix;

        public static bool TryParsePrefixed(string commandId, string prefix, out long id)
        {
            id = 0;
            if (commandId == null || !commandId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return long.TryParse(commandId.Substring(prefix.Length), out id);
        }

        public static bool TryParseUserSuspended(string commandId, out long userId) =>
            TryParsePrefixed(commandId, UserSuspendedPrefix, out userId);

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

        public static bool TryParseUserSendMode(string commandId, out long userId, out SendMode mode)
        {
            userId = 0;
            mode = SendMode.Everyone;

            if (commandId == null || !commandId.StartsWith(UserSendModePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var payload = commandId.Substring(UserSendModePrefix.Length);
            var parts = payload.Split(new[] { UserSendModeSeparator }, StringSplitOptions.None);
            if (parts.Length != 2 || !long.TryParse(parts[0], out userId) || !Enum.TryParse(parts[1], out mode))
            {
                return false;
            }

            return true;
        }

        public static bool TryParseUserTarget(string commandId, out long userId, out long targetUserId)
        {
            userId = 0;
            targetUserId = 0;

            if (commandId == null || !commandId.StartsWith(UserTargetPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var payload = commandId.Substring(UserTargetPrefix.Length);
            var parts = payload.Split(new[] { UserTargetSeparator }, StringSplitOptions.None);
            if (parts.Length != 2 || !long.TryParse(parts[0], out userId) || !long.TryParse(parts[1], out targetUserId))
            {
                return false;
            }

            return true;
        }
    }
}