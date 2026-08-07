using System;

namespace RecommendMe.UI.Admin
{
    internal static class AdminCommands
    {
        public const string Refresh = "admin-refresh";
        public const string ToggleExpansion = "admin-toggle-expansion";
        public const string SendToRefresh = "send-to-refresh";
        public const string ReceiveFromRefresh = "receive-from-refresh";
        public const string GroupMembershipRefresh = "group-membership-refresh";
        public const string DefaultPolicyRefresh = "default-policy-refresh";
        public const string ToggleAllowNewUsers = "send-to-toggle-new-users";

        private const string SuspendedPrefix = "admin-suspended:";
        private const string MediaTypePrefix = "mediatype:";
        private const string SendToPrefix = "admin-send-to:";
        private const string ReceiveFromPrefix = "admin-receive-from:";
        private const string MembershipPrefix = "admin-membership:";
        private const string TargetPrefix = "admin-target:";
        private const string ReceiveSenderPrefix = "admin-receive-sender:";
        private const string ReceiveMediaPrefix = "admin-receive-media:";
        private const string GroupPrefix = "admin-user-group:";
        private const string SelectDefaultPrefix = "admin-select-default:";
        private const string Separator = "|||";

        public static string Suspended(long id) => SuspendedPrefix + id;
        public static string BuildMediaTypeToggle(string mediaType) => MediaTypePrefix + mediaType;
        public static string SendTo(long id) => SendToPrefix + id;
        public static string ReceiveFrom(long id) => ReceiveFromPrefix + id;
        public static string Membership(long id) => MembershipPrefix + id;
        public static string Target(long ownerId, long targetId) => TargetPrefix + ownerId + Separator + targetId;
        public static string ReceiveSender(long ownerId, long senderId) => ReceiveSenderPrefix + ownerId + Separator + senderId;
        public static string ReceiveMedia(long ownerId, long senderId, string mediaType) => ReceiveMediaPrefix + ownerId + Separator + senderId + Separator + mediaType;
        public static string Group(long userId, string groupId) => GroupPrefix + userId + Separator + groupId;
        public static string SelectDefault(long id) => SelectDefaultPrefix + id;

        public static bool TrySuspended(string command, out long id) => TryLong(command, SuspendedPrefix, out id);
        public static bool TryParseMediaType(string command, out string mediaType) => TryPayload(command, MediaTypePrefix, out mediaType);
        public static bool TrySendTo(string command, out long id) => TryLong(command, SendToPrefix, out id);
        public static bool TryReceiveFrom(string command, out long id) => TryLong(command, ReceiveFromPrefix, out id);
        public static bool TryMembership(string command, out long id) => TryLong(command, MembershipPrefix, out id);
        public static bool TrySelectDefault(string command, out long id) => TryLong(command, SelectDefaultPrefix, out id);
        public static bool TryTarget(string command, out long ownerId, out long targetId) => TryLongPair(command, TargetPrefix, out ownerId, out targetId);
        public static bool TryReceiveSender(string command, out long ownerId, out long senderId) => TryLongPair(command, ReceiveSenderPrefix, out ownerId, out senderId);

        public static bool TryReceiveMedia(string command, out long ownerId, out long senderId, out string mediaType)
        {
            ownerId = 0; senderId = 0; mediaType = null;
            if (!TryPayload(command, ReceiveMediaPrefix, out var value)) return false;
            var parts = value.Split(new[] { Separator }, StringSplitOptions.None);
            return parts.Length == 3 && long.TryParse(parts[0], out ownerId) && long.TryParse(parts[1], out senderId) && !string.IsNullOrEmpty(mediaType = parts[2]);
        }

        public static bool TryGroup(string command, out long userId, out string groupId)
        {
            userId = 0; groupId = null;
            if (!TryPayload(command, GroupPrefix, out var value)) return false;
            var parts = value.Split(new[] { Separator }, StringSplitOptions.None);
            return parts.Length == 2 && long.TryParse(parts[0], out userId) && !string.IsNullOrEmpty(groupId = parts[1]);
        }

        private static bool TryLongPair(string command, string prefix, out long first, out long second)
        {
            first = 0; second = 0;
            if (!TryPayload(command, prefix, out var value)) return false;
            var parts = value.Split(new[] { Separator }, StringSplitOptions.None);
            return parts.Length == 2 && long.TryParse(parts[0], out first) && long.TryParse(parts[1], out second);
        }

        private static bool TryLong(string command, string prefix, out long id)
        {
            id = 0;
            return TryPayload(command, prefix, out var value) && long.TryParse(value, out id);
        }

        private static bool TryPayload(string command, string prefix, out string value)
        {
            value = null;
            if (command == null || !command.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
            value = command.Substring(prefix.Length);
            return !string.IsNullOrEmpty(value);
        }
    }
}
