using Avalonia.Controls;

namespace ControlStyles
{
    public class UiComboBox : ComboBox
    {
#if DEBUG
    ~UiComboBox() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif


    }
}
