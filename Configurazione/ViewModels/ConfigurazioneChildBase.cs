using AppServices.Database;
using Configurazione.Core.Repository;
using ReactiveUI;
using ReactiveUI.Primitives;
using ViewModels;
namespace Configurazione.ViewModels;

public abstract class ConfigurazioneChildBase(IScreen host,IConfigurazioneRepository repository) : ViewModelBase(host)
{
    protected IConfigurazioneRepository Repository { get; }=repository;
    private string _info="";
    private bool _busy;
    public bool IsBusy=>_busy;
    public string InfoLabel { get=>_info; protected set=>this.RaiseAndSetIfChanged(ref _info,value); }
    protected virtual void RefreshCommands() { }
    protected async Task<bool> RunOperation(Func<CancellationToken,Task> operation,string message)
    {
        if(_busy||_isClosing)return false;
        var ct=Token;
        _busy=true;IsWaiting=true;WaitMessage=message;InfoLabel="";
        this.RaisePropertyChanged(nameof(IsBusy));RefreshCommands();
        try { await operation(ct);ct.ThrowIfCancellationRequested();return true; }
        catch(OperationCanceledException) { return false; }
        catch(DatabaseHostException ex) { if(!_isClosing)InfoLabel=ex.Message;return false; }
        catch(InvalidOperationException ex) { if(!_isClosing)InfoLabel=ex.Message;return false; }
        catch(Exception) { if(!_isClosing)InfoLabel="Operazione non riuscita. Verifica la connessione.";return false; }
        finally {
            if(!_isClosing) {
                _busy=false;IsWaiting=false;WaitMessage="";
                this.RaisePropertyChanged(nameof(IsBusy));RefreshCommands();
            }
        }
    }
}
public static class ConfigurazioneTitles
{
    public static string Singular(ConfigurazioneSection s)=>s switch {
        ConfigurazioneSection.Operatori=>"OPERATORE",ConfigurazioneSection.Postazioni=>"POSTAZIONE",
        ConfigurazioneSection.Settori=>"SETTORE",ConfigurazioneSection.Tariffe=>"TARIFFA",_=>"TIPO DI RIENTRO" };
    public static string Plural(ConfigurazioneSection s)=>s switch {
        ConfigurazioneSection.Operatori=>"OPERATORI",ConfigurazioneSection.Postazioni=>"POSTAZIONI",
        ConfigurazioneSection.Settori=>"SETTORI",ConfigurazioneSection.Tariffe=>"TARIFFE",_=>"TIPI DI RIENTRO" };
    public static string Links(ConfigurazioneSection s)=>s switch {
        ConfigurazioneSection.Operatori=>"PERMESSI",ConfigurazioneSection.Postazioni=>"REPARTI",_=>"LISTINO" };
}
