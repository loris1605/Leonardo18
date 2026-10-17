using System.Collections.ObjectModel;
using AppServices.Database;
using Configurazione.Contracts;
using Configurazione.Core.Repository;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
namespace Configurazione.ViewModels;

public sealed class ConfigurazioneTreeNode(ConfigurazioneRecord record,string label,string details,ConfigurazioneTreeNode[]? children=null)
{
#if DEBUG
    ~ConfigurazioneTreeNode() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif
    public ConfigurazioneRecord Record { get; }=record;
    public string Label { get; }=label;
    public string Dettagli { get; }=details;
    public ConfigurazioneTreeNode[] Children { get; }=children??[];
}
public sealed class ConfigurazioneGroupViewModel : ConfigurazioneChildBase,IConfigurazioneGroupViewModel
{
    private readonly StateSignal<bool> _ready=new(false),_selectedReady=new(false);
    private readonly Signal<ConfigurazioneNavigation> _navigation=new();
    private readonly List<IDisposable> _subscriptions=[];
    private ConfigurazioneRecord[] _records=[];
    private readonly int _selectionId;
    private bool _loaded;
    private string _search="";
    private ConfigurazioneTreeNode? _selected;
    public ConfigurazioneSection Section { get; }
    public string Title=>ConfigurazioneTitles.Plural(Section);
    public string LinksText=>ConfigurazioneTitles.Links(Section);
    public bool HasLinks=>Section is ConfigurazioneSection.Operatori or ConfigurazioneSection.Postazioni or ConfigurazioneSection.Settori;
    public IObservable<ConfigurazioneNavigation> Navigation=>_navigation;
    public ObservableCollection<ConfigurazioneTreeNode> Rows { get; }=[];
    public ConfigurazioneTreeNode? Selected {
        get=>_selected;
        set { this.RaiseAndSetIfChanged(ref _selected,value);RefreshCommands(); }
    }
    public string SearchText {
        get=>_search;
        set {this.RaiseAndSetIfChanged(ref _search,value);ApplyFilter();}
    }
    public string ResultText=>$"{Rows.Count} elementi";
    public ReactiveCommand<RxVoid,RxVoid> AddCommand { get; }
    public ReactiveCommand<RxVoid,RxVoid> EditCommand { get; }
    public ReactiveCommand<RxVoid,RxVoid> DeleteCommand { get; }
    public ReactiveCommand<RxVoid,RxVoid> LinksCommand { get; }
    public ReactiveCommand<RxVoid,RxVoid> RefreshCommand { get; }
    public ConfigurazioneGroupViewModel(IScreen host,IConfigurazioneRepository repository,ConfigurazioneSection section,int selectionId=0):base(host,repository)
    {
        Section=section;_selectionId=selectionId;
        AddCommand=ReactiveCommand.Create(()=>Request(ConfigurazioneAction.Add),_ready);
        EditCommand=ReactiveCommand.Create(()=>Request(ConfigurazioneAction.Update),_selectedReady);
        DeleteCommand=ReactiveCommand.Create(()=>Request(ConfigurazioneAction.Delete),_selectedReady);
        LinksCommand=ReactiveCommand.Create(()=> {if(HasLinks)Request(ConfigurazioneAction.LinksSave);},_selectedReady);
        RefreshCommand=ReactiveCommand.CreateFromTask(OnLoading,_ready);
        foreach(var command in new[]{AddCommand,EditCommand,DeleteCommand,LinksCommand,RefreshCommand})
            _subscriptions.Add(command.ThrownExceptions.SubscribePrimitives(_=>InfoLabel="Operazione elenco non riuscita."));
    }
    protected override async Task OnLoading()
    {
        await RunOperation(async ct=>{
            var result=await Repository.Execute(new(){Section=Section,Action=ConfigurazioneAction.Load},ct);
            ct.ThrowIfCancellationRequested();
            if(_isClosing)return;
            _records=result.Records;
            _loaded=true;ApplyFilter();
        },"CARICAMENTO "+Title+" IN CORSO...");
    }
    private void ApplyFilter()
    {
        if(_isClosing)return;
        var id=Selected?.Record.Id??_selectionId;
        Rows.Clear();
        foreach(var r in _records.Where(x=>string.IsNullOrWhiteSpace(SearchText)
            ||x.Nome.Contains(SearchText,StringComparison.OrdinalIgnoreCase)
            ||x.Relazioni.Any(n=>n.Contains(SearchText,StringComparison.OrdinalIgnoreCase))).OrderBy(x=>x.Nome))
        {
            var details=Section==ConfigurazioneSection.Tariffe?$"{r.Etichetta} · {r.Prezzo:0.00} €"+(r.FreeDrink?" · Free drink":""):
                Section==ConfigurazioneSection.Rientri?$"Durata: {r.DurataOre} ore":r.Dettagli;
            Rows.Add(new(r,r.Nome,details,r.Relazioni.Select(n=>new ConfigurazioneTreeNode(r,n,"")).ToArray()));
        }
        Selected=Rows.FirstOrDefault(x=>x.Record.Id==id)??Rows.FirstOrDefault();
        this.RaisePropertyChanged(nameof(ResultText));
    }
    private void Request(ConfigurazioneAction action)
    {
        if(_isClosing||IsBusy)return;
        _navigation.OnNext(new(action,action==ConfigurazioneAction.Add?0:Selected?.Record.Id??0,Selected?.Record.Nome??""));
    }
    protected override void RefreshCommands()
    {
        // La rimozione delle righe può aggiornare Selected mentre la vista si chiude.
        if(_isClosing)return;
        _ready.Value=_loaded&&!IsBusy;
        _selectedReady.Value=_ready.Value&&Selected is not null;
    }
    protected override void OnFinalDestruction()
    {
        _isClosing=true;
        foreach(var s in _subscriptions)s.Dispose();_subscriptions.Clear();
        foreach(var c in new[]{AddCommand,EditCommand,DeleteCommand,LinksCommand,RefreshCommand})c.Dispose();
        _navigation.Dispose();_ready.Dispose();_selectedReady.Dispose();
        Rows.Clear();_records=[];_selected=null;
        base.OnFinalDestruction();
    }
}
