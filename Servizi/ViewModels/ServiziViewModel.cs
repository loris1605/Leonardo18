using System.Collections.ObjectModel;
using AppServices.Database;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
using Servizi.Contracts;
using Servizi.Core.Repository;
using ViewModels;
namespace Servizi.ViewModels;

public sealed partial class ServiziViewModel : ViewModelBase, IServiziViewModel
{
    private readonly IServiziRepository _repository;
    private readonly Signal<RxVoid> _menu=new();
    private readonly StateSignal<bool> _ready=new(false);
    private readonly List<IDisposable> _errors=[];
    private ServiziRecord[] _all=[];
    private ServiziRecord? _selected;
    private ServiziAction _action;
    private int _id;
    private bool _busy,_editor,_loaded;
    private string _search="",_info="",_nome="",_sort="Nome";
    private decimal? _prezzo=0,_ingressi=0,_giorni=0;
    private bool _attivo=true;
    public ObservableCollection<ServiziRecord> Records { get; }=[];
    public string[] SortOptions { get; }=["Nome","Ingressi","Durata","Prezzo","Stato"];
    public string SortBy { get=>_sort; set { if(_isClosing)return; this.RaiseAndSetIfChanged(ref _sort,value); Filter(); } }
    public ServiziRecord? Selected { get=>_selected; set {if(!_isClosing)this.RaiseAndSetIfChanged(ref _selected,value);} }
    public string SearchText {get=>_search;set {if(_isClosing)return;this.RaiseAndSetIfChanged(ref _search,value);Filter();}}
    public string InfoLabel {get=>_info;private set=>this.RaiseAndSetIfChanged(ref _info,value);}
    public bool IsBusy {get=>_busy;private set {this.RaiseAndSetIfChanged(ref _busy,value); UpdateReady();}}
    public bool HasEditor {get=>_editor;private set {this.RaiseAndSetIfChanged(ref _editor,value);this.RaisePropertyChanged(nameof(IsEditable));UpdateReady();}}
    public bool GroupEnabled=>_loaded&&!IsBusy&&!HasEditor;
    public bool IsEditable=>HasEditor&&_action!=ServiziAction.Delete;
    public string EditorTitle=>IsFidelity?(_action switch {ServiziAction.Add=>"NUOVA FIDELITY",ServiziAction.Delete=>"ELIMINA FIDELITY",_=>"MODIFICA FIDELITY"}):(_action switch {ServiziAction.Add=>"NUOVO ABBONAMENTO",ServiziAction.Delete=>"ELIMINA ABBONAMENTO",_=>"MODIFICA ABBONAMENTO"});
    public FluentIcons.Common.Symbol SaveIcon => _action==ServiziAction.Delete?FluentIcons.Common.Symbol.Delete:FluentIcons.Common.Symbol.Save;
    public string SaveButtonText=>_action==ServiziAction.Delete?"ELIMINA":"SALVA";
    public string Nome {get=>_nome;set{if(!_isClosing)this.RaiseAndSetIfChanged(ref _nome,value);}}
    public decimal? Prezzo {get=>_prezzo;set{if(!_isClosing)this.RaiseAndSetIfChanged(ref _prezzo,value);}}
    public decimal? NumeroIngressi {get=>_ingressi;set{if(!_isClosing)this.RaiseAndSetIfChanged(ref _ingressi,value);}}
    public decimal? DurataGiorni {get=>_giorni;set{if(!_isClosing)this.RaiseAndSetIfChanged(ref _giorni,value);}}
    public bool Attivo {get=>_attivo;set{if(!_isClosing)this.RaiseAndSetIfChanged(ref _attivo,value);}}
    public IObservable<RxVoid> ServiziToMenu=>_menu;
    public ReactiveCommand<RxVoid,RxVoid> MenuCommand {get;}
    public ReactiveCommand<RxVoid,RxVoid> AddCommand {get;}
    public ReactiveCommand<RxVoid,RxVoid> UpdCommand {get;}
    public ReactiveCommand<RxVoid,RxVoid> DelCommand {get;}
    public ReactiveCommand<RxVoid,RxVoid> RefreshCommand {get;}
    public ReactiveCommand<RxVoid,RxVoid> CancelCommand {get;}
    public ServiziViewModel(IScreen? host,IServiziRepository repository):base(host,"servizi")
    {
        _repository=repository;InitializeSections();
        MenuCommand=ReactiveCommand.Create(()=>{if(!IsBusy&&!HasEditor)_menu.OnNext(RxVoid.Default);});
        AddCommand=ReactiveCommand.Create(()=>Open(new(),ServiziAction.Add),_ready);
        UpdCommand=ReactiveCommand.CreateFromTask(()=>Read(ServiziAction.Update),_ready);
        DelCommand=ReactiveCommand.CreateFromTask(()=>Read(ServiziAction.Delete),_ready);
        RefreshCommand=ReactiveCommand.CreateFromTask(()=>Run(new(){Action=ServiziAction.Load}));
        CancelCommand=ReactiveCommand.Create(()=>{if(!IsBusy) {HasEditor=false;InfoLabel="";}});
        foreach(var cmd in new[]{MenuCommand,AddCommand,UpdCommand,DelCommand,RefreshCommand,CancelCommand})
            _errors.Add(cmd.ThrownExceptions.SubscribePrimitives(ex=>{if(!_isClosing)InfoLabel=ex.Message;}));
    }
    private void UpdateReady() {if(_isClosing)return;_ready.Value=GroupEnabled;this.RaisePropertyChanged(nameof(GroupEnabled));}
    private void Filter()
    {
        var id=Selected?.Id;IEnumerable<ServiziRecord> rows=_all.Where(x=>x.Nome.Contains(SearchText.Trim(),StringComparison.CurrentCultureIgnoreCase));
        rows=SortBy switch {"Ingressi"=>rows.OrderBy(x=>x.NumeroIngressi).ThenBy(x=>x.Nome),"Durata"=>rows.OrderBy(x=>x.DurataGiorni).ThenBy(x=>x.Nome),"Prezzo"=>rows.OrderBy(x=>x.Prezzo).ThenBy(x=>x.Nome),"Stato"=>rows.OrderByDescending(x=>x.Attivo).ThenBy(x=>x.Nome),_=>rows.OrderBy(x=>x.Nome,StringComparer.CurrentCultureIgnoreCase)};
        Records.Clear();foreach(var r in rows)Records.Add(r);Selected=Records.FirstOrDefault(x=>x.Id==id);
    }
    protected override Task OnLoading()=>Run(new(){Action=ServiziAction.Load});
    private async Task Run(ServiziRequest request)
    {
        if(_isClosing||IsBusy||(HasEditor&&request.Action==ServiziAction.Load))return;
        request.Section=IsFidelity?ServiziSection.Fidelity:ServiziSection.Abbonamenti;IsBusy=true;InfoLabel="";
        try {
            var result=await _repository.Execute(request,Token);if(_isClosing)return;
            _all=result.Records;_loaded=true;
            if(request.Action!=ServiziAction.Load){HasEditor=false;SearchText="";}
            Filter();Selected=Records.FirstOrDefault(x=>x.Id==result.SelectedId)??Selected;
            InfoLabel=request.Action==ServiziAction.Load?$"{Records.Count} "+(IsFidelity?"tipi Fidelity.":"tipi di abbonamento."):"Operazione completata.";
        }
        catch(OperationCanceledException) when(Token.IsCancellationRequested){}
        catch(Exception ex){if(!_isClosing)InfoLabel=ex.Message;}
        finally {if(!_isClosing)IsBusy=false;}
    }
    private async Task Read(ServiziAction action)
    {
        if(_isClosing||!GroupEnabled)return;
        if(Selected is null){InfoLabel="Seleziona una voce in elenco.";return;}
        var id=Selected.Id;IsBusy=true;InfoLabel="";
        try {var result=await _repository.Execute(new(){Action=ServiziAction.Read,RecordId=id,Section=IsFidelity?ServiziSection.Fidelity:ServiziSection.Abbonamenti},Token);if(!_isClosing)Open(result.Records.Single(),action);}
        catch(OperationCanceledException) when(Token.IsCancellationRequested){}
        catch(Exception ex){if(!_isClosing)InfoLabel=ex.Message;}
        finally{if(!_isClosing)IsBusy=false;}
    }
    private void Open(ServiziRecord r,ServiziAction action)
    {
        if(_isClosing)return;
        _action=action;_id=r.Id;Nome=r.Nome;Prezzo=r.Prezzo;NumeroIngressi=r.NumeroIngressi;DurataGiorni=r.DurataGiorni;Attivo=action==ServiziAction.Add||r.Attivo;SelectedPremio=r.TipoPremioId==2?"Sconto in denaro":"Ingresso Fidelity";
        HasEditor=true;InfoLabel=action==ServiziAction.Delete?"Conferma l'eliminazione del tipo di abbonamento selezionato.":"";
        this.RaisePropertyChanged(nameof(EditorTitle));this.RaisePropertyChanged(nameof(SaveButtonText));this.RaisePropertyChanged(nameof(SaveIcon));this.RaisePropertyChanged(nameof(IsEditable));
    }
    protected override Task OnSaving()
    {
        if(!HasEditor||IsBusy||_isClosing)return Task.CompletedTask;
        if(_action!=ServiziAction.Delete && (Nome.Trim().Length is <2 or >25 || NumeroIngressi is null or <0 or >int.MaxValue || DurataGiorni is null or <0 or >int.MaxValue
            || NumeroIngressi!=decimal.Truncate(NumeroIngressi.Value) || DurataGiorni!=decimal.Truncate(DurataGiorni.Value)
            || Prezzo is null or <0 or >9999999999999999.99m || decimal.Round(Prezzo.Value,2)!=Prezzo))
        {InfoLabel="Verifica nome (2–25 caratteri), ingressi e giorni interi non negativi e prezzo con massimo due decimali.";return Task.CompletedTask;}
        if(IsFidelity&&_action!=ServiziAction.Delete&&(NumeroIngressi<1||SelectedPremio=="Sconto in denaro"&&Prezzo<=0)){InfoLabel="Imposta un traguardo positivo e un importo per il premio sconto.";return Task.CompletedTask;}
        return Run(new(){Action=_action,RecordId=_id,Record=new(){Id=_id,Nome=Nome,TipoPremioId=SelectedPremio=="Sconto in denaro"?2:1,Prezzo=IsFidelity&&SelectedPremio!="Sconto in denaro"?0:Prezzo??0,NumeroIngressi=(int)(NumeroIngressi??0),DurataGiorni=(int)(DurataGiorni??0),Attivo=Attivo}});
    }
    protected override Task OnEsc(){if(!IsBusy){if(HasEditor)HasEditor=false;else _menu.OnNext(RxVoid.Default);}return Task.CompletedTask;}
    protected override void OnFinalDestruction()
    {
        foreach(var subscription in _errors)subscription.Dispose();
        foreach(var command in new[]{MenuCommand,AddCommand,UpdCommand,DelCommand,RefreshCommand,CancelCommand})command.Dispose();
        AbbonamentiSectionCommand.Dispose();FidelitySectionCommand.Dispose();_ready.Dispose();_menu.Dispose();Records.Clear();_all=[];base.OnFinalDestruction();
    }
}