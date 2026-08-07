using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;

namespace RecommendMe.UI.Admin
{
    public class MediaUI : EditableOptionsBase
    {
        public override string EditorTitle => "Media";
        public override string EditorDescription => "Choose which media types can be recommended server-wide.";
        public CaptionItem Heading { get; set; } = new CaptionItem("Recommendable Media Types");
        public GenericItemList MediaTypeList { get; set; } = new GenericItemList();
    }
}
