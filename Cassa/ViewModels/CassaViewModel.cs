using System.Collections.ObjectModel;
using AppServices.Database;
using Cassa.Contracts;
using Cassa.Core.Repository;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
using ViewModels;

namespace Cassa.ViewModels;

public sealed record CassaSettoreSpesa(string Nome, CassaVoceSpesa[] Voci);

public sealed partial class CassaViewModel : ViewModelBase, ICassaViewModel
{
    private readonly ICassaRepository _repository;
    private readonly int _postazioneId;
    private readonly Signal<RxVoid> _toMenu = new();
    private readonly StateSignal<bool> _ready = new(false), _entryReady = new(false);
    private readonly List<IDisposable> _subscriptions = [];
    private readonly ReactiveCommand<RxVoid, RxVoid>[] _commands;
    public ReactiveCommand<CassaVoceSpesa, RxVoid> AddChargeCommand { get; }
    public ReactiveCommand<CassaConto, RxVoid> SetPagamentoCommand { get; }
    public ReactiveCommand<CassaConto, RxVoid> StornaContoCommand { get; }
    public ReactiveCommand<string, RxVoid> SortContoCommand { get; }
    private string _contoSort = nameof(CassaConto.DataOra);
    private bool _contoDescending;
    private string SortHeader(string column, string label) => label + (_contoSort == column ? (_contoDescending ? " ▼" : " ▲") : "");
    public string DataHeader => SortHeader(nameof(CassaConto.DataOra), "DATA E ORA");
    public string PostazioneHeader => SortHeader(nameof(CassaConto.DescPostazione), "POSTAZIONE");
    public string SettoreHeader => SortHeader(nameof(CassaConto.DescSettore), "SETTORE");
    public string DettaglioHeader => SortHeader(nameof(CassaConto.VoiceDesc), "DETTAGLIO");
    public string PrezzoHeader => SortHeader(nameof(CassaConto.VoicePrice), "PREZZO");
    public string StatoHeader => SortHeader(nameof(CassaConto.Stato), "STATO");
    private CassaScheda[] _inside = [];
    private string[] _occupied = [];
    private string _station = "", _position = "", _card = "", _entryPosition = "", _notes = "", _search = "", _info = "";
    private bool _busy, _loaded;
    private bool _paymentBusy;
    public bool IsPaymentBusy => _paymentBusy;
    private bool _guardarobaBusy;
    public bool IsGuardarobaBusy => _guardarobaBusy;
    private string _guardarobaMessage="Vuoto o 0 = libero";
    public string GuardarobaMessage { get => _guardarobaMessage; private set => this.RaiseAndSetIfChanged(ref _guardarobaMessage,value); }
    private string _receiverAddress = "";
    private Task? _receiverStopTask;
    private int _page;
    private CassaScheda? _sheet, _selected;
    private CassaSocio? _member;
    private CassaIngresso? _rate;
    private string _grb1="", _grb2="", _grb3="", _grb4="";
    public string Grb1 { get => _grb1; set => this.RaiseAndSetIfChanged(ref _grb1, value); }
    public string Grb2 { get => _grb2; set => this.RaiseAndSetIfChanged(ref _grb2, value); }
    public string Grb3 { get => _grb3; set => this.RaiseAndSetIfChanged(ref _grb3, value); }
    public string Grb4 { get => _grb4; set => this.RaiseAndSetIfChanged(ref _grb4, value); }
    public ReactiveCommand<RxVoid, RxVoid> SaveGuardarobaCommand { get; }

    public IObservable<RxVoid> CassaToMenu => _toMenu;
    public Interaction<RxVoid, RxVoid> PositionFocus { get; } = new();
    public Interaction<RxVoid, RxVoid> CardFocus { get; } = new();
    public Interaction<RxVoid, RxVoid> EntryPositionFocus { get; } = new();
    public ReactiveCommand<RxVoid, RxVoid> MenuCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> BackCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> OpenSheetCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> ClearSheetCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> EntraSocioCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> ListaSociCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> FindCardCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> RegisterEntryCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> OpenSelectedCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> RefreshListCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> OpenMemberSheetCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> ResetEntryCommand { get; }

