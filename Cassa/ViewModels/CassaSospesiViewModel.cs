using System.Collections.ObjectModel;
using AppServices.Database;
using ReactiveUI;
using ReactiveUI.Primitives;
namespace Cassa.ViewModels;

public sealed partial class CassaViewModel
{
    private int _sospesiBackPage;
    private string _sospesiCard="";
    public bool IsSospesi=>_page==4;
    public Interaction<RxVoid,RxVoid> SospesiCardFocus {get;}=new();
    public string TesseraFiltroSospesi{get=>_sospesiCard;set=>this.RaiseAndSetIfChanged(ref _sospesiCard,value);}
    public ObservableCollection<CassaSospeso> Sospesi {get;}=[];
    public string RiepilogoSospesi=>$"{Sospesi.Count} sospesi · Da incassare: {Sospesi.Where(x=>x.Tipo=="Credito").Sum(x=>x.Importo):0.00} € · Da pagare: {Sospesi.Where(x=>x.Tipo=="Debito").Sum(x=>x.Importo):0.00} €";
    public ReactiveCommand<RxVoid,RxVoid> ApriSospesiCommand{get;private set;}=null!;
    public ReactiveCommand<string,RxVoid> ApriSospesiSocioCommand{get;private set;}=null!;
    public ReactiveCommand<RxVoid,RxVoid> CercaSospesiCommand{get;private set;}=null!;
    public ReactiveCommand<RxVoid,RxVoid> TuttiSospesiCommand{get;private set;}=null!;
    public ReactiveCommand<CassaSospeso,RxVoid> IncassaSospesoCommand{get;private set;}=null!;

    private void InitializeSospesi()
    {
        ApriSospesiCommand=ReactiveCommand.CreateFromTask(()=>OpenPending(Sheet?.NumeroTessera??""),_ready);
        ApriSospesiSocioCommand=ReactiveCommand.CreateFromTask<string>(OpenPending,_ready);
        CercaSospesiCommand=ReactiveCommand.CreateFromTask(ReloadPending,_ready);
        TuttiSospesiCommand=ReactiveCommand.CreateFromTask(async()=>{TesseraFiltroSospesi="";await ReloadPending();},_ready);
        IncassaSospesoCommand=ReactiveCommand.CreateFromTask<CassaSospeso>(SettlePending,_ready);
        _subscriptions.Add(ApriSospesiCommand.ThrownExceptions.SubscribePrimitives(_=>{if(!_isClosing)InfoLabel="Impossibile aprire i sospesi.";}));
        _subscriptions.Add(ApriSospesiSocioCommand.ThrownExceptions.SubscribePrimitives(_=>{if(!_isClosing)InfoLabel="Impossibile aprire i sospesi del socio.";}));
        _subscriptions.Add(CercaSospesiCommand.ThrownExceptions.SubscribePrimitives(_=>{if(!_isClosing)InfoLabel="Impossibile cercare i sospesi.";}));
        _subscriptions.Add(TuttiSospesiCommand.ThrownExceptions.SubscribePrimitives(_=>{if(!_isClosing)InfoLabel="Impossibile caricare i sospesi.";}));
        _subscriptions.Add(IncassaSospesoCommand.ThrownExceptions.SubscribePrimitives(_=>{if(!_isClosing)InfoLabel="Incasso non riuscito.";}));
    }
    private async Task OpenPending(string card)
    {
        if(IsEntry||_busy||_paymentBusy||_guardarobaBusy||_isClosing)return;
        if(!IsSospesi)_sospesiBackPage=_page;
        TesseraFiltroSospesi=card;
        if(await Run(CassaAction.LoadSospesi,"CARICAMENTO SOSPESI...",result=>{ApplyPending(result);SetPage(4);},card:card.Trim()))
            await SetFocus(SospesiCardFocus);
    }
    private async Task ReloadPending()
    {
        if(!IsSospesi)return;
        await Run(CassaAction.LoadSospesi,"RICERCA SOSPESI...",ApplyPending,card:TesseraFiltroSospesi.Trim());
    }
    private async Task SettlePending(CassaSospeso item)
    {
        var entry=IsEntry;
        if(!(entry?SospesiIngresso.Contains(item):IsSospesi&&Sospesi.Contains(item))||!item.CanIncassare)return;
        var member=Member;
        var card=entry?member?.NumeroTessera??"":TesseraFiltroSospesi.Trim();
        if(await Run(CassaAction.SaldaSospeso,"REGISTRAZIONE INCASSO SOSPESO...",result=>{
            if(result.SospesoSaldatoId!=item.Id)throw new InvalidOperationException("Conferma dell'incasso non ricevuta. Riprova per verificarne l'esito.");
            if(entry)
            {
                var updated=result.Socio?.PersonId==member?.PersonId?result.Socio:
                    result.SociConSospesi.FirstOrDefault(x=>x.PersonId==member?.PersonId);
                if(member is null||updated is null)throw new InvalidOperationException("Stato dei sospesi non ricevuto. Identifica nuovamente il socio per verificarlo.");
                member.SospesiAperti=updated.SospesiAperti;member.TotaleSospesi=updated.TotaleSospesi;
                this.RaisePropertyChanged(nameof(Member));
                SospesiIngresso.Clear();foreach(var pending in result.Sospesi)SospesiIngresso.Add(pending);
                ApplyMemberEntryOptions(result);
                RefreshEntry();
            }
            else ApplyPending(result);
        },card:card,sospesoId:item.Id))
            InfoLabel="Sospeso saldato: pagamento registrato in contabilità.";
    }
    private void ApplyPending(CassaResult result)
    {
        Sospesi.Clear();foreach(var item in result.Sospesi)Sospesi.Add(item);
        this.RaisePropertyChanged(nameof(RiepilogoSospesi));
        (int Count,decimal Total)? Summary(int personId)
        {
            if(result.SociConSospesi.FirstOrDefault(x=>x.PersonId==personId) is {} known)return(known.SospesiAperti,known.TotaleSospesi);
            if(TesseraFiltroSospesi.Trim().Length>0&&result.Socio?.PersonId!=personId)return null;
            var items=result.Sospesi.Where(x=>x.Tipo=="Credito"&&(x.PersonId==personId||x.PersonId2==personId)).ToArray();
            return(items.Length,items.Sum(x=>x.Importo));
        }
        if(_sheet is not null&&Summary(_sheet.PersonId) is {} sheet)
        {_sheet.SospesiAperti=sheet.Count;_sheet.TotaleSospesi=sheet.Total;this.RaisePropertyChanged(nameof(Sheet));}
        foreach(var member in new[]{_member,_buyMember1,_buyMember2})
            if(member is not null&&Summary(member.PersonId) is {} summary){member.SospesiAperti=summary.Count;member.TotaleSospesi=summary.Total;}
        this.RaisePropertyChanged(nameof(Member));RefreshEntry();RefreshAbbonamento();
    }
    private void DisposeSospesi()
    {
        ApriSospesiCommand.Dispose();ApriSospesiSocioCommand.Dispose();CercaSospesiCommand.Dispose();TuttiSospesiCommand.Dispose();IncassaSospesoCommand.Dispose();Sospesi.Clear();
    }
}
