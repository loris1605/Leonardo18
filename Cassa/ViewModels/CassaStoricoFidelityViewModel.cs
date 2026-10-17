using System.Collections.ObjectModel;
using AppServices.Database;
using ReactiveUI;
using ReactiveUI.Primitives;
namespace Cassa.ViewModels;
public sealed partial class CassaViewModel
{
    private string _tesseraStoricoFidelity="",_riepilogoStoricoFidelity="Cerca un socio per vedere la sua Fidelity.";
    public bool IsStoricoFidelity=>_page==6;
    public string TesseraStoricoFidelity {get=>_tesseraStoricoFidelity;set {if(_tesseraStoricoFidelity==value)return;this.RaiseAndSetIfChanged(ref _tesseraStoricoFidelity,value);ClearStoricoFidelity();}}
    public string RiepilogoStoricoFidelity {get=>_riepilogoStoricoFidelity;private set=>this.RaiseAndSetIfChanged(ref _riepilogoStoricoFidelity,value);}
    public ObservableCollection<CassaMovimentoFidelity> MovimentiFidelity {get;}=[];
    public ReactiveCommand<RxVoid,RxVoid> ApriStoricoFidelityCommand {get;private set;}=null!;
    public ReactiveCommand<RxVoid,RxVoid> CercaStoricoFidelityCommand {get;private set;}=null!;
    private void InitializeStoricoFidelity()
    {
        ApriStoricoFidelityCommand=ReactiveCommand.CreateFromTask(async()=>{
            TesseraStoricoFidelity=Sheet?.NumeroTessera??"";ClearStoricoFidelity();InfoLabel="";SetPage(6);
            if(!string.IsNullOrWhiteSpace(TesseraStoricoFidelity))await CercaStoricoFidelity();
        },_ready);
        CercaStoricoFidelityCommand=ReactiveCommand.CreateFromTask(CercaStoricoFidelity,_ready);
        foreach(var command in new[]{ApriStoricoFidelityCommand,CercaStoricoFidelityCommand})
            _subscriptions.Add(command.ThrownExceptions.SubscribePrimitives(_=>{if(!_isClosing)InfoLabel="Impossibile caricare lo storico Fidelity.";}));
    }
    private void ClearStoricoFidelity(){MovimentiFidelity.Clear();RiepilogoStoricoFidelity="Cerca un socio per vedere la sua Fidelity.";}
    private async Task CercaStoricoFidelity()
    {
        ClearStoricoFidelity();var card=TesseraStoricoFidelity.Trim();
        if(card.Length==0){InfoLabel="Inserisci la tessera del socio.";return;}
        await Run(CassaAction.LoadFidelityHistory,"CARICAMENTO STORICO FIDELITY...",result=>{
            if(TesseraStoricoFidelity.Trim()!=card)return;
            var person=result.Socio?.Anagrafica??"";
            if(result.Fidelity is not {} f){RiepilogoStoricoFidelity=person+" · Fidelity mai attivata.";return;}
            RiepilogoStoricoFidelity=$"{person} · {f.Nome}\n{(f.Attiva?"Raccolta attiva":"Raccolta non attiva o scaduta")} · {f.Punti} timbri su {f.Traguardo} · Credito disponibile {f.CreditoSconto:N2} €\n"+
                (f.AttivataIl is {} start?$"Attivazione raccolta attuale: {start:dd/MM/yyyy HH:mm} · ":"")+f.Scadenza+
                (result.MovimentiFidelity.Length==0?"\nNessun movimento registrato.":"\nMovimenti di tutti i cicli, dal più recente.");
            foreach(var movement in result.MovimentiFidelity)MovimentiFidelity.Add(movement);
        },card:card);
    }
}
