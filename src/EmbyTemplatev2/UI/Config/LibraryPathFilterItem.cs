using System.ComponentModel;

namespace EmbyTemplatev2.UI.Config
{
    public class LibraryPathFilterItem
    {
        [DisplayName("Library")]
        public string LibraryName { get; set; }

        [DisplayName("Path")]
        public string Path { get; set; }

        [DisplayName("Enabled")]
        public bool Enabled { get; set; } = false;
    }
}