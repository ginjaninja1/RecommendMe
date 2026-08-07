using System;

namespace RecommendMe.UI.Account
{
    internal static class AccountCommands
    {
        private const string Prefix = "optout:";
        private const string BlockPrefix = "block:";
        private const string Separator = "|||";

        public static string BuildOptOutToggle(long senderUserId, string mediaType) =>
            $"{Prefix}{senderUserId}{Separator}{mediaType}";

        public static string BuildBlockToggle(long senderUserId) => $"{BlockPrefix}{senderUserId}";

        public static bool TryParseBlock(string commandId, out long senderUserId)
        {
            senderUserId = 0;

            if (commandId == null || !commandId.StartsWith(BlockPrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return long.TryParse(commandId.Substring(BlockPrefix.Length), out senderUserId);
        }

        public static bool TryParse(string commandId, out long senderUserId, out string mediaType)
        {
            senderUserId = 0;
            mediaType = null;

            if (commandId == null || !commandId.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var payload = commandId.Substring(Prefix.Length);
            var parts = payload.Split(new[] { Separator }, StringSplitOptions.None);
            if (parts.Length != 2 || !long.TryParse(parts[0], out senderUserId))
            {
                return false;
            }

            mediaType = parts[1];
            return true;
        }
    }
}