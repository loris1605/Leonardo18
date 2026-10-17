using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI.Primitives;
using Soci.ViewModels;
using System.Windows.Input;
using Views;
namespace Soci.Views;
public partial class SociView : BaseUserControl<SociViewModel>
{
    private bool? _compact;
    public SociView() { InitializeComponent(); SizeChanged += (_, _) => UpdateColumns(); }
    private void UpdateColumns()
    {
        bool compact = Bounds.Width < 1100;
        if(ViewModel is { } vm)vm.IsWideLayout=!compact;
        if (_compact == compact) return;
        _compact = compact;
        MainColumns.ColumnDefinitions = new ColumnDefinitions(compact ? "*" : "3*,2*");
        MainColumns.RowDefinitions = new RowDefinitions(compact ? "Auto,Auto" : "Auto");
        Grid.SetColumn(EditorPanel, compact ? 0 : 1);
        Grid.SetRow(EditorPanel, compact ? 1 : 0);
    }
    protected override IDisposable OnViewActivated(SociViewModel vm)
    {
        vm.IsWideLayout=Bounds.Width>=1100;
        return vm.FieldFocus.RegisterHandler(async interaction => {
            await Dispatcher.UIThread.InvokeAsync(() => {
                if(vm.IsBusy)return;
                var control=this.GetVisualDescendants().OfType<Control>().FirstOrDefault(x=>x.Name==interaction.Input);
                control?.BringIntoView(); control?.Focus(); if(control is TextBox text)text.SelectAll();
            },DispatcherPriority.Background);
            interaction.SetOutput(RxVoid.Default);
        });
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if(e.Handled||ViewModel is not { } vm)return;
        ICommand? command=e.Key==Key.Escape?vm.EscPressedCommand:
            e.Key==Key.S&&e.KeyModifiers.HasFlag(KeyModifiers.Control)?vm.SaveCommand:null;
        if(command is not null){e.Handled=true;if(command.CanExecute(null))command.Execute(null);}
    }
}