    public string Title => "Leonardo18 - CASSA " + _station;
    public string ReceiverStatus => string.IsNullOrEmpty(_receiverAddress) ? "" : "Ricezione tessere attiva";
    public bool IsBusy => _busy;
    public bool IsUiReady => _loaded && !_busy && !_isClosing;
    public bool IsPostazione => _page == 0;
    public bool IsEntry => _page == 1;
    public bool IsList => _page == 2;
    public bool HasSheet => _sheet is not null;
    public bool HasMember => _member is not null;
    public bool MemberInside => !string.IsNullOrWhiteSpace(_member?.PosizioneOccupata);
    public CassaScheda? Sheet => _sheet;
    public CassaSocio? Member => _member;
    public bool ShowIngressi => Member?.HasSospesi != true;
    public ObservableCollection<CassaSospeso> SospesiIngresso { get; } = [];
    public ObservableCollection<CassaConto> Conto { get; } = [];
    public ObservableCollection<CassaVoceSpesa> VociSpesa { get; } = [];
    public bool HasVociSpesa => VociSpesa.Count != 0;
    public CassaSettoreSpesa[] SettoriSpesa { get; private set; } = [];
    private CassaSettoreSpesa? _selectedSettoreSpesa;
    public CassaSettoreSpesa? SelectedSettoreSpesa
    {
        get => _selectedSettoreSpesa;
        set => this.RaiseAndSetIfChanged(ref _selectedSettoreSpesa, value);
    }
    public ObservableCollection<CassaIngresso> Ingressi { get; } = [];
    public ObservableCollection<CassaScheda> Schede { get; } = [];
    public string InfoLabel { get => _info; private set => this.RaiseAndSetIfChanged(ref _info, value); }
    public string ResultText => $"{Schede.Count} soci all'interno";
    public decimal Totale => Conto.Sum(x => x.VoicePrice);
    public decimal DaPagare => Conto.Where(x => !x.Pagato).Sum(x => x.VoicePrice);
    public bool CanSelectIngresso => IsUiReady && Member is { HasSospesi:false };
    public string EntryWarning => CassaEntryRules.Validate(Member, NumeroTessera, EntryPosition, EntryValidationRate, _occupied, DateTime.Today);

    public string Posizione { get => _position; set => this.RaiseAndSetIfChanged(ref _position, value); }
    public string NumeroTessera
    {
        get => _card;
        set
        {
            if (_card == value) return;
            this.RaiseAndSetIfChanged(ref _card, value);
            SetMember(null); // Never register a different typed card with the previous person's identity.
        }
    }
    public string EntryPosition { get => _entryPosition; set { this.RaiseAndSetIfChanged(ref _entryPosition, value); RefreshEntry(); } }
    public string Note { get => _notes; set => this.RaiseAndSetIfChanged(ref _notes, value); }
    public string SearchText { get => _search; set { this.RaiseAndSetIfChanged(ref _search, value); ApplyFilter(); } }
    public CassaIngresso? SelectedIngresso { get => _rate; set { this.RaiseAndSetIfChanged(ref _rate, value); if(value is not null){ClearBollino();ClearFidelitySelection();} RefreshEntry(); } }
    public CassaScheda? SelectedScheda { get => _selected; set => this.RaiseAndSetIfChanged(ref _selected, value); }

