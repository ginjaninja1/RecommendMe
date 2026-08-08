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
        public override string EditorTitle => "Receive Policy";

        public override string EditorDescription =>
            "Choose which permitted users and media types you want to receive recommendations from.";

        public CaptionItem SendersHeading { get; set; } = new CaptionItem("Senders You Can Receive From");

        public GenericItemList SenderList { get; set; } = new GenericItemList();
    }
}
