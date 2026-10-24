using Menu.Contracts;
using AppServices.Database;
using Menu.Core.Repository;
using Menu.ViewModels.Map;
using Models.Entity.Global;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
using System.Collections.ObjectModel;
using ViewModels;
namespace Menu.ViewModels;

public sealed partial class MenuViewModel : ViewModelBase, IMenuViewModel
{
    private readonly IMenuRepository _repository;
    private readonly StateSignal<bool> _ready = new(false), _open = new(false), _close = new(false);
    private readonly Signal<string> _navigation = new();
    private readonly List<IDisposable> _subscriptions = [];
    private bool _busy, _loaded, _dayOpen, _admin, _cash, _report;
    private string _info = "";
    public IObservable<string> Navigation => _navigation;
    public ObservableCollection<MenuPostazioneMap> CassaPostazioniDataSource { get; } = [];
    private MenuPostazioneMap? _selected;
    public MenuPostazioneMap? SelectedPostazione
    {
        get => _selected;
        set => this.RaiseAndSetIfChanged(ref _selected, value);
    }
    public ReactiveCommand<RxVoid, RxVoid> LogoutCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> ConnectionCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> ConfigurazioneCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> SociCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> ServiziCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> ReportCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> ApriGiornataCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> ChiudiGiornataCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> CassaPostazioneCommand { get; }
    public string OperatoreName => "Operatore: " + GlobalValuesC.MySetting?.NOMEOPERATORE;
    public bool IsBusy => _busy;
    public bool IsMenuReady => _loaded && !_busy && !IsOpeningCash;
    public bool AmministratoreVisible => _admin;
    public bool CassaVisible => _cash;
    public bool ReportVisible => _report;
    public string SessioneContabile => "Sessione contabile " + (_dayOpen ? "aperta" : "chiusa");
    public string InfoError { get => _info; private set => this.RaiseAndSetIfChanged(ref _info, value); }

