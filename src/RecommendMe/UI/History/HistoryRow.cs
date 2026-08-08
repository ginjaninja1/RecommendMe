using System.ComponentModel;

namespace RecommendMe.UI.History
{
    /// <summary>One row in the recommendation history grid.</summary>
    public class HistoryRow
    {
        [Browsable(false)]
        public string RecommendationId { get; set; } = string.Empty;

        [DisplayName("Recommended To")]
        public string RecommendedTo { get; set; } = string.Empty;

        [DisplayName("Recommended By")]
        public string RecommendedBy { get; set; } = string.Empty;

        [DisplayName("Private")]
        public string Private { get; set; } = string.Empty;

        [DisplayName("Date Recommended")]
        public string DateRecommended { get; set; } = string.Empty;

        [DisplayName("Media Type")]
        public string MediaType { get; set; } = string.Empty;

        [DisplayName("Name")]
        public string Name { get; set; } = string.Empty;
    }
}
