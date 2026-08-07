using System;

namespace RecommendMe.UI.Recommend
{
    /// <summary>
    /// Command id constants for the Recommend page, plus the matching
    /// build/parse logic for the one parameterised command (Send, which
    /// encodes the target item's internal id).
    /// </summary>
    internal static class RecommendCommands
    {
        public const string Search = "search";

        public const string UpdateFormState = "updateformstate";

        public const string OpenHistory = "openhistory";

        private const string SendPrefix = "send:";
        private const string ExpandPrefix = "expand:";

        public static string BuildSendCommandId(long itemId) => $"{SendPrefix}{itemId}";
        public static string BuildExpandCommandId(long itemId) => $"{ExpandPrefix}{itemId}";

        public static bool TryParseSend(string commandId, out long itemId)
        {
            itemId = 0;

            if (commandId == null || !commandId.StartsWith(SendPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return long.TryParse(commandId.Substring(SendPrefix.Length), out itemId);
        }

        public static bool TryParseExpand(string commandId, out long itemId)
        {
            itemId = 0;
            return commandId != null
                && commandId.StartsWith(ExpandPrefix, StringComparison.OrdinalIgnoreCase)
                && long.TryParse(commandId.Substring(ExpandPrefix.Length), out itemId);
        }
    }
}
