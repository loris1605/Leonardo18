using AppServices.Database;
using ReactiveUI;
using ReactiveUI.Primitives;
namespace Cassa.ViewModels;
public sealed partial class CassaViewModel
{
    public bool CanEditSheet=>IsUiReady&&!_paymentBusy&&!_guardarobaBusy&&Sheet is {Blocco:false};
    public bool CanSetBloccoScheda=>IsUiReady&&IsPostazione&&HasSheet&&!_paymentBusy&&!_guardarobaBusy;
    public string BloccoSchedaLabel=>Sheet?.Blocco==true?"SBLOCCA SCHEDA":"BLOCCA SCHEDA";
    public ReactiveCommand<RxVoid,RxVoid> SetBloccoSchedaCommand {get;private set;}=null!;
    private void InitializeBloccoScheda()
    {
        SetBloccoSchedaCommand=ReactiveCommand.CreateFromTask(async()=>{
            if(!CanSetBloccoScheda||Sheet is not {} sheet)return;
            var target=!sheet.Blocco;
            if(await Run(CassaAction.SetBloccoScheda,"AGGIORNAMENTO BLOCCO SCHEDA...",result=>{
                SetSheet(result.Scheda,result.Conto,result.VociSpesa,result.Fidelity);LoadGuardaroba(result.Scheda);
            },position:sheet.Posizione,sheetId:sheet.Id,bloccoScheda:target))
                InfoLabel=target?"Scheda bloccata in tutte le postazioni. La consultazione rimane disponibile.":"Scheda sbloccata: inserimenti e modifiche nuovamente consentiti.";
        },_ready);
        _subscriptions.Add(SetBloccoSchedaCommand.ThrownExceptions.SubscribePrimitives(_=>{if(!_isClosing)InfoLabel="Impossibile aggiornare il blocco della scheda.";}));
    }
}
