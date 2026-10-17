using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Cassa.ViewModels;
using System.Windows.Input;
namespace Cassa.Views;
public partial class StoricoFidelityView:UserControl
{
    public StoricoFidelityView()
    {
        InitializeComponent();
        HistoryCardBox.AddHandler(InputElement.KeyDownEvent,(_,e)=>{
            if(e.Key!=Key.Enter)return;
            e.Handled=true;
            ICommand? command=(DataContext as CassaViewModel)?.CercaStoricoFidelityCommand;
            if(command?.CanExecute(null)==true)command.Execute(null);
        },RoutingStrategies.Tunnel);
    }
}
