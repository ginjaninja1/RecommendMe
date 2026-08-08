using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;

namespace RecommendMe.UI
{
    /// <summary>
    /// Read-only content shown in place of every user-facing RecommendMe page
    /// while the current user's plugin access is suspended.
    /// </summary>
    public class SuspendedUI : EditableOptionsBase
    {
        public override string EditorTitle => "Access suspended";

        public override string EditorDescription =>
            "Your account is suspended, actions are disabled.";

        public GenericItemList SuspensionNotice { get; set; } = new GenericItemList
        {
            new GenericListItem
            {
                PrimaryText = "Your account is suspended, actions are disabled.",
                SecondaryText = "An administrator has suspended your access to RecommendMe.",
                Icon = IconNames.error,
                Status = ItemStatus.Failed
            }
        };
    }
}
