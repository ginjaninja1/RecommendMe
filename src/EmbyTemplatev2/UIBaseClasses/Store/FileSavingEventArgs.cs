using Emby.Web.GenericEdit;
using System;

namespace EmbyTemplatev2.UIBaseClasses.Store
{
    public class FileSavingEventArgs : EventArgs
    {
        public FileSavingEventArgs(EditableOptionsBase options)
        {
            this.Options = options;
        }

        public EditableOptionsBase Options { get; }

        public bool Cancel { get; set; }
    }
}
