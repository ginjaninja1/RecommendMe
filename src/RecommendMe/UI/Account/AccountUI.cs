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
        /// Present because this.User (the browsing user) is not available
        /// until after this view-model's first RunCommand call - see
        /// AccountPageView. Tapping this triggers the initial load.
        /// </summary>
        public GenericItemList LoadButton { get; set; } = new GenericItemList
        {
            new GenericListItem
            {
                PrimaryText = "Load My Senders",
                Icon = IconNames.refresh,
                Status = ItemStatus.Succeeded,
                Button1 = new ButtonItem("Load") { CommandId = "load" }
            }
        };

        public GenericItemList SenderList { get; set; } = new GenericItemList();
    }
}
