using AppServices.Database;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
namespace Cassa.ViewModels;
public sealed partial class CassaViewModel
{
    private readonly StateSignal<bool> _payAllReady=new(false);
    public bool CanPagaTutto=>CanEditSheet&&IsPostazione&&Sheet?.CheckoutTime==DateTime.MaxValue&&Conto.Any(x=>!x.Pagato)&&DaPagare>=0;
    public ReactiveCommand<RxVoid,RxVoid> PagaTuttoCommand {get;private set;}=null!;
    private void InitializePagamentoTutto()
    {
        PagaTuttoCommand=ReactiveCommand.CreateFromTask(async()=>{
            if(!CanPagaTutto||Sheet is not {} sheet)return;
            var ids=Conto.Select(x=>x.Id).ToArray();var total=DaPagare;
            if(await Run(CassaAction.PagaTutto,"REGISTRAZIONE PAGAMENTO CONTO...",result=>{
                if(result.Scheda?.Id!=sheet.Id||!result.Conto.Select(x=>x.Id).ToHashSet().SetEquals(ids)||result.Conto.Any(x=>!x.Pagato))
                    throw new InvalidOperationException("Conferma del pagamento non ricevuta. Riapri la scheda per verificarne lo stato.");
                foreach(var saved in result.Conto)
                    if(Conto.FirstOrDefault(x=>x.Id==saved.Id) is {} current)current.Pagato=saved.Pagato;
                if(_contoSort==nameof(CassaConto.Stato))ApplyContoSort();
                this.RaisePropertyChanged(nameof(DaPagare));
                InfoLabel="Tutte le voci sono pagate. Scritture contabili registrate.";
            },position:sheet.Posizione,sheetId:sheet.Id,contoIds:ids,prezzoConfermato:total))
                await SetFocus(PositionFocus);
        },_payAllReady);
        _subscriptions.Add(PagaTuttoCommand.ThrownExceptions.SubscribePrimitives(_=>{if(!_isClosing)InfoLabel="Pagamento conto non riuscito.";}));
    }
    private void RefreshPagamentoTutto(){_payAllReady.Value=CanPagaTutto;this.RaisePropertyChanged(nameof(CanPagaTutto));}
}
