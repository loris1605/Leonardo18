using System.Collections.ObjectModel;
using AppServices.Database;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
namespace Cassa.ViewModels;

public sealed partial class CassaViewModel
{
    private readonly StateSignal<bool> _buyReady=new(false);
    private string _buyCard1="",_buyCard2="",_buyNote="",_purchaseError="";
    public string ErroreAcquisto
    {
        get=>_purchaseError;
        private set{this.RaiseAndSetIfChanged(ref _purchaseError,value);this.RaisePropertyChanged(nameof(HasErroreAcquisto));}
    }
    public bool HasErroreAcquisto=>!string.IsNullOrEmpty(ErroreAcquisto);
    private bool _buySecond,_buyPaid;
    private CassaSocio? _buyMember1,_buyMember2;
    private CassaTipoAbbonamento? _buyType;
    private Guid _purchaseId;
    private CassaAcquistoAbbonamento? _purchase;
    private readonly StateSignal<bool> _openBuyReady=new(false);
    private bool _abbonamentiDisponibili;
    public bool AcquistoAbbonamentiDisponibile => _abbonamentiDisponibili;
    private void UpdateSubscriptionAvailability(CassaResult result)
    {
        if(result.AbbonamentiDisponibili is not {} available)return;
        _abbonamentiDisponibili=available;
        this.RaisePropertyChanged(nameof(AcquistoAbbonamentiDisponibile));
        this.RaisePropertyChanged(nameof(AvvisoAcquistoAbbonamenti));
    }
    public string AvvisoAcquistoAbbonamenti => !AcquistoAbbonamentiDisponibile ? "Nessun abbonamento disponibile da vendere." : "Pagato: registra incasso e ricavo. Non pagato: conserva il credito in Sospesi fino al saldo.";
    public bool IsAcquistoAbbonamento=>_page==3;
    public bool HasAcquistoAbbonamento=>_purchase is not null;
    public bool CanEditAbbonamento=>!HasAcquistoAbbonamento;
    public ObservableCollection<CassaTipoAbbonamento> TipiAbbonamento {get;}=[];
    public CassaAcquistoAbbonamento? AcquistoAbbonamento=>_purchase;
    public CassaSocio? AbbonamentoSocio1=>_buyMember1;
    public CassaSocio? AbbonamentoSocio2=>_buyMember2;
    public bool HasAbbonamentoSocio1=>_buyMember1 is not null;
    public bool HasAbbonamentoSocio2=>_buyMember2 is not null;
    public string TesseraAbbonamento1
    {
        get=>_buyCard1;
        set{if(_isClosing||_buyCard1==value)return;this.RaiseAndSetIfChanged(ref _buyCard1,value);_buyMember1=null;RefreshAbbonamento();}
    }
    public string TesseraAbbonamento2
    {
        get=>_buyCard2;
        set{if(_isClosing||_buyCard2==value)return;this.RaiseAndSetIfChanged(ref _buyCard2,value);_buyMember2=null;RefreshAbbonamento();}
    }
    public bool SecondoSocioAbbonamento
    {
        get=>_buySecond;
        set{if(_isClosing)return;this.RaiseAndSetIfChanged(ref _buySecond,value);if(!value){TesseraAbbonamento2="";_buyMember2=null;}RefreshAbbonamento();}
    }
    public bool AbbonamentoPagato{get=>_buyPaid;set{if(!_isClosing)this.RaiseAndSetIfChanged(ref _buyPaid,value);}}
    public string NoteAbbonamento{get=>_buyNote;set{if(!_isClosing)this.RaiseAndSetIfChanged(ref _buyNote,value);}}
    public CassaTipoAbbonamento? TipoAbbonamentoSelezionato
    {
        get=>_buyType;
        set{if(_isClosing)return;this.RaiseAndSetIfChanged(ref _buyType,value);RefreshAbbonamento();}
    }
    public string RiepilogoTipoAbbonamento
    {
        get
        {
            if(_buyType is not {} t)return "Seleziona il tipo di abbonamento.";
            string end;try{end=DateTime.Today.AddDays(t.DurataGiorni).ToString("dd/MM/yyyy");}catch(ArgumentOutOfRangeException){end="Durata non valida";}
            return $"{t.NumeroIngressi} ingressi · {t.DurataGiorni} giorni\nDal {DateTime.Today:dd/MM/yyyy} al {end}\nPrezzo totale: {t.Prezzo:0.00} €";
        }
    }
    public string AbbonamentoWarning
    {
        get
        {
            if(_buyType is null)return "Seleziona un tipo di abbonamento attivo.";
            if(_buyMember1 is null || _buyMember1.NumeroTessera!=TesseraAbbonamento1.Trim())return "Identifica la tessera del primo socio.";
            if(!ValidPurchaseMember(_buyMember1))return "La tessera del primo socio è scaduta o non abilitata.";
            if(SecondoSocioAbbonamento){
                if(_buyMember2 is null || _buyMember2.NumeroTessera!=TesseraAbbonamento2.Trim())return "Identifica la tessera del secondo socio.";
                if(!ValidPurchaseMember(_buyMember2))return "La tessera del secondo socio è scaduta o non abilitata.";
                if(_buyMember1.PersonId==_buyMember2.PersonId)return "Le due tessere devono appartenere a soci distinti.";
            }
            return "";
        }
    }
    private static bool ValidPurchaseMember(CassaSocio member)=>member.PersonId>0&&!member.Blocco&&
        DateTime.TryParseExact(member.ScadenzaTessera.ToString("D8"),"yyyyMMdd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var expiry)&&expiry>=DateTime.Today;
    public ReactiveCommand<RxVoid,RxVoid> AcquistaAbbonamentoCommand {get;private set;}=null!;
    public ReactiveCommand<RxVoid,RxVoid> AggiornaTipiAbbonamentoCommand {get;private set;}=null!;
    public ReactiveCommand<string,RxVoid> IdentificaTesseraAbbonamentoCommand {get;private set;}=null!;
    public ReactiveCommand<RxVoid,RxVoid> ConfermaAbbonamentoCommand {get;private set;}=null!;
    public Interaction<RxVoid,RxVoid> AbbonamentoCardFocus {get;}=new();
    private void InitializeAbbonamento()
    {
        AcquistaAbbonamentoCommand=ReactiveCommand.CreateFromTask(PrepareAbbonamento,_openBuyReady);
        AggiornaTipiAbbonamentoCommand=ReactiveCommand.CreateFromTask(ReloadTipiAbbonamento,_ready);
        _subscriptions.Add(AggiornaTipiAbbonamentoCommand.ThrownExceptions.SubscribePrimitives(ex=>{if(!_isClosing)InfoLabel="Impossibile aggiornare i tipi di abbonamento.";}));
        IdentificaTesseraAbbonamentoCommand=ReactiveCommand.CreateFromTask<string>(FindAbbonamentoCard,_ready);
        ConfermaAbbonamentoCommand=ReactiveCommand.CreateFromTask(BuyAbbonamento,_buyReady);
        _subscriptions.Add(AcquistaAbbonamentoCommand.ThrownExceptions.SubscribePrimitives(ex=>{if(!_isClosing)InfoLabel="Impossibile aprire gli abbonamenti.";}));
        _subscriptions.Add(IdentificaTesseraAbbonamentoCommand.ThrownExceptions.SubscribePrimitives(ex=>{if(!_isClosing)InfoLabel="Impossibile identificare la tessera.";}));
        _subscriptions.Add(ConfermaAbbonamentoCommand.ThrownExceptions.SubscribePrimitives(ex=>{if(!_isClosing)InfoLabel="Acquisto non riuscito.";}));
    }
    private async Task PrepareAbbonamento()
    {
        if(_busy||_paymentBusy||_guardarobaBusy||_isClosing)return;
        if(await Run(CassaAction.PrepareAbbonamento,"CARICAMENTO ABBONAMENTI...",result=>{
            TipiAbbonamento.Clear();foreach(var t in result.TipiAbbonamento)TipiAbbonamento.Add(t);
            ErroreAcquisto="";_purchase=null;_purchaseId=Guid.NewGuid();_buyMember1=null;_buyMember2=null;
            TesseraAbbonamento1=Sheet?.NumeroTessera??"";TesseraAbbonamento2="";SecondoSocioAbbonamento=false;
            NoteAbbonamento="";AbbonamentoPagato=false;TipoAbbonamentoSelezionato=TipiAbbonamento.FirstOrDefault();
            SetPage(3);RefreshAbbonamento();
            if(TipiAbbonamento.Count==0)InfoLabel="Nessun abbonamento attivo disponibile. Configura i tipi di abbonamento in Servizi.";
        }))await SetFocus(AbbonamentoCardFocus);
    }
    private async Task ReloadTipiAbbonamento()
    {
        if(!IsAcquistoAbbonamento||HasAcquistoAbbonamento)return;
        ErroreAcquisto="";var selectedId=TipoAbbonamentoSelezionato?.Id;
        await Run(CassaAction.PrepareAbbonamento,"AGGIORNAMENTO ABBONAMENTI...",result=>{
            TipiAbbonamento.Clear();foreach(var type in result.TipiAbbonamento)TipiAbbonamento.Add(type);
            TipoAbbonamentoSelezionato=TipiAbbonamento.FirstOrDefault(x=>x.Id==selectedId)??TipiAbbonamento.FirstOrDefault();
            if(TipiAbbonamento.Count==0)InfoLabel="Nessun abbonamento attivo disponibile.";
        });
    }
    private async Task FindAbbonamentoCard(string slot)
    {
        if(!IsAcquistoAbbonamento||HasAcquistoAbbonamento || (slot!="1"&&slot!="2") || (slot=="2"&&!SecondoSocioAbbonamento))return;
        var card=(slot=="1"?TesseraAbbonamento1:TesseraAbbonamento2).Trim();
        if(card.Length==0){InfoLabel="Inserisci o striscia la tessera da identificare.";return;}
        await Run(CassaAction.FindCard,"IDENTIFICAZIONE SOCIO...",result=>{
            if(slot=="1"&&TesseraAbbonamento1.Trim()==card)_buyMember1=result.Socio;
            if(slot=="2"&&TesseraAbbonamento2.Trim()==card)_buyMember2=result.Socio;
            if(result.Socio is null)InfoLabel="Tessera non trovata. Verifica il numero o acquisisci la tessera.";
            RefreshAbbonamento();
        },card:card);
    }
    private async Task BuyAbbonamento()
    {
        if(_busy||_isClosing||!IsAcquistoAbbonamento||HasAcquistoAbbonamento||AbbonamentoWarning.Length>0||_buyType is null)return;
        ErroreAcquisto="";var ct=Token;_busy=true;IsWaiting=true;WaitMessage="REGISTRAZIONE ACQUISTO ABBONAMENTO...";InfoLabel="";Refresh();
        try{
            var result=await _repository.Execute(new(){Action=CassaAction.BuyAbbonamento,PostazioneId=_postazioneId,TipoAbbonamentoId=_buyType.Id,
                NumeroTessera=TesseraAbbonamento1.Trim(),NumeroTessera2=SecondoSocioAbbonamento?TesseraAbbonamento2.Trim():"",
                PersonId1=_buyMember1!.PersonId,PersonId2=SecondoSocioAbbonamento?_buyMember2!.PersonId:0,
                PrezzoConfermato=_buyType.Prezzo,IngressiConfermati=_buyType.NumeroIngressi,DurataConfermata=_buyType.DurataGiorni,AcquistoId=_purchaseId,Note=NoteAbbonamento,Pagato=AbbonamentoPagato},ct);
            ct.ThrowIfCancellationRequested();if(_isClosing)return;
            _purchase=result.AcquistoAbbonamento??throw new InvalidOperationException("Conferma dell'acquisto non ricevuta. Riprova senza avviare un nuovo acquisto.");
            InfoLabel=_purchase.Prezzo==0?"Abbonamento gratuito registrato.":_purchase.Pagato?"Abbonamento acquistato: pagamento registrato.":"Abbonamento acquistato: credito conservato in Sospesi.";RefreshAbbonamento();
        }
        catch(OperationCanceledException){}
        catch(Exception ex) when(ex is DatabaseHostException or InvalidOperationException){if(!_isClosing){ErroreAcquisto=ex.Message;InfoLabel="";}}
        catch(Exception ex){System.Diagnostics.Debug.WriteLine(ex);if(!_isClosing){ErroreAcquisto="Acquisto non confermato. Riprova con gli stessi dati per verificarne l'esito.";InfoLabel="";}}
        finally{if(!_isClosing){_busy=false;IsWaiting=false;WaitMessage="";Refresh();}}
    }
    private void RefreshAbbonamento()
    {
        if(_isClosing)return;
        foreach(var name in new[]{nameof(AbbonamentoSocio1),nameof(AbbonamentoSocio2),nameof(HasAbbonamentoSocio1),nameof(HasAbbonamentoSocio2),nameof(AbbonamentoWarning),nameof(RiepilogoTipoAbbonamento),nameof(AcquistoAbbonamento),nameof(HasAcquistoAbbonamento),nameof(CanEditAbbonamento)})this.RaisePropertyChanged(name);
        _openBuyReady.Value=AcquistoAbbonamentiDisponibile&&IsUiReady;
        _buyReady.Value=AcquistoAbbonamentiDisponibile&&IsAcquistoAbbonamento&&IsUiReady&&!HasAcquistoAbbonamento&&AbbonamentoWarning.Length==0;
    }
    private void DisposeAbbonamento()
    {
        AggiornaTipiAbbonamentoCommand.Dispose();AcquistaAbbonamentoCommand.Dispose();IdentificaTesseraAbbonamentoCommand.Dispose();ConfermaAbbonamentoCommand.Dispose();_buyReady.Dispose();_openBuyReady.Dispose();
        TipiAbbonamento.Clear();_buyMember1=null;_buyMember2=null;_buyType=null;_purchase=null;
    }
}