using Soci.Contracts;
using AppServices.Database;
using Soci.Core.Repository;
using Soci.ViewModels.Map;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
using System.Collections.ObjectModel;
using ViewModels;
namespace Soci.ViewModels;

public sealed class SociViewModel : ViewModelBase, ISociViewModel
{
    private readonly ISociPersonRepository _repository;
    private readonly Signal<RxVoid> _backToMenu=new();
    private readonly StateSignal<bool> _notBusy=new(true), _group=new(false), _person=new(false),
        _socio=new(false), _card=new(false), _deletePerson=new(false), _deleteSocio=new(false), _searchReady=new(false), _reload=new(true);
    private StateSignal<bool>? _saveReady;
    private StateSignal<bool> SaveReady => _saveReady ??= new(false);
    protected override IObservable<bool> CanSave => SaveReady;
    private readonly List<IDisposable> _subscriptions=[];
    private readonly List<ReactiveCommand<RxVoid,RxVoid>> _commands=[];
    private bool _busy, _loaded, _showSearch, _limitReached, _wide;
    private string _info="", _sort="Cognome";
    private SociPersonMap? _selected;
    private SociTreeNode? _selectedNode;
    private SociInputBase? _editor;
    private SociRecord[] _records=[];
    public IObservable<RxVoid> SociToMenu => _backToMenu;
    public Interaction<string,RxVoid> FieldFocus { get; }=new();
    public ObservableCollection<SociPersonMap> DataSource { get; }=[];
    public ObservableCollection<SociTreeNode> GroupedDataSource { get; }=[];
    public SociTreeNode? GroupSelectedNode
    {
        get=>_selectedNode;
        set { this.RaiseAndSetIfChanged(ref _selectedNode,value); GroupBindingT=value?.Row; }
    }
    public PersonSearchViewModel Search { get; }=new();
    public string[] SortOptions { get; }=["Cognome","Nome","Nascita","Codice socio","Tessera"];
    public string SortBy { get=>_sort; set { this.RaiseAndSetIfChanged(ref _sort,value); SortRecords(); } }
    public SociPersonMap? GroupBindingT {
        get=>_selected;
        set { this.RaiseAndSetIfChanged(ref _selected,value); Refresh(); }
    }
    public SociInputBase? Editor { get=>_editor; private set { this.RaiseAndSetIfChanged(ref _editor,value); Refresh(); } }
    public bool IsWideLayout { get=>_wide; set { this.RaiseAndSetIfChanged(ref _wide,value); Refresh(); } }
    public bool IsGroupVisible => IsWideLayout || (!IsEditorVisible && !IsSearchVisible);
    public bool IsBusy => _busy;
    public bool GroupEnabled => _loaded&&!_busy&&Editor is null&&!IsSearchVisible;
    public bool IsEditorVisible => Editor is not null;
    public string SaveButtonText => Editor?.SaveText ?? "SALVA";
    public bool IsSearchVisible => _showSearch;
    public string ResultText => $"{DataSource.Count} {(DataSource.Count==1?"registrazione":"registrazioni")}" + (_limitReached ? " — primi 100 risultati: restringi la ricerca":"");
    public string InfoLabel { get=>_info; private set=>this.RaiseAndSetIfChanged(ref _info,value); }
    public ReactiveCommand<RxVoid,RxVoid> BlocchiCommand { get; }
    public ReactiveCommand<RxVoid,RxVoid> AddCommand { get; }
    public ReactiveCommand<RxVoid,RxVoid> UpdCommand { get; }
    public ReactiveCommand<RxVoid,RxVoid> DelCommand { get; }
    public ReactiveCommand<RxVoid,RxVoid> AddCodiceSocioCommand { get; }
    public ReactiveCommand<RxVoid,RxVoid> UpdCodiceSocioCommand { get; }
    public ReactiveCommand<RxVoid,RxVoid> DelCodiceSocioCommand { get; }
    public ReactiveCommand<RxVoid,RxVoid> AddTesseraCommand { get; }
    public ReactiveCommand<RxVoid,RxVoid> UpdTesseraCommand { get; }
    public ReactiveCommand<RxVoid,RxVoid> DelTesseraCommand { get; }
    public ReactiveCommand<RxVoid,RxVoid> PersonSearchCommand { get; }
    public ReactiveCommand<RxVoid,RxVoid> SearchCommand { get; }
    public ReactiveCommand<RxVoid,RxVoid> FilterCommand { get; }
    public ReactiveCommand<RxVoid,RxVoid> CancelCommand { get; }
    public ReactiveCommand<RxVoid,RxVoid> MenuCommand { get; }

