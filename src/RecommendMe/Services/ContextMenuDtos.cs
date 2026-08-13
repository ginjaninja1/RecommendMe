using System.Collections.Generic;
using MediaBrowser.Model.Services;

namespace RecommendMe.Services
{
    [Route("/RecommendMe/ContextMenu/Settings", "GET")]
    public class GetRecommendContextMenuSettings
    {
    }

    [Route("/RecommendMe/RecommendTargets/{ItemId}", "GET")]
    public class GetRecommendTargets
    {
        public long ItemId { get; set; }
    }

    [Route("/RecommendMe/Recommend", "POST")]
    public class SendContextMenuRecommendation
    {
        public long ItemId { get; set; }

        public long TargetUserId { get; set; }
    }

    public class RecommendContextMenuSettings
    {
        public List<string> AllowedItemTypes { get; set; } = new List<string>();
    }

    public class RecommendTargetDto
    {
        public string Id { get; set; }

        public string Name { get; set; }
    }

    public class RecommendTargetsResult
    {
        public bool Allowed { get; set; }

        public string Message { get; set; }

        public List<RecommendTargetDto> Targets { get; set; } = new List<RecommendTargetDto>();
    }

    public class ContextMenuRecommendationResult
    {
        public bool Success { get; set; }

        public string Message { get; set; }
    }

    [Route("/RecommendMe/WatchedStatus/{ItemId}", "GET")]
    public class GetWatchedStatus
    {
        public long ItemId { get; set; }
    }

    public class WatchedStatusResult
    {
        public bool Allowed { get; set; }

        public string Message { get; set; }

        public string ItemName { get; set; }

        public List<WatchedStatusUserDto> Users { get; set; } = new List<WatchedStatusUserDto>();
    }

    public class WatchedStatusUserDto
    {
        public string UserName { get; set; }

        public bool Watched { get; set; }

        public System.DateTimeOffset? LastPlayed { get; set; }

        public long ResumePositionTicks { get; set; }
    }
}
