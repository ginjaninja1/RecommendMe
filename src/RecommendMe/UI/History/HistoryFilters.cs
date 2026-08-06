using System.Collections.Generic;

namespace RecommendMe.UI.History
{
    internal static class HistoryFilters
    {
        public const string Last1Month = "Last Month";
        public const string Last3Months = "Last 3 Months";
        public const string Last6Months = "Last 6 Months";
        public const string AllTime = "All Time";

        public static readonly IReadOnlyList<string> AllDateRanges = new[]
        {
            Last1Month, Last3Months, Last6Months, AllTime
        };

        public const string CurrentUser = "Me";
        public const string AnyoneVisibleToMe = "Anyone Visible To Me";

        public static readonly IReadOnlyList<string> AllRecipientFilters = new[]
        {
            CurrentUser, AnyoneVisibleToMe
        };

        public const string Anyone = "Anyone";
    }

    internal static class HistoryCommands
    {
        public const string Refresh = "refreshhistory";
    }
}