    public SociViewModel(ISociPersonRepository? repository=null,IScreen? host=null):base(host)
    {
        _repository=repository??new SociPersonRepository();
        BlocchiCommand=Command(()=>OpenEditor(new BloccoSocioViewModel(SelectedData())),_person);
        AddCommand=Command(()=>OpenEditor(new PersonAddViewModel()),_group);
        UpdCommand=Command(()=>OpenEditor(new PersonUpdViewModel(SelectedData())),_person);
        DelCommand=Command(()=>OpenEditor(new PersonDelViewModel(SelectedData())),_deletePerson);
        AddCodiceSocioCommand=Command(()=>OpenEditor(new CodiceSocioAddViewModel(SelectedData())),_person);
        UpdCodiceSocioCommand=Command(()=>OpenEditor(new CodiceSocioUpdViewModel(SelectedData())),_socio);
        DelCodiceSocioCommand=Command(()=>OpenEditor(new CodiceSocioDelViewModel(SelectedData())),_deleteSocio);
        AddTesseraCommand=Command(()=>OpenEditor(new TesseraAddViewModel(SelectedData())),_socio);
        UpdTesseraCommand=Command(()=>OpenEditor(new TesseraUpdViewModel(SelectedData())),_card);
        DelTesseraCommand=Command(()=>OpenEditor(new TesseraDelViewModel(SelectedData())),_card);
        PersonSearchCommand=Command(()=>{ _showSearch=true; InfoLabel=""; Refresh(); },_group);
        CancelCommand=Command(CloseInput,_notBusy);
        MenuCommand=Command(GoMenu,_notBusy);
        SearchCommand=TaskCommand(()=>RunOperation(async()=>{
            var query=Search.ToSearch(); await LoadCore(query); _showSearch=false;
        },"RICERCA SOCI IN CORSO..."),_searchReady);
        FilterCommand=TaskCommand(()=>RunOperation(()=>LoadCore(new()),"CARICAMENTO SOCI IN CORSO..."),_reload);
    }
    private SociRecord SelectedData()=>GroupBindingT?.ToDto()??throw new SociValidationException("Seleziona una registrazione.");
    private ReactiveCommand<RxVoid,RxVoid> Command(Action action,IObservable<bool> allowed)
    {
        var command=ReactiveCommand.Create(action,allowed); Own(command); return command;
    }
    private ReactiveCommand<RxVoid,RxVoid> TaskCommand(Func<Task> action,IObservable<bool> allowed)
    {
        var command=ReactiveCommand.CreateFromTask(action,allowed); Own(command); return command;
    }
    private void Own(ReactiveCommand<RxVoid,RxVoid> command)
    {
        _commands.Add(command);
        _subscriptions.Add(command.ThrownExceptions.SubscribePrimitives(_=>InfoLabel="Operazione soci non riuscita."));
    }
    private void OpenEditor(SociInputBase editor) { InfoLabel=""; Editor=editor; FocusEditor(); }
    private async void FocusEditor()
    {
        if(Editor is { } editor) await TriggerInteraction(FieldFocus,editor.FocusName,100);
    }
    private void CloseInput() { Editor=null; _showSearch=false; InfoLabel=""; Refresh(); }
    private void GoMenu()
    {
        if(_busy||_isClosing)return;
        _isClosing=true; Refresh(); _backToMenu.OnNext(RxVoid.Default);
    }
    protected override Task OnLoading()=>RunOperation(()=>LoadCore(new()),"CARICAMENTO SOCI IN CORSO...");
    private async Task LoadCore(SociSearch query)
    {
        var result=await _repository.Execute(new SociRequest { Action="load", Search=query },Token);
        Token.ThrowIfCancellationRequested();
        _records=result.Records; _limitReached=result.LimitReached; _loaded=true; SortRecords();
        if(_records.Length==0)InfoLabel="Nessuna registrazione trovata.";
    }
    private void SortRecords()
    {
        var previous=GroupBindingT;
        var previousKind=GroupSelectedNode?.Kind;
        var expansion=GroupedDataSource.SelectMany(x=>x.DescendantsAndSelf()).ToDictionary(x=>(x.Kind,x.Row.Id,x.Row.CodiceSocio,x.Row.CodiceTessera),x=>x.IsExpanded);
        IEnumerable<SociRecord> rows=SortBy switch {
            "Nome"=>_records.OrderBy(x=>x.Nome).ThenBy(x=>x.Cognome),
            "Nascita"=>_records.OrderBy(x=>x.Natoil),
            "Codice socio"=>_records.OrderBy(x=>x.NumeroSocio),
            "Tessera"=>_records.OrderBy(x=>x.NumeroTessera),
            _=>_records.OrderBy(x=>x.Cognome).ThenBy(x=>x.Nome) };
        DataSource.Clear(); foreach(var row in rows)DataSource.Add(new SociPersonMap(row));
                GroupedDataSource.Clear();
        foreach(var root in SociTreeNode.Build(DataSource))
        {
            foreach(var node in root.DescendantsAndSelf())
                if(expansion.TryGetValue((node.Kind,node.Row.Id,node.Row.CodiceSocio,node.Row.CodiceTessera),out bool expanded))node.IsExpanded=expanded;
            GroupedDataSource.Add(root);
        }
        var nodes=GroupedDataSource.SelectMany(x=>x.DescendantsAndSelf()).ToArray();
        GroupSelectedNode=nodes.FirstOrDefault(x=>previous is not null
            &&(previousKind is null||x.Kind==previousKind)&&x.Row.Id==previous.Id
            &&x.Row.CodiceSocio==previous.CodiceSocio&&x.Row.CodiceTessera==previous.CodiceTessera)
            ??nodes.FirstOrDefault(x=>x.Kind==SociNodeKind.Tessera)
            ??nodes.FirstOrDefault(x=>x.Kind==SociNodeKind.Socio)??nodes.FirstOrDefault();
        this.RaisePropertyChanged(nameof(ResultText));
    }
    protected override Task OnSaving()=>RunOperation(async()=>{
        var editor=Editor??throw new SociValidationException("Apri prima una scheda.");
        editor.Validate();
        var result=await _repository.Execute(new SociRequest { Action=editor.Action,Record=editor.ToDto() },Token);
        Token.ThrowIfCancellationRequested();
        Editor=null; _showSearch=false;
        WaitMessage="AGGIORNAMENTO ELENCO SOCI...";
        await LoadCore(new SociSearch { PersonId=result.PersonId });
        if(_records.Length>0)InfoLabel="Operazione completata.";
    },Editor?.IsDelete==true?"ELIMINAZIONE IN CORSO...":"SALVATAGGIO IN CORSO...");
    private async Task RunOperation(Func<Task> action,string message)
    {
        if(_busy||_isClosing)return;
        WaitMessage=message; _busy=true; IsWaiting=true; InfoLabel=""; Refresh();
        try { await action(); }
        catch(OperationCanceledException) { }
        catch(SociValidationException ex) { InfoLabel=ex.Message; }
        catch(DatabaseHostException ex) { InfoLabel=ex.Message; }
        catch(Exception) { InfoLabel="Operazione soci non riuscita. Verifica la connessione al database."; }
        finally { _busy=false; IsWaiting=false; WaitMessage=""; if(!_isClosing){Refresh();FocusEditor();} }
    }
    private void Refresh()
    {
        bool available=!_busy&&!_isClosing;
        _notBusy.Value=available; _reload.Value=available&&Editor is null&&!_showSearch;
        bool group=available&&_loaded&&Editor is null&&!_showSearch;
        _group.Value=group; _person.Value=group&&GroupBindingT?.Id>0;
        _socio.Value=group&&GroupBindingT?.CodiceSocio>0; _card.Value=group&&GroupBindingT?.CodiceTessera>0;
        _deletePerson.Value=_person.Value&&!DataSource.Any(x=>x.Id==GroupBindingT!.Id&&x.CodiceSocio>0);
        _deleteSocio.Value=_socio.Value&&!DataSource.Any(x=>x.Id==GroupBindingT!.Id&&x.CodiceSocio==GroupBindingT.CodiceSocio&&x.CodiceTessera>0);
        SaveReady.Value=available&&Editor is not null; _searchReady.Value=available&&_showSearch;
        foreach(var property in new[]{nameof(IsBusy),nameof(GroupEnabled),nameof(IsEditorVisible),nameof(SaveButtonText),nameof(IsSearchVisible),nameof(IsGroupVisible)})
            this.RaisePropertyChanged(property);
    }
    protected override Task OnEsc() { if(!_busy){if(Editor is not null||_showSearch)CloseInput();else GoMenu();}return Task.CompletedTask; }
    protected override void OnFinalDestruction()
    {
        _isClosing=true;
        foreach(var subscription in _subscriptions)subscription.Dispose();
        foreach(var command in _commands)command.Dispose();
        _backToMenu.Dispose();
        foreach(var state in new[]{_notBusy,_group,_person,_socio,_card,_deletePerson,_deleteSocio,_searchReady,_reload,SaveReady})state.Dispose();
        base.OnFinalDestruction();
    }
}
