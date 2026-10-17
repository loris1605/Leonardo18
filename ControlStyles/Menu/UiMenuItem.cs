using Avalonia.Controls;

namespace ControlStyles
{
    public class UiMenuItem : MenuItem
    {
#if DEBUG
    ~UiMenuItem() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        protected override Type StyleKeyOverride => typeof(MenuItem);

        public UiMenuItem()
        {
            this.Classes.Add("UiMenuItemStyle");
        }
    }
}
