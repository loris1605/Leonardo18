using System.Collections.ObjectModel;
using AppServices.Database;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
namespace Cassa.ViewModels;
public sealed partial class CassaViewModel
{
    private readonly StateSignal<bool> _exitPayReady=new(false),_exitDebtReady=new(false),_openExitReady=new(false);
    private string _exitPosition="",_exitCard="";
    private CassaScheda? _exitSheet;
    private CassaUscita? _exit;
    private Guid _exitId;
    public bool CanEsceSocio=>IsUiReady&&IsPostazione&&!_paymentBusy&&!_guardarobaBusy&&Sheet is {Blocco:false,CheckoutTime:var end}&&end==DateTime.MaxValue;
    public bool IsUscita=>_page==5;
    public CassaScheda? SocioUscita=>_exitSheet;
    public bool HasSocioUscita=>_exitSheet!=null;
    public bool UscitaConfermata=>_exit?.Confermata==true;
    public ObservableCollection<CassaConto> ContoUscita {get;}=[];
    public string PosizioneUscita
    {
        get=>_exitPosition;
        set{if(_isClosing||_exitPosition==value)return;this.RaiseAndSetIfChanged(ref _exitPosition,value);if(value.Length>0){_exitCard="";this.RaisePropertyChanged(nameof(TesseraUscita));}ResetExit();}
    }
    public string TesseraUscita
    {
        get=>_exitCard;
        set{if(_isClosing||_exitCard==value)return;this.RaiseAndSetIfChanged(ref _exitCard,value);if(value.Length>0){_exitPosition="";this.RaisePropertyChanged(nameof(PosizioneUscita));}ResetExit();}
    }
    public decimal TotaleUscita=>ContoUscita.Sum(x=>x.VoicePrice);
    public decimal PagatoUscita=>ContoUscita.Where(x=>x.Pagato).Sum(x=>x.VoicePrice);
    public decimal DaPagareUscita=>_exit?.DaPagare??0;
    public string GuardarobaUscita=>_exitSheet is {} s?$"Guardaroba: Grb1 {s.Grb1} · Grb2 {s.Grb2} · Grb3 {s.Grb3} · Grb4 {s.Grb4}":"";
    public string AvvisoUscita=>UscitaConfermata?$"USCITA REGISTRATA · {_exit!.DataUscita:dd/MM/yyyy HH:mm} · Incassato {_exit.Incassato:N2} € · Lasciato in sospeso {_exit.Sospeso:N2} €":
        _exitSheet?.Blocco==true?"Scheda bloccata: verifica la situazione nella Scheda.":DaPagareUscita<0?"È previsto un rimborso: regolarizza il conto nella Scheda.":HasSocioUscita?"Verifica il conto e restituisci gli oggetti del guardaroba prima di confermare.":"Cerca il socio tramite posizione oppure tessera.";
    public string IncassaUscitaLabel=>DaPagareUscita==0?"REGISTRA USCITA":"INCASSA E REGISTRA USCITA";
    public Interaction<RxVoid,RxVoid> UscitaFocus {get;}=new();
    public ReactiveCommand<RxVoid,RxVoid> EsceSocioCommand {get;private set;}=null!;
    public ReactiveCommand<RxVoid,RxVoid> CercaUscitaCommand {get;private set;}=null!;
    public ReactiveCommand<RxVoid,RxVoid> IncassaUscitaCommand {get;private set;}=null!;
    public ReactiveCommand<RxVoid,RxVoid> SospendiUscitaCommand {get;private set;}=null!;
    private void InitializeUscita()
    {
        EsceSocioCommand=ReactiveCommand.CreateFromTask(OpenExit,_openExitReady);
        CercaUscitaCommand=ReactiveCommand.CreateFromTask(FindExit,_ready);
        IncassaUscitaCommand=ReactiveCommand.CreateFromTask(()=>SaveExit(true),_exitPayReady);
        SospendiUscitaCommand=ReactiveCommand.CreateFromTask(()=>SaveExit(false),_exitDebtReady);
        foreach(var command in new[]{EsceSocioCommand,CercaUscitaCommand,IncassaUscitaCommand,SospendiUscitaCommand})
            _subscriptions.Add(command.ThrownExceptions.SubscribePrimitives(_=>{if(!_isClosing)InfoLabel="Operazione di uscita non riuscita.";}));
    }
    private void ResetExit(){_exitSheet=null;_exit=null;ContoUscita.Clear();_exitId=Guid.Empty;RefreshUscita();}
    private async Task OpenExit()
    {
        if(!CanEsceSocio||Sheet is not {} sheet)return;
        _exitPosition=sheet.Posizione;_exitCard="";
        this.RaisePropertyChanged(nameof(PosizioneUscita));this.RaisePropertyChanged(nameof(TesseraUscita));ResetExit();SetPage(5);
        if(_exitPosition.Length>0)await FindExit();await SetFocus(UscitaFocus);
    }
    private async Task FindExit()
    {
        if(!IsUscita||_busy)return;
        ResetExit();
        await Run(CassaAction.PrepareUscita,"CARICAMENTO CONTO IN USCITA...",result=>{
            _exitSheet=result.Scheda??throw new InvalidOperationException("Scheda in uscita non ricevuta.");
            _exit=result.Uscita??throw new InvalidOperationException("Riepilogo uscita non ricevuto.");
            _exitId=Guid.NewGuid();ContoUscita.Clear();foreach(var row in result.Conto)ContoUscita.Add(row);RefreshUscita();
        },position:PosizioneUscita.Trim(),card:TesseraUscita.Trim());
    }
    private async Task SaveExit(bool collect)
    {
        if(!IsUscita||_exitSheet is null||_exit is null||UscitaConfermata||_exitId==Guid.Empty)return;
        var id=_exitSheet.Id;var key=_exitId;var version=_exit.VersioneConto;
        if(await Run(CassaAction.RegisterUscita,"REGISTRAZIONE USCITA...",result=>{
            if(result.Uscita is not {Confermata:true} receipt||receipt.IdOperazione!=key||result.Scheda?.Id!=id)
                throw new InvalidOperationException("Conferma dell'uscita non ricevuta. Riprova con gli stessi dati per verificarne l'esito.");
            _exit=receipt;_exitSheet=result.Scheda;ContoUscita.Clear();foreach(var row in result.Conto)ContoUscita.Add(row);
            SetSheet(null,[]);Posizione="";_inside=_inside.Where(x=>x.Id!=id).ToArray();ApplyFilter();RefreshUscita();
        },sheetId:id,pagato:collect,uscitaId:key,versioneConto:version))InfoLabel="Uscita registrata. La posizione è libera.";
    }
    private void RefreshUscita()
    {
        if(_isClosing)return;
        RefreshPagamentoTutto();
        _openExitReady.Value=CanEsceSocio;this.RaisePropertyChanged(nameof(CanEsceSocio));
        foreach(var name in new[]{nameof(SocioUscita),nameof(HasSocioUscita),nameof(UscitaConfermata),nameof(TotaleUscita),nameof(PagatoUscita),nameof(DaPagareUscita),nameof(GuardarobaUscita),nameof(AvvisoUscita),nameof(IncassaUscitaLabel)})this.RaisePropertyChanged(name);
        var ready=IsUscita&&IsUiReady&&HasSocioUscita&&!UscitaConfermata&&_exitSheet?.Blocco==false&&DaPagareUscita>=0;
        _exitPayReady.Value=ready;_exitDebtReady.Value=ready&&DaPagareUscita>0;
    }
    private void DisposeUscita(){EsceSocioCommand.Dispose();CercaUscitaCommand.Dispose();IncassaUscitaCommand.Dispose();SospendiUscitaCommand.Dispose();_openExitReady.Dispose();_exitPayReady.Dispose();_exitDebtReady.Dispose();ContoUscita.Clear();}
}
