using Avalonia.Controls;

namespace ControlStyles;

public class UiDatePicker : DatePicker
{
#if DEBUG
    ~UiDatePicker() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    // Riutilizza il template standard di Avalonia per DatePicker.
    protected override Type StyleKeyOverride => typeof(DatePicker);

    public UiDatePicker()
    {
        Classes.Add("UiDatePickerStyle");
    }
}
