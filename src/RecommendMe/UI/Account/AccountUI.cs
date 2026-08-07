using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;

namespace RecommendMe.UI.Account
{
    /// <summary>
    /// View-model for the Account tab: lists every sender this user is
    /// currently permitted (by the admin matrix) to receive from, with a
    /// per-media-type opt-out toggle for each.
    /// </summary>
    public class AccountUI : EditableOptionsBase
    {
        public override string EditorTitle => "Account";

        public override string EditorDescription =>
            "Choose which of your permitted senders (and which media types) you want to receive recommendations from.";

        public CaptionItem SendersHeading { get; set; } = new CaptionItem("Senders You Can Receive From");

        /// <summary>
        /// The sender list now loads automatically as soon as the page
        /// knows who's viewing it (see AccountPageView's User override).
        /// This button stays as a manual re-sync - e.g. if another user
        /// changes their SendMode/media-type list while this tab is open.
        /// </summary>
        public GenericItemList LoadButton { get; set; } = new GenericItemList
        {
            new GenericListItem
            {
                PrimaryText = "Refresh",
                Icon = IconNames.refresh,
                Status = ItemStatus.Succeeded,
                Button1 = new ButtonItem("Refresh") { CommandId = "load" }
            }
        };

        public GenericItemList SenderList { get; set; } = new GenericItemList();
    }
}