using Avalonia.Controls;
namespace Soci.Views;
public partial class PersonInputView : UserControl
{
#if DEBUG
    ~PersonInputView() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public PersonInputView()
    {
        InitializeComponent();
        CognomeInput.TextChanged+=(_,_)=>Uppercase(CognomeInput);
        NomeInput.TextChanged+=(_,_)=>Uppercase(NomeInput);
    }
    private static void Uppercase(TextBox input)
    {
        var text=input.Text??"";
        var upper=text.ToUpperInvariant();
        if(text==upper)return;
        var start=input.SelectionStart;var end=input.SelectionEnd;var caret=input.CaretIndex;
        input.Text=upper;
        input.SelectionStart=start;input.SelectionEnd=end;input.CaretIndex=caret;
    }
}