using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;

namespace RecommendMe.UI.Admin
{
    /// <summary>
    /// View-model for the admin permissions page. Always freshly built from
    /// <see cref="Models.AdminSettings"/> by <see cref="AdminViewBuilder"/> -
    /// never persisted directly (mirrors the ConfigUI/PluginConfiguration
    /// split already established in this codebase).
    /// </summary>
    public class AdminSettingsUI : EditableOptionsBase
    {
        public override string EditorTitle => "RecommendMe - Admin Settings";

        public override string EditorDescription => "Control who can recommend to whom.";

        public CaptionItem MediaTypeHeading { get; set; } = new CaptionItem("Recommendable Media Types (server-wide)");

        public GenericItemList MediaTypeList { get; set; } = new GenericItemList();

        public CaptionItem NewUserHeading { get; set; } = new CaptionItem("New User Defaults");

        public GenericItemList NewUserDefaultsList { get; set; } = new GenericItemList();

        public CaptionItem UserMatrixHeading { get; set; } =
            new CaptionItem("Per-User Access (includes Emergency Revocation)");

        public GenericItemList UserAccessList { get; set; } = new GenericItemList();
    }
}