using System;

namespace EmbyTemplatev2.UI.Config
{
    /// <summary>
    /// Command id constants for the config page's library/path toggle
    /// controls, plus the matching build/parse logic. Kept together so the
    /// two never drift apart.
    /// </summary>
    internal static class LibraryFilterCommands
    {
        public const string PageSave = "PageSave";

        private const string LibraryTogglePrefix = "togglelib:";
        private const string PathTogglePrefix = "togglepath:";
        private const string PathSeparator = "|||";

        public static string BuildLibraryToggleCommandId(string libraryName)
        {
            return $"{LibraryTogglePrefix}{libraryName}";
        }

        public static string BuildPathToggleCommandId(string libraryName, string path)
        {
            return $"{PathTogglePrefix}{libraryName}{PathSeparator}{path}";
        }

        public static bool TryParseLibraryToggle(string commandId, out string libraryName)
        {
            libraryName = null;

            if (commandId == null ||
                !commandId.StartsWith(LibraryTogglePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            libraryName = commandId.Substring(LibraryTogglePrefix.Length);
            return true;
        }

        public static bool TryParsePathToggle(string commandId, out string libraryName, out string path)
        {
            libraryName = null;
            path = null;

            if (commandId == null ||
                !commandId.StartsWith(PathTogglePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var payload = commandId.Substring(PathTogglePrefix.Length);
            var parts = payload.Split(new[] { PathSeparator }, StringSplitOptions.None);

            if (parts.Length != 2)
            {
                return false;
            }

            libraryName = parts[0];
            path = parts[1];
            return true;
        }
    }
}