using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Cassa.ViewModels;
using ReactiveUI.Primitives;
namespace Cassa.Views;
public partial class UscitaSocioView:UserControl
{
    public UscitaSocioView(){InitializeComponent();ExitPositionBox.AddHandler(InputElement.KeyDownEvent,Search,RoutingStrategies.Tunnel);ExitCardBox.AddHandler(InputElement.KeyDownEvent,Search,RoutingStrategies.Tunnel);}
    private void Search(object? sender,KeyEventArgs e)
    {if(e.Key==Key.Enter&&DataContext is CassaViewModel vm&&((System.Windows.Input.ICommand)vm.CercaUscitaCommand).CanExecute(null)){e.Handled=true;((System.Windows.Input.ICommand)vm.CercaUscitaCommand).Execute(null);}}
}
