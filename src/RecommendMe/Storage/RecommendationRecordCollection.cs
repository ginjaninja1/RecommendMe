using System.Collections.Generic;
using RecommendMe.Models;

namespace RecommendMe.Storage
{
    internal sealed class RecommendationRecordCollection
    {
        public RecommendationRecordCollection()
        {
        }

        public List<RecommendationRecord> Records { get; set; } = new List<RecommendationRecord>();
    }
}
