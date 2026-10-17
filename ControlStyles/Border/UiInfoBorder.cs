using Avalonia.Controls;

namespace ControlStyles
{
    public partial class UiInfoBorder : Border
    {
#if DEBUG
    ~UiInfoBorder() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        //protected override Type StyleKeyOverride => typeof(Border);
    }
}
