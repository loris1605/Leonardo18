using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Cassa.ViewModels;
namespace Cassa.Views;

public partial class AcquistoAbbonamentoView:UserControl
{
    public AcquistoAbbonamentoView()
    {
        InitializeComponent();
        FirstCardBox.AddHandler(InputElement.KeyDownEvent,(_,e)=>Read(e,"1"),RoutingStrategies.Tunnel);
        SecondCardBox.AddHandler(InputElement.KeyDownEvent,(_,e)=>Read(e,"2"),RoutingStrategies.Tunnel);
    }
    private void Read(KeyEventArgs e,string slot)
    {
        if(e.Key==Key.Enter && DataContext is CassaViewModel vm && ((System.Windows.Input.ICommand)vm.IdentificaTesseraAbbonamentoCommand).CanExecute(slot))
        {e.Handled=true;((System.Windows.Input.ICommand)vm.IdentificaTesseraAbbonamentoCommand).Execute(slot);}
    }
}