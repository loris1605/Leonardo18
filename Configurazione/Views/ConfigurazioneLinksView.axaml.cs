using Avalonia.Input;
using Configurazione.ViewModels;
using System.Windows.Input;
using Views;
namespace Configurazione.Views;
public partial class ConfigurazioneLinksView : BaseUserControl<ConfigurazioneLinksViewModel>
{
    public ConfigurazioneLinksView(){InitializeComponent();}
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if(e.Handled||ViewModel is not {} vm)return;
        ICommand? command=e.Key==Key.Escape?vm.CancelCommand:
            e.Key==Key.S&&e.KeyModifiers.HasFlag(KeyModifiers.Control)?vm.SaveCommand:null;
        if(command is not null){e.Handled=true;if(command.CanExecute(null))command.Execute(null);}
    }
}
