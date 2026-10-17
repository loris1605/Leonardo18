using Avalonia.Controls;

namespace ControlStyles
{
    public class PopupBorder : Border
    {
#if DEBUG
    ~PopupBorder() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif


    }
}
