using Avalonia.Controls;

namespace ControlStyles
{
    public class UiMenu : Menu
    {
#if DEBUG
    ~UiMenu() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        protected override Type StyleKeyOverride => typeof(Menu);

        public UiMenu()
        {
            this.Classes.Add("UiMenuStyle");
        }

    }
}
