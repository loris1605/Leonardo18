using Avalonia.Input;
using Avalonia.Threading;
using Configurazione.ViewModels;
using ReactiveUI.Primitives;
using System.Windows.Input;
using Views;
namespace Configurazione.Views;
public partial class ConfigurazioneInputView : BaseUserControl<ConfigurazioneInputViewModel>
{
    public ConfigurazioneInputView(){InitializeComponent();}
    protected override IDisposable OnViewActivated(ConfigurazioneInputViewModel vm)=>
        vm.NomeFocus.RegisterHandler(async interaction=>{
            await Dispatcher.UIThread.InvokeAsync(()=>{if(!vm.IsBusy){NomeText.Focus();NomeText.SelectAll();}},DispatcherPriority.Background);
            interaction.SetOutput(RxVoid.Default);
        });
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if(e.Handled||ViewModel is not {} vm)return;
        ICommand? command=e.Key==Key.Escape?vm.CancelCommand:
            e.Key==Key.S&&e.KeyModifiers.HasFlag(KeyModifiers.Control)?vm.SaveCommand:null;
        if(command is not null){e.Handled=true;if(command.CanExecute(null))command.Execute(null);}
    }
}
