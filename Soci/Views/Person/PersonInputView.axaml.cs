using Avalonia.Controls;
namespace Soci.Views;
public partial class PersonInputView : UserControl
{
#if DEBUG
    ~PersonInputView() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public PersonInputView() { InitializeComponent(); }
}