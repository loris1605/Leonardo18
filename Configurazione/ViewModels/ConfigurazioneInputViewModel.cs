using System.Collections.ObjectModel;
using System.Globalization;
using AppServices.Database;
using Configurazione.Contracts;
using Configurazione.Core.Repository;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
namespace Configurazione.ViewModels;

public abstract class ConfigurazioneEditorBase : ConfigurazioneChildBase,IConfigurazioneInputViewModel
{
    private readonly Signal<int> _closed=new();
    private StateSignal<bool>? _canSave;
    private bool _loaded;
    protected StateSignal<bool> SaveReady=>_canSave??=new(false);
    protected override IObservable<bool> CanSave=>SaveReady;
    public IObservable<int> Closed=>_closed;
    public ReactiveCommand<RxVoid,RxVoid> CancelCommand { get; }
    private readonly IDisposable _cancelErrors;
    protected ConfigurazioneEditorBase(IScreen host,IConfigurazioneRepository repository):base(host,repository)
    {
        CancelCommand=ReactiveCommand.Create(()=>Close(0));
        _cancelErrors=CancelCommand.ThrownExceptions.SubscribePrimitives(_=>InfoLabel="Impossibile chiudere il form.");
    }
    protected void Loaded() {_loaded=true;RefreshCommands();}
    protected override void RefreshCommands()=>SaveReady.Value=_loaded&&!IsBusy&&!_isClosing;
    public void Cancel()=>Close(0);
    protected void Close(int id)
    {
        if(IsBusy||_isClosing)return;
        _isClosing=true;RefreshCommands();_closed.OnNext(id);
    }
    protected override Task OnEsc() {Close(0);return Task.CompletedTask;}
    protected override void OnFinalDestruction()
    {
        _isClosing=true;_closed.Dispose();_canSave?.Dispose();_cancelErrors.Dispose();CancelCommand.Dispose();
        base.OnFinalDestruction();
    }
}
public sealed class ConfigurazioneInputViewModel : ConfigurazioneEditorBase
{
    private ConfigurazioneRecord _record=new();
    private readonly int _id;
    private ConfigurazioneOption? _tipo,_rientro;
    private string _nome="",_etichetta="",_password="",_badge="0",_prezzo="0",_durata="0";
    private bool _abilitato=true,_freeDrink;
    public ConfigurazioneSection Section { get; }
    public ConfigurazioneAction Action { get; }
    public string Title=>(Action==ConfigurazioneAction.Add?"AGGIUNGI ":Action==ConfigurazioneAction.Update?"MODIFICA ":"ELIMINA ")+ConfigurazioneTitles.Singular(Section);
    public FluentIcons.Common.Symbol SaveIcon => Action==ConfigurazioneAction.Delete ? FluentIcons.Common.Symbol.Delete : FluentIcons.Common.Symbol.Save;
    public string SaveText=>Action==ConfigurazioneAction.Delete?"CONFERMA ELIMINAZIONE":"SALVA";
    public bool FieldsEnabled=>Action!=ConfigurazioneAction.Delete;
    public bool IsOperatore=>Section==ConfigurazioneSection.Operatori;
    public bool HasPassword=>IsOperatore&&FieldsEnabled;
    public string PasswordHint=>Action==ConfigurazioneAction.Update?"Lascia vuota per mantenere la password attuale":"Inserisci la password";
    public bool HasEtichetta=>Section is ConfigurazioneSection.Settori or ConfigurazioneSection.Tariffe;
    public bool HasTipo=>Section is ConfigurazioneSection.Settori or ConfigurazioneSection.Postazioni;
    public bool IsPostazione=>Section==ConfigurazioneSection.Postazioni;
    public bool IsTariffa=>Section==ConfigurazioneSection.Tariffe;
    public bool IsRientro=>Section==ConfigurazioneSection.Rientri;
    public int NameMaxLength=>IsOperatore?50:25;
    public string Nome { get=>_nome;set=>this.RaiseAndSetIfChanged(ref _nome,value); }
    public string Etichetta { get=>_etichetta;set=>this.RaiseAndSetIfChanged(ref _etichetta,value); }
    public string Password { get=>_password;set=>this.RaiseAndSetIfChanged(ref _password,value); }
    public string BadgeText { get=>_badge;set=>this.RaiseAndSetIfChanged(ref _badge,value); }
    public string PrezzoText { get=>_prezzo;set=>this.RaiseAndSetIfChanged(ref _prezzo,value); }
    public string DurataText { get=>_durata;set=>this.RaiseAndSetIfChanged(ref _durata,value); }
    public bool Abilitato { get=>_abilitato;set=>this.RaiseAndSetIfChanged(ref _abilitato,value); }
    public bool FreeDrink { get=>_freeDrink;set=>this.RaiseAndSetIfChanged(ref _freeDrink,value); }
    public ConfigurazioneOption? SelectedTipo { get=>_tipo;set=>this.RaiseAndSetIfChanged(ref _tipo,value); }
    public ConfigurazioneOption? SelectedRientro { get=>_rientro;set=>this.RaiseAndSetIfChanged(ref _rientro,value); }
    public ObservableCollection<ConfigurazioneOption> Tipi { get; }=[];
    public ObservableCollection<ConfigurazioneOption> Rientri { get; }=[];
    public Interaction<RxVoid,RxVoid> NomeFocus { get; }=new();
    public ConfigurazioneInputViewModel(IScreen host,IConfigurazioneRepository repository,ConfigurazioneSection section,ConfigurazioneAction action,int id):base(host,repository)
    {Section=section;Action=action;_id=id;}
    protected override async Task OnLoading()
    {
        var ok=await RunOperation(async ct=>{
            var result=await Repository.Execute(new(){Section=Section,Action=ConfigurazioneAction.Read,RecordId=_id},ct);
            _record=result.Records.Single();
            Nome=_record.Nome;Etichetta=_record.Etichetta;Abilitato=_record.Abilitato;BadgeText=_record.Badge.ToString();
            PrezzoText=_record.Prezzo.ToString("0.00",CultureInfo.CurrentCulture);DurataText=_record.DurataOre.ToString();FreeDrink=_record.FreeDrink;
            Tipi.Clear();foreach(var option in result.Tipi)Tipi.Add(option);
            Rientri.Clear();foreach(var option in result.Rientri)Rientri.Add(option);
            SelectedTipo=Tipi.FirstOrDefault(x=>x.Id==_record.TipoId)??(_id==0?Tipi.FirstOrDefault():null);
            SelectedRientro=Rientri.FirstOrDefault(x=>x.Id==_record.RientroId)??(_id==0?Rientri.FirstOrDefault():null);
            Loaded();
        },"CARICAMENTO "+ConfigurazioneTitles.Singular(Section)+" IN CORSO...");
        if(ok&&FieldsEnabled&&!_isClosing)await SetFocus(NomeFocus);
    }
    protected override async Task OnSaving()
    {
        int badge=0,durata=0;decimal prezzo=0;
        if(Action!=ConfigurazioneAction.Delete)
        {
            if(IsOperatore&&(!int.TryParse(BadgeText,out badge)||badge<0)){InfoLabel="Badge non valido.";return;}
            if(IsRientro&&(!int.TryParse(DurataText,out durata)||durata<0)){InfoLabel="Durata non valida.";return;}
            if(IsTariffa&&!decimal.TryParse(PrezzoText,NumberStyles.Number,CultureInfo.CurrentCulture,out prezzo)){InfoLabel="Prezzo non valido.";return;}
        }
        int saved=0;
        var ok=await RunOperation(async ct=>{
            var data=new ConfigurazioneRecord{Id=_record.Id,Nome=Nome,Etichetta=Etichetta,Password=Password,
                Abilitato=Abilitato,Badge=badge,TipoId=SelectedTipo?.Id??0,RientroId=SelectedRientro?.Id??0,
                Prezzo=prezzo,FreeDrink=FreeDrink,DurataOre=durata};
            var request=new ConfigurazioneRequest{Section=Section,Action=Action,RecordId=_id,Record=data};
            try{saved=(await Repository.Execute(request,ct)).RecordId;}
            finally {data.Password="";}
        },Action==ConfigurazioneAction.Delete?"ELIMINAZIONE IN CORSO...":"SALVATAGGIO IN CORSO...");
        if(ok) {Password="";Close(saved==0?-1:saved);}
    }
    protected override void OnFinalDestruction()
    {
        Password="";_record.Password="";Tipi.Clear();Rientri.Clear();
        base.OnFinalDestruction();
    }
}
public sealed class ConfigurazioneChoice : ReactiveObject
{
#if DEBUG
    ~ConfigurazioneChoice() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif
    public int Id { get; }
    public string Nome { get; }
    private bool _selected;
    public bool Selezionato {get=>_selected;set=>this.RaiseAndSetIfChanged(ref _selected,value);}
    public ConfigurazioneChoice(ConfigurazioneOption option){Id=option.Id;Nome=option.Nome;_selected=option.Selezionato;}
}
public sealed class ConfigurazioneLinksViewModel : ConfigurazioneEditorBase
{
    private readonly int _id;
    public ConfigurazioneSection Section { get; }
    public string Title { get; }
    public ObservableCollection<ConfigurazioneChoice> Scelte { get; }=[];
    public ConfigurazioneLinksViewModel(IScreen host,IConfigurazioneRepository repository,ConfigurazioneSection section,int id,string nome):base(host,repository)
    {Section=section;_id=id;Title=ConfigurazioneTitles.Links(section)+" · "+nome;}
    protected override async Task OnLoading()
    {
        await RunOperation(async ct=>{
            var result=await Repository.Execute(new(){Section=Section,Action=ConfigurazioneAction.LinksRead,RecordId=_id},ct);
            Scelte.Clear();foreach(var option in result.Scelte)Scelte.Add(new(option));Loaded();
        },"CARICAMENTO "+ConfigurazioneTitles.Links(Section)+" IN CORSO...");
    }
    protected override async Task OnSaving()
    {
        var ok=await RunOperation(async ct=>{
            await Repository.Execute(new(){Section=Section,Action=ConfigurazioneAction.LinksSave,RecordId=_id,
                SelectedIds=Scelte.Where(x=>x.Selezionato).Select(x=>x.Id).ToArray()},ct);
        },"SALVATAGGIO "+ConfigurazioneTitles.Links(Section)+" IN CORSO...");
        if(ok)Close(_id);
    }
    protected override void OnFinalDestruction(){Scelte.Clear();base.OnFinalDestruction();}
}

