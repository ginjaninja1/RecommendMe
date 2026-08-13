using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;

namespace RecommendMe.Services
{
    /// <summary>
    /// Authenticated API used by the web-client context-menu command. The
    /// caller is always derived from Emby's authorization context; client
    /// supplied sender identities are deliberately not accepted.
    /// </summary>
    [Authenticated]
    public class ContextMenuApiService : IService, IRequiresRequest
    {
        private readonly IAuthorizationContext authorizationContext;

        public ContextMenuApiService(IAuthorizationContext authorizationContext)
        {
            this.authorizationContext = authorizationContext;
        }

        public IRequest Request { get; set; }

        public async Task<object> Get(GetRecommendContextMenuSettings request)
        {
            var settings = await Plugin.Instance.AdminSettingsStore.GetAsync().ConfigureAwait(false);
            Plugin.Instance.Logger.Info(
                "RecommendMe context menu returned {0} allowed item type(s).",
                settings.GloballyAllowedMediaTypes.Count);
            return new RecommendContextMenuSettings
            {
                AllowedItemTypes = settings.GloballyAllowedMediaTypes
                    .Distinct(StringComparer.Ordinal)
                    .ToList()
            };
        }

        public async Task<object> Get(GetRecommendTargets request)
        {
            var sender = this.GetCaller();
            var item = Plugin.Instance.LibraryManager.GetItemById(request.ItemId);
            Plugin.Instance.Logger.Info(
                "RecommendMe context menu target request: sender={0} ({1}), item={2}.",
                sender.Name,
                sender.InternalId,
                request.ItemId);
            var unavailable = await ValidateItemAsync(sender, item).ConfigureAwait(false);
            if (unavailable != null)
            {
                return new RecommendTargetsResult { Allowed = false, Message = unavailable };
            }

            var result = new RecommendTargetsResult { Allowed = true };
            foreach (var candidate in Plugin.Instance.GetAllUsers().OrderBy(user => user.Name))
            {
                var permission = await Plugin.Instance.PermissionService
                    .CanSendAsync(sender, candidate, item.GetType().Name)
                    .ConfigureAwait(false);

                if (permission == SendPermissionResult.Allowed && item.IsVisible(candidate))
                {
                    result.Targets.Add(new RecommendTargetDto
                    {
                        Id = candidate.InternalId.ToString(CultureInfo.InvariantCulture),
                        Name = candidate.InternalId == sender.InternalId
                            ? candidate.Name + " (yourself)"
                            : candidate.Name
                    });
                }
            }

            if (result.Targets.Count == 0)
            {
                result.Message = "There are no users eligible to receive this item.";
            }

            Plugin.Instance.Logger.Info(
                "RecommendMe context menu returned {0} eligible target(s) for item {1}.",
                result.Targets.Count,
                request.ItemId);

            return result;
        }

        public async Task<object> Post(SendContextMenuRecommendation request)
        {
            var sender = this.GetCaller();
            var item = Plugin.Instance.LibraryManager.GetItemById(request.ItemId);
            Plugin.Instance.Logger.Info(
                "RecommendMe context menu send request: sender={0} ({1}), target={2}, item={3}.",
                sender.Name,
                sender.InternalId,
                request.TargetUserId,
                request.ItemId);
            var unavailable = await ValidateItemAsync(sender, item).ConfigureAwait(false);
            if (unavailable != null)
            {
                return Failure(unavailable);
            }

            var recipient = Plugin.Instance.GetAllUsers()
                .FirstOrDefault(user => user.InternalId == request.TargetUserId);
            if (recipient == null)
            {
                return Failure("That recipient is no longer available.");
            }

            // RecommendationService repeats all authorization, visibility,
            // watched-state and live collection checks. This endpoint never
            // relies on the target list previously returned to the browser.
            var sendResult = await Plugin.Instance.RecommendationService
                .SendRecommendationAsync(sender, recipient, item, item.GetType().Name, false)
                .ConfigureAwait(false);

            switch (sendResult)
            {
                case RecommendationSendResult.Success:
                    return Success("Recommended to " + recipient.Name + ".");
                case RecommendationSendResult.RecipientBlockedSender:
                    return Failure(recipient.Name + " is not accepting recommendations from you.");
                case RecommendationSendResult.RecipientOptedOutMediaType:
                    return Failure(recipient.Name + " is not accepting this type of recommendation from you.");
                case RecommendationSendResult.AlreadyWatchedByRecipient:
                    return Failure(
                        recipient.Name + " has already watched " + FormatItemTitle(item) + ".");
                case RecommendationSendResult.AlreadyInRecipientCollection:
                    return Failure(
                        recipient.Name + " already has " + FormatItemTitle(item) +
                        " in their recommendation collection.");
                case RecommendationSendResult.RecipientCannotAccessItem:
                    return Failure(recipient.Name + " does not have access to this item.");
                default:
                    return Failure("You do not have permission to recommend this item to that user.");
            }
        }

        private User GetCaller()
        {
            var authorization = this.authorizationContext.GetAuthorizationInfo(this.Request);
            var user = Plugin.Instance.GetAllUsers()
                .FirstOrDefault(candidate => candidate.InternalId == authorization.UserId);

            if (user == null)
            {
                throw new UnauthorizedAccessException("The authenticated Emby user could not be resolved.");
            }

            return user;
        }

        private static async Task<string> ValidateItemAsync(User sender, BaseItem item)
        {
            if (item == null || !item.IsVisible(sender))
            {
                return "That media item is unavailable.";
            }

            var settings = await Plugin.Instance.AdminSettingsStore.GetAsync().ConfigureAwait(false);
            if (!settings.GloballyAllowedMediaTypes.Contains(item.GetType().Name))
            {
                return "Recommendations are disabled for this media type.";
            }

            if (await Plugin.Instance.PermissionService.IsAccessSuspendedAsync(sender).ConfigureAwait(false))
            {
                return "Your RecommendMe access is suspended.";
            }

            return null;
        }

        private static ContextMenuRecommendationResult Success(string message) =>
            new ContextMenuRecommendationResult { Success = true, Message = message };

        private static ContextMenuRecommendationResult Failure(string message) =>
            new ContextMenuRecommendationResult { Success = false, Message = message };

        private static string FormatItemTitle(BaseItem item) =>
            item.ProductionYear.HasValue
                ? item.Name + " (" + item.ProductionYear.Value.ToString(CultureInfo.InvariantCulture) + ")"
                : item.Name;
    }
}