    public CassaViewModel(IScreen host, ICassaRepository repository, int postazioneId) : base(host)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _postazioneId = postazioneId;
        InitializeAbbonamento();
        InitializeSospesi();InitializeStoricoFidelity();
        InitializeEntrySubscriptions();InitializeFidelity();
        InitializeUscita();
        // Returning to the menu must remain possible if loading the station fails.
        MenuCommand = ReactiveCommand.CreateFromTask(BackToMenu);
        BackCommand = ReactiveCommand.CreateFromTask(Back, _ready);
        OpenSheetCommand = ReactiveCommand.CreateFromTask(OpenSheet, _ready);
        ClearSheetCommand = ReactiveCommand.CreateFromTask(ClearSheet, _ready);
        EntraSocioCommand = ReactiveCommand.CreateFromTask(PrepareEntry, _ready);
        ListaSociCommand = ReactiveCommand.CreateFromTask(LoadList, _ready);
        RefreshListCommand = ReactiveCommand.CreateFromTask(LoadList, _ready);
        FindCardCommand = ReactiveCommand.CreateFromTask(FindCard, _ready);
        RegisterEntryCommand = ReactiveCommand.CreateFromTask(RegisterEntry, _entryReady);
        OpenSelectedCommand = ReactiveCommand.CreateFromTask(OpenSelected, _ready);
        OpenMemberSheetCommand = ReactiveCommand.CreateFromTask(OpenMemberSheet, _ready);
        ResetEntryCommand = ReactiveCommand.CreateFromTask(ResetEntry, _ready);
        SaveGuardarobaCommand = ReactiveCommand.CreateFromTask(SaveGuardaroba, _ready);
        AddChargeCommand = ReactiveCommand.CreateFromTask<CassaVoceSpesa>(AddCharge, _ready);
        SetPagamentoCommand = ReactiveCommand.CreateFromTask<CassaConto>(SetPagamento, _ready);
        StornaContoCommand = ReactiveCommand.CreateFromTask<CassaConto>(StornaConto, _ready);
        _subscriptions.Add(StornaContoCommand.ThrownExceptions.SubscribePrimitives(ex => {
            if (!_isClosing) InfoLabel="Storno non riuscito.";
            System.Diagnostics.Debug.WriteLine(ex);
        }));
        _subscriptions.Add(SetPagamentoCommand.ThrownExceptions.SubscribePrimitives(ex => {
            if (!_isClosing) InfoLabel = "Aggiornamento pagamento non riuscito.";
            System.Diagnostics.Debug.WriteLine(ex);
        }));
        SortContoCommand = ReactiveCommand.Create<string>(SortConto, _ready);
        _subscriptions.Add(SortContoCommand.ThrownExceptions.SubscribePrimitives(ex => {
            if (!_isClosing) InfoLabel = "Ordinamento non riuscito.";
            System.Diagnostics.Debug.WriteLine(ex);
        }));
        _subscriptions.Add(AddChargeCommand.ThrownExceptions.SubscribePrimitives(ex => {
            if (!_isClosing) InfoLabel = "Addebito non riuscito.";
            System.Diagnostics.Debug.WriteLine(ex);
        }));
        _commands = [MenuCommand, BackCommand, OpenSheetCommand, ClearSheetCommand, EntraSocioCommand,
            ListaSociCommand, RefreshListCommand, FindCardCommand, RegisterEntryCommand, OpenSelectedCommand,
            OpenMemberSheetCommand, ResetEntryCommand, SaveGuardarobaCommand];
        foreach (var command in _commands)
            _subscriptions.Add(command.ThrownExceptions.SubscribePrimitives(ex => {
                if (!_isClosing) InfoLabel = "Operazione cassa non riuscita.";
                System.Diagnostics.Debug.WriteLine(ex);
            }));
    }

    protected override async Task OnLoading()
    {
        await Run(CassaAction.Load, "CARICAMENTO CASSA IN CORSO...", result => {
            _station = result.PostazioneName; _loaded = true; this.RaisePropertyChanged(nameof(Title));
            _receiverAddress = result.StrisciateEndpoint; this.RaisePropertyChanged(nameof(ReceiverStatus));
        });
        if (!_isClosing && _loaded) await SetFocus(PositionFocus);
    }

    private async Task<bool> Run(CassaAction action, string message, Action<CassaResult> apply,
        string position = "", string card = "", int rateId = 0, int sheetId = 0, int listinoId = 0, int[]? wardrobe = null,
        int contoId = 0, bool pagato = false, long sospesoId = 0, Guid uscitaId = default, string versioneConto = "", int abbonamentoIngressoId = 0, int numeroIngressoAbbonamento = 0, bool rientro = false, int tipoFidelityId = 0, int personFidelityId = 0, int ingressoFidelityId = 0, bool usaCreditoFidelity = false)
    {
        if (_busy || _isClosing) return false;
        var ct = Token;
        _busy = true; IsWaiting = true; WaitMessage = message; InfoLabel = ""; Refresh();
        try
        {
            var result = await _repository.Execute(new CassaRequest {
                Action=action, PostazioneId=_postazioneId, Posizione=position,
                NumeroTessera=card, TariffaId=rateId, Note=Note, SchedaId=sheetId, ListinoId=listinoId,
                Grb1=wardrobe?[0] ?? 0, Grb2=wardrobe?[1] ?? 0, Grb3=wardrobe?[2] ?? 0, Grb4=wardrobe?[3] ?? 0,
                TipoFidelityId=tipoFidelityId,PersonId1=personFidelityId,IngressoFidelityId=ingressoFidelityId,UsaCreditoFidelity=usaCreditoFidelity,Rientro=rientro,AbbonamentoIngressoId=abbonamentoIngressoId,NumeroIngressoAbbonamento=numeroIngressoAbbonamento,ContoId=contoId, Pagato=pagato, SospesoId=sospesoId, UscitaId=uscitaId, VersioneConto=versioneConto
            }, ct);
            ct.ThrowIfCancellationRequested();
            if (_isClosing) return false;
            UpdateSubscriptionAvailability(result);
            apply(result);
            return true;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) when (ex is DatabaseHostException or InvalidOperationException)
        { if (!_isClosing) InfoLabel = ex.Message; return false; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            if (!_isClosing) InfoLabel = "Operazione cassa non riuscita. Verifica la connessione.";
            return false;
        }
        finally
        {
            // A response may finish after the view has been detached and its signals disposed.
            if (!_isClosing) { _busy=false; IsWaiting=false; WaitMessage=""; Refresh(); }
        }
    }

    private async Task OpenSheet()
    {
        if (string.IsNullOrWhiteSpace(Posizione)) { InfoLabel="Inserisci la posizione."; return; }
        SetSheet(null, []);
        if (await Run(CassaAction.OpenSheet, "APERTURA SCHEDA IN CORSO...", result => {
            SetSheet(result.Scheda, result.Conto, result.VociSpesa,result.Fidelity); SetPage(0);
        }, position: Posizione.Trim())) await SetFocus(PositionFocus);
    }
    private async Task ClearSheet()
    { SetSheet(null, []); Posizione=""; InfoLabel=""; await SetFocus(PositionFocus); }
    private async Task AddCharge(CassaVoceSpesa item)
    {
        if (Sheet is not { } sheet || !IsPostazione || !VociSpesa.Contains(item)) return;
        if (await Run(CassaAction.AddCharge, "REGISTRAZIONE ADDEBITO...", result => {
            SetSheet(result.Scheda, result.Conto, result.VociSpesa,result.Fidelity);
        }, position: sheet.Posizione, sheetId: sheet.Id, listinoId: item.ListinoId,usaCreditoFidelity:UsaCreditoFidelity)) await SetFocus(PositionFocus);
    }
    private void SetSheet(CassaScheda? sheet, CassaConto[] lines, CassaVoceSpesa[]? items = null,CassaFidelity? fidelity=null)
    {
        if (_sheet?.Id != sheet?.Id || sheet is null) {LoadGuardaroba(sheet);UsaCreditoFidelity=false;}ApplyFidelity(fidelity);
        _sheet=sheet; Conto.Clear(); foreach (var line in lines) Conto.Add(line);
        ApplyContoSort();
        VociSpesa.Clear(); foreach (var item in items ?? []) VociSpesa.Add(item);
        var selectedSector = SelectedSettoreSpesa?.Nome;
        SettoriSpesa = VociSpesa.GroupBy(v => v.Settore)
            .Select(g => new CassaSettoreSpesa(g.Key, g.ToArray())).ToArray();
        this.RaisePropertyChanged(nameof(HasVociSpesa)); this.RaisePropertyChanged(nameof(SettoriSpesa));
        SelectedSettoreSpesa = SettoriSpesa.FirstOrDefault(s => s.Nome == selectedSector) ?? SettoriSpesa.FirstOrDefault();
        foreach (var name in new[]{nameof(Sheet),nameof(HasSheet),nameof(Totale),nameof(DaPagare)}) this.RaisePropertyChanged(name);
    }
    private async Task SetPagamento(CassaConto line)
    {
        if (_busy || _paymentBusy || _isClosing || Sheet is not { } sheet || !IsPostazione || !Conto.Contains(line)) return;
        var ct=Token;
        var paymentSaved=false;
        _paymentBusy=true; this.RaisePropertyChanged(nameof(IsPaymentBusy)); InfoLabel="";
        try
        {
            var result=await _repository.Execute(new CassaRequest {
                Action=CassaAction.SetPagamento, PostazioneId=_postazioneId, Posizione=sheet.Posizione,
                SchedaId=sheet.Id, ContoId=line.Id, Pagato=!line.Pagato
            },ct);
            ct.ThrowIfCancellationRequested();
            if (_isClosing || Sheet?.Id!=sheet.Id) return;
            var saved=result.Conto.SingleOrDefault(x=>x.Id==line.Id)
                ?? throw new InvalidOperationException("Stato della voce non ricevuto. Riapri la scheda per verificarlo.");
            var current=Conto.FirstOrDefault(x=>x.Id==line.Id);
            if (current is null) return;
            current.Pagato=saved.Pagato;
            if (_contoSort==nameof(CassaConto.Stato)) ApplyContoSort();
            this.RaisePropertyChanged(nameof(DaPagare));
            paymentSaved=true;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is DatabaseHostException or InvalidOperationException)
        { if (!_isClosing && Sheet?.Id==sheet.Id) InfoLabel=ex.Message; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            if (!_isClosing && Sheet?.Id==sheet.Id) InfoLabel="Aggiornamento pagamento non riuscito. Verifica la connessione.";
        }
        finally
        {
            if (!_isClosing)
            {
                if (Sheet?.Id==sheet.Id) Conto.FirstOrDefault(x=>x.Id==line.Id)?.RefreshPagamento();
                _paymentBusy=false; this.RaisePropertyChanged(nameof(IsPaymentBusy));
            }
        }
        if (paymentSaved && !_isClosing && IsPostazione && Sheet?.Id==sheet.Id) await SetFocus(PositionFocus);
    }
    private void LoadGuardaroba(CassaScheda? sheet)
    {
        GuardarobaMessage="Vuoto o 0 = libero";
        static string Format(int value) => value == 0 ? "" : value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Grb1=Format(sheet?.Grb1 ?? 0); Grb2=Format(sheet?.Grb2 ?? 0);
        Grb3=Format(sheet?.Grb3 ?? 0); Grb4=Format(sheet?.Grb4 ?? 0);
    }
    private async Task StornaConto(CassaConto line)
    {
        if (_busy || _paymentBusy || _isClosing || !line.CanStornare || Sheet is not { } sheet || !IsPostazione || !Conto.Contains(line)) return;
        var ct=Token; var saved=false;
        _paymentBusy=true; this.RaisePropertyChanged(nameof(IsPaymentBusy)); InfoLabel="";
        try
        {
            var result=await _repository.Execute(new CassaRequest {
                Action=CassaAction.StornaConto,PostazioneId=_postazioneId,Posizione=sheet.Posizione,
                SchedaId=sheet.Id,ContoId=line.Id
            },ct);
            ct.ThrowIfCancellationRequested();
            if (_isClosing || Sheet?.Id!=sheet.Id) return;
            foreach (var row in result.Conto)
                if (Conto.FirstOrDefault(x=>x.Id==row.Id) is {} existing){existing.HasScontoFidelity=row.HasScontoFidelity;existing.IsStornata=row.IsStornata;}else Conto.Add(row);
            ApplyContoSort();
            this.RaisePropertyChanged(nameof(Totale)); this.RaisePropertyChanged(nameof(DaPagare));
            if(result.Scheda is {} updated)SetSheet(updated,result.Conto,VociSpesa.ToArray(),result.Fidelity);
            ApplyFidelity(result.Fidelity);saved=true;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is DatabaseHostException or InvalidOperationException)
        { if (!_isClosing && Sheet?.Id==sheet.Id) InfoLabel=ex.Message; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            if (!_isClosing && Sheet?.Id==sheet.Id) InfoLabel="Storno non riuscito. Verifica la connessione.";
        }
        finally
        {
            if (!_isClosing) { _paymentBusy=false; this.RaisePropertyChanged(nameof(IsPaymentBusy)); }
        }
        if (saved && !_isClosing && IsPostazione && Sheet?.Id==sheet.Id) await SetFocus(PositionFocus);
    }
    private void SortConto(string column)
    {
        if (!HasSheet || column is not (nameof(CassaConto.DataOra) or nameof(CassaConto.DescPostazione)
            or nameof(CassaConto.DescSettore) or nameof(CassaConto.VoiceDesc) or nameof(CassaConto.VoicePrice) or nameof(CassaConto.Stato))) return;
        _contoDescending = column == _contoSort && !_contoDescending;
        _contoSort = column;
        ApplyContoSort();
        foreach (var name in new[] { nameof(DataHeader), nameof(PostazioneHeader), nameof(SettoreHeader),
            nameof(DettaglioHeader), nameof(PrezzoHeader), nameof(StatoHeader) }) this.RaisePropertyChanged(name);
    }
    private void ApplyContoSort()
    {
        IOrderedEnumerable<CassaConto> Order<T>(Func<CassaConto,T> selector, IComparer<T>? comparer = null) =>
            _contoDescending ? Conto.OrderByDescending(selector, comparer) : Conto.OrderBy(selector, comparer);
        var text = StringComparer.CurrentCultureIgnoreCase;
        var ordered = _contoSort switch {
            nameof(CassaConto.DescPostazione) => Order(x => x.DescPostazione, text),
            nameof(CassaConto.DescSettore) => Order(x => x.DescSettore, text),
            nameof(CassaConto.VoiceDesc) => Order(x => x.VoiceDesc, text),
            nameof(CassaConto.VoicePrice) => Order(x => x.VoicePrice),
            nameof(CassaConto.Stato) => Order(x => x.Stato, text),
            _ => Order(x => x.DataOra)
        };
        var rows = ordered.ThenBy(x => x.DataOra).ThenBy(x => x.Id).ToArray();
        for (var i=0;i<rows.Length;i++)
        {
            var oldIndex=Conto.IndexOf(rows[i]);
            if (oldIndex!=i) Conto.Move(oldIndex,i);
        }
    }
    private async Task SaveGuardaroba()
    {
        if (_busy || _guardarobaBusy || _isClosing || Sheet is not { } sheet || !IsPostazione) return;
        var inputs=new[] { Grb1, Grb2, Grb3, Grb4 };
        var values=new int[4];
        for (var i=0;i<inputs.Length;i++)
        {
            if (string.IsNullOrWhiteSpace(inputs[i])) continue;
            if (!int.TryParse(inputs[i], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture,
                out values[i]) || values[i]<0)
            { GuardarobaMessage=$"Grb{i+1}: numero non valido."; return; }
        }
        var ct=Token; var saved=false;
        _guardarobaBusy=true; this.RaisePropertyChanged(nameof(IsGuardarobaBusy)); GuardarobaMessage="Salvataggio...";
        try
        {
            var result=await _repository.Execute(new CassaRequest {
                Action=CassaAction.SaveGuardaroba,PostazioneId=_postazioneId,Posizione=sheet.Posizione,SchedaId=sheet.Id,
                Grb1=values[0],Grb2=values[1],Grb3=values[2],Grb4=values[3]
            },ct);
            ct.ThrowIfCancellationRequested();
            if (_isClosing || Sheet?.Id!=sheet.Id) return;
            var updated=result.Scheda;
            if (updated is null || updated.Id!=sheet.Id) throw new InvalidOperationException("Guardaroba non ricevuto. Riapri la scheda.");
            Sheet.Grb1=updated.Grb1; Sheet.Grb2=updated.Grb2; Sheet.Grb3=updated.Grb3; Sheet.Grb4=updated.Grb4;
            LoadGuardaroba(Sheet); GuardarobaMessage="Guardaroba salvato."; saved=true;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is DatabaseHostException or InvalidOperationException)
        { if (!_isClosing && Sheet?.Id==sheet.Id) GuardarobaMessage=ex.Message; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            if (!_isClosing && Sheet?.Id==sheet.Id) GuardarobaMessage="Salvataggio non riuscito.";
        }
        finally
        {
            if (!_isClosing) { _guardarobaBusy=false; this.RaisePropertyChanged(nameof(IsGuardarobaBusy)); }
        }
        if (saved && !_isClosing && IsPostazione && Sheet?.Id==sheet.Id) await SetFocus(PositionFocus);
    }
    private async Task PrepareEntry()
    {
        if (await Run(CassaAction.PrepareEntry, "PREPARAZIONE INGRESSO IN CORSO...", result => {
            SetMember(null); NumeroTessera=""; EntryPosition=""; Note="";
            _occupied=result.PosizioniOccupate;
            _ingressiOrdinari=result.Ingressi;Ingressi.Clear(); foreach (var rate in result.Ingressi) Ingressi.Add(rate);
            SelectedIngresso=Ingressi.FirstOrDefault(); SetPage(1);
            if (Ingressi.Count == 0) InfoLabel="Nessun ingresso disponibile per questa postazione.";
        })) await SetFocus(CardFocus);
    }
    private async Task FindCard()
    {
        SetMember(null);
        if (string.IsNullOrWhiteSpace(NumeroTessera)) { InfoLabel="Inserisci il numero della tessera."; return; }
        var number = NumeroTessera.Trim();
        if (await Run(CassaAction.FindCard, "RICERCA SOCIO IN CORSO...", result => {
            _occupied=result.PosizioniOccupate; SetMember(result.Socio);
            foreach(var pending in result.Sospesi) SospesiIngresso.Add(pending);
            ApplyMemberEntryOptions(result);
            if (Member is null) InfoLabel="Socio non trovato.";
            else if (MemberInside) InfoLabel="Socio già all'interno, posizione " + Member.PosizioneOccupata + ".";
        }, card: number) && Member is not null && !MemberInside) await SetFocus(EntryPositionFocus);
    }
    private void SetMember(CassaSocio? member)
    {
        SospesiIngresso.Clear();
        ApplyMemberEntryOptions(new());
        _member=member;
        foreach (var name in new[]{nameof(Member),nameof(HasMember),nameof(MemberInside)}) this.RaisePropertyChanged(name);
        RefreshEntry();
    }
    private async Task RegisterEntry()
    {
        if (!string.IsNullOrEmpty(EntryWarning)) { InfoLabel=EntryWarning; return; }
        var position=EntryPosition.Trim();
        if (await Run(CassaAction.RegisterEntry, "REGISTRAZIONE INGRESSO IN CORSO...", result => {
            Posizione=result.Scheda?.Posizione ?? position; SetPage(0);
        }, position, NumeroTessera.Trim(), SelectedIngresso?.Id??0, abbonamentoIngressoId:_bollino?.Data.AbbonamentoId??0, numeroIngressoAbbonamento:_bollino?.Numero??0,rientro:HasRientro,ingressoFidelityId:_selectedFidelity?Fidelity?.Id??0:0,usaCreditoFidelity:UsaCreditoFidelity)) await OpenSheet();
    }
    private async Task LoadList()
    {
        await Run(CassaAction.ListInside, "CARICAMENTO SOCI ALL'INTERNO...", result => {
            _inside=result.Schede; ApplyFilter(); SetPage(2);
        });
    }
    private void ApplyFilter()
    {
        if (_isClosing) return;
        var id=SelectedScheda?.Id;
        var search=SearchText.Trim(); Schede.Clear();
        foreach (var sheet in _inside.Where(x => search.Length == 0
            || x.Posizione.Contains(search,StringComparison.OrdinalIgnoreCase)
            || x.Anagrafica.Contains(search,StringComparison.OrdinalIgnoreCase)
            || x.NumeroTessera.Contains(search,StringComparison.OrdinalIgnoreCase))) Schede.Add(sheet);
        SelectedScheda=Schede.FirstOrDefault(x => x.Id == id) ?? Schede.FirstOrDefault();
        this.RaisePropertyChanged(nameof(ResultText));
    }
    private async Task OpenSelected()
    {
        if (SelectedScheda is not { } sheet) { InfoLabel="Seleziona un socio dall'elenco."; return; }
        Posizione=sheet.Posizione; await OpenSheet();
    }
    private async Task OpenMemberSheet()
    {
        if (!MemberInside) return;
        Posizione=Member!.PosizioneOccupata; await OpenSheet();
    }
    private async Task ResetEntry()
    { SetMember(null); NumeroTessera=""; EntryPosition=""; Note=""; InfoLabel=""; await SetFocus(CardFocus); }
    private void SetPage(int page)
    {
        _page=page;
        foreach (var name in new[]{nameof(IsPostazione),nameof(IsEntry),nameof(IsList),nameof(IsAcquistoAbbonamento),nameof(IsSospesi),nameof(IsUscita),nameof(IsStoricoFidelity)}) this.RaisePropertyChanged(name);
        RefreshEntry(); RefreshAbbonamento(); RefreshUscita();
    }
    private async Task Back()
        {
        var page=IsSospesi?_sospesiBackPage:0;
        SetPage(page);InfoLabel="";
        if(page==0)await SetFocus(PositionFocus);
        else if(page==1)await SetFocus(CardFocus);
        else if(page==3)await SetFocus(AbbonamentoCardFocus);
    }
    private async Task BackToMenu()
    {
        if (_busy || _isClosing) return;
        _busy = true; IsWaiting = true; WaitMessage = "CHIUSURA RICEZIONE TESSERE..."; Refresh();
        await StopReceiver();
        if (_isClosing) return;
        _isClosing = true; _ready.Value = false; _entryReady.Value = false; _toMenu.OnNext(RxVoid.Default);
    }
    private Task StopReceiver() => _receiverStopTask ??= StopReceiverCore();
    private async Task StopReceiverCore()
    {
        try
        {
            await _repository.Execute(new CassaRequest { Action = CassaAction.StopStrisciate, PostazioneId = _postazioneId }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // The worker may already be gone at application shutdown; never touch disposed UI signals.
            System.Diagnostics.Debug.WriteLine("[CASSA] Chiusura ricevitore: " + ex.GetType().Name);
        }
    }
    protected override async Task OnEsc()
    {
        if (_busy) return;
        if (!IsPostazione) await Back();
        else if (HasSheet || !string.IsNullOrEmpty(Posizione)) await ClearSheet();
        else await BackToMenu();
    }
    private void Refresh()
    {
        if (_isClosing) return;
        _ready.Value=IsUiReady; RefreshEntry(); RefreshAbbonamento(); RefreshUscita();
        this.RaisePropertyChanged(nameof(IsBusy)); this.RaisePropertyChanged(nameof(IsUiReady));
    }
    private void RefreshEntry()
    {
        if (_isClosing) return;
        this.RaisePropertyChanged(nameof(EntryWarning));
        this.RaisePropertyChanged(nameof(CanSelectIngresso));
        this.RaisePropertyChanged(nameof(ShowIngressi));RefreshFidelity();
        _entryReady.Value=IsEntry && IsUiReady && string.IsNullOrEmpty(EntryWarning);
    }
    protected override void OnFinalDestruction()
    {
        _isClosing=true;
        _ = StopReceiver();
        foreach (var subscription in _subscriptions) subscription.Dispose(); _subscriptions.Clear();
        foreach (var command in _commands) command.Dispose();
        DisposeAbbonamento();
        DisposeSospesi();ApriStoricoFidelityCommand.Dispose();CercaStoricoFidelityCommand.Dispose();MovimentiFidelity.Clear();
        SelezionaBollinoCommand.Dispose();AbbonamentiIngresso.Clear();AttivaFidelityCommand.Dispose();IngressoFidelityCommand.Dispose();TipiFidelity.Clear();
        DisposeUscita();
        AddChargeCommand.Dispose(); VociSpesa.Clear();
        SetPagamentoCommand.Dispose();
        StornaContoCommand.Dispose();
        SortContoCommand.Dispose();
        SettoriSpesa = []; _selectedSettoreSpesa = null;
        _ready.Dispose(); _entryReady.Dispose(); _toMenu.Dispose();
        Conto.Clear(); Ingressi.Clear(); Schede.Clear(); _inside=[]; _occupied=[];
        _sheet=null; _member=null; _selected=null;
        base.OnFinalDestruction();
    }
}
