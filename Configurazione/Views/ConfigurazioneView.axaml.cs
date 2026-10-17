using Avalonia.Controls;
using Avalonia.Input;
using Configurazione.ViewModels;
using System.Windows.Input;
using Views;
namespace Configurazione.Views;
public partial class ConfigurazioneView : BaseUserControl<ConfigurazioneViewModel>
{
    public ConfigurazioneView(){InitializeComponent();SizeChanged+=(_,_)=>UpdateColumns();}
    private void UpdateColumns()
    {
        var compact=Bounds.Width<1100;
        if(ViewModel is {} vm)vm.IsWideLayout=!compact;
        MainColumns.ColumnDefinitions=new(compact?"*":"3*,2*");
        MainColumns.RowDefinitions=new(compact?"Auto,Auto":"Auto");
        Grid.SetColumn(InputHost,compact?0:1);Grid.SetRow(InputHost,compact?1:0);
    }
    protected override IDisposable? OnViewActivated(ConfigurazioneViewModel vm){UpdateColumns();return base.OnViewActivated(vm);}
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if(e.Handled||ViewModel is not {} vm)return;
        if(e.Key==Key.Escape)
        {
            e.Handled=true;ICommand command=vm.EscPressedCommand;
            if(command.CanExecute(null))command.Execute(null);
        }
    }
}