    public MenuViewModel(IMenuRepository? repository = null, IScreen? host = null, IMirrorClient? mirrorClient=null) : base(host)
    {
        _repository = repository ?? new MenuRepository();
        InitializeMirror(mirrorClient??(repository is null?new MirrorClient():null));
        LogoutCommand = ReactiveCommand.Create(() => Navigate("login"), _ready);
        ConnectionCommand = ReactiveCommand.Create(() => Navigate("connection"), _ready);
        SociCommand = ReactiveCommand.Create(() => { if (_admin) Navigate("soci"); }, _ready);
        ConfigurazioneCommand = ReactiveCommand.Create(() => { if (_admin) Navigate("configurazione"); }, _ready);
        ServiziCommand = ReactiveCommand.Create(() => { if (_admin) Navigate("servizi"); }, _ready);
        ReportCommand = ReactiveCommand.Create(() => { if (_report) Navigate("report"); }, _ready);
        CassaPostazioneCommand = ReactiveCommand.Create(() => {
            if (!_cash || !_dayOpen || SelectedPostazione is null) return;
            GlobalValuesC.MyPostazione = GlobalValuesC.MySetting?.POSTAZIONI?
                .FirstOrDefault(x => x.CODICEPOSTAZIONE == SelectedPostazione.CodicePostazione);
            if (GlobalValuesC.MyPostazione is null) { InfoError = "Postazione non disponibile. Accedi nuovamente."; return; }
            Navigate("cassa");
        }, _close);
        InitializeCassaIniziale();
        ApriGiornataCommand = ReactiveCommand.Create(BeginOpening, _open);
        ChiudiGiornataCommand = ReactiveCommand.CreateFromTask(
            () => RunOperation("menu-close", "CHIUSURA GIORNATA IN CORSO..."), _close);
        foreach (var command in new[] { LogoutCommand, ConnectionCommand, SociCommand, ConfigurazioneCommand,
            ServiziCommand, ReportCommand, CassaPostazioneCommand, ApriGiornataCommand, ChiudiGiornataCommand })
            _subscriptions.Add(command.ThrownExceptions.SubscribePrimitives(_ => InfoError = "Operazione menu non riuscita."));
    }
    protected override async Task OnLoading()
    {
        await RunOperation("menu-load", "CARICAMENTO MENU IN CORSO...");
        if(!_isClosing)_=PollMirror();
    }
    private async Task RunOperation(string operation, string message, MenuAperturaRequest? apertura=null)
    {
        if (_busy || _isClosing) return;
        WaitMessage = message; _busy = true; IsWaiting = true; InfoError = ""; Refresh();
        try
        {
            var setting = GlobalValuesC.MySetting ?? throw new InvalidOperationException("Accedi nuovamente.");
            var state = operation=="menu-open"
                ? await _repository.OpenDay(setting.IDOPERATORE,apertura ?? throw new InvalidOperationException("Conteggio cassa assente."),Token)
                : await _repository.Execute(setting.IDOPERATORE, operation, Token);
            Token.ThrowIfCancellationRequested();
            _admin = state.Stations.Any(x => x.TypeId == 1);
            _cash = state.Stations.Any(x => x.TypeId == 2);
            _report = _admin || _cash || state.Stations.Any(x => x.TypeId == 6);
            _dayOpen = state.Day?.Open == true;
            setting.GIORNATA = state.Day is null ? null : new GiornataXC {
                IDGIORNATA = state.Day.Id, DATAINIZIO = state.Day.Start, DATAFINE = state.Day.End, APERTA = state.Day.Open };
            CassaPostazioniDataSource.Clear();
            foreach (var station in state.Stations.Where(x => x.TypeId == 2).DistinctBy(x => x.Id))
                CassaPostazioniDataSource.Add(new(station.Id, station.Name));
            SelectedPostazione = CassaPostazioniDataSource.FirstOrDefault();
            _loaded = true;
            if(operation=="menu-open") _openingCash=false;
        }
        catch (OperationCanceledException) { }
        catch (DatabaseHostException ex) { InfoError = ex.Message; }
        catch (InvalidOperationException ex) { InfoError = ex.Message; }
        catch (Exception) { InfoError = "Impossibile aggiornare il menu. Verifica la connessione."; }
        finally { _busy = false; IsWaiting = false; WaitMessage = ""; Refresh(); }
    }
    private void Refresh()
    {
        RefreshMirror();
        _ready.Value = !_busy && !_isClosing && !IsOpeningCash;
        _openingReady.Value = _loaded && !_busy && !_isClosing && IsOpeningCash;
        _open.Value = _loaded && _ready.Value && (_admin || _cash) && !_dayOpen;
        _close.Value = _loaded && _ready.Value && (_admin || _cash) && _dayOpen;
        foreach (var property in new[] { nameof(IsBusy), nameof(IsMenuReady), nameof(AmministratoreVisible),
            nameof(CassaVisible), nameof(ReportVisible), nameof(SessioneContabile), nameof(IsOpeningCash), nameof(IsOpeningReady) }) this.RaisePropertyChanged(property);
    }
    private void Unavailable(string name, bool allowed)
    { if (allowed) InfoError = $"La funzione {name} non è ancora disponibile."; }
    private void Navigate(string destination)
    { _isClosing = true; Refresh(); _navigation.OnNext(destination); }
    protected override Task OnEsc() { if (!_busy) { if(IsOpeningCash) CancelOpening(); else Navigate("login"); } return Task.CompletedTask; }
    protected override void OnFinalDestruction()
    {
        _isClosing = true;
        DisposeMirror();
        foreach (var subscription in _subscriptions) subscription.Dispose();
        foreach (var command in new[] { LogoutCommand, ConnectionCommand, SociCommand, ConfigurazioneCommand,
            ServiziCommand, ReportCommand, CassaPostazioneCommand, ApriGiornataCommand, ChiudiGiornataCommand })
            command.Dispose();
        ConfermaAperturaCommand.Dispose(); AnnullaAperturaCommand.Dispose(); _openingReady.Dispose();
        _navigation.Dispose(); _ready.Dispose(); _open.Dispose(); _close.Dispose();
        base.OnFinalDestruction();
    }
}

