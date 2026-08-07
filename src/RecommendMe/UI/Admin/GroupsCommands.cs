using System;

namespace RecommendMe.UI.Admin
{
    internal static class GroupsCommands
    {
        public const string Refresh = "groups-refresh";
        public const string Create = "groups-create";
        public const string Rename = "groups-rename";
        public const string Delete = "groups-delete";
        private const string SelectPrefix = "group-select:";
        private const string TogglePrefix = "group-toggle-user:";
        private const string UserPrefix = "group-user-lookup:";
        private const string Separator = "|||";

        public static string Select(string groupId) => SelectPrefix + groupId;
        public static string ToggleUser(string groupId, long userId) => TogglePrefix + groupId + Separator + userId;
        public static string UserLookup(long userId) => UserPrefix + userId;
        public static bool TrySelect(string command, out string id) => TryString(command, SelectPrefix, out id);
        public static bool TryUserLookup(string command, out long id)
        {
            id = 0;
            return TryString(command, UserPrefix, out var value) && long.TryParse(value, out id);
        }
        public static bool TryToggleUser(string command, out string groupId, out long userId)
        {
            groupId = null; userId = 0;
            if (!TryString(command, TogglePrefix, out var value)) return false;
            var parts = value.Split(new[] { Separator }, StringSplitOptions.None);
            if (parts.Length != 2 || !long.TryParse(parts[1], out userId)) return false;
            groupId = parts[0]; return true;
        }
        private static bool TryString(string command, string prefix, out string value)
        {
            value = null;
            if (command == null || !command.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
            value = command.Substring(prefix.Length); return !string.IsNullOrEmpty(value);
        }
    }
}
