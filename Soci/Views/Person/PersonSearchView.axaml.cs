using Avalonia.Controls;
namespace Soci.Views;
public partial class PersonSearchView : UserControl
{
#if DEBUG
    ~PersonSearchView() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public PersonSearchView() { InitializeComponent(); }
}