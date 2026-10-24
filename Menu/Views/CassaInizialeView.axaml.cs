using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ControlStyles;
using Menu.ViewModels;
namespace Menu.Views;
public partial class CassaInizialeView:UserControl
{
    public CassaInizialeView(){InitializeComponent();}
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        FocusTwentyEuro();
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if(change.Property==IsVisibleProperty && IsVisible)FocusTwentyEuro();
    }
    private void FocusTwentyEuro()
    {
        Dispatcher.UIThread.Post(()=>
        {
            if(!IsEffectivelyVisible || TopLevel.GetTopLevel(this) is null
                || DataContext is not MenuViewModel{IsOpeningCash:true,IsOpeningReady:true})return;
            var input=this.GetVisualDescendants().OfType<UiIntegerTextBox>()
                .FirstOrDefault(x=>x.DataContext is TaglioCassaViewModel{Taglio:20m});
            if(input?.Focus()==true)input.SelectAll();
        },DispatcherPriority.Loaded);
    }
}
