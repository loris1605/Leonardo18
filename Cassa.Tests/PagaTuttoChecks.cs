using System.Text.Json;
using AppServices.Database;
using Avalonia.Controls;
using Avalonia.Threading;
using Cassa.Core.Repository;
using Cassa.ViewModels;
using Cassa.Views;
using Leonardo18.DatabaseHost.Cassa;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Models.Context;
using Models.Contabilita;
using Models.Tables;
using ReactiveUI;
using ReactiveUI.Primitives;

internal static class PagaTuttoChecks
{
    public static int Count;
    private static void Check(bool ok,string text){if(!ok)throw new Exception(text);Count++;}
    private static async Task Reject(Func<Task> action,string text){try{await action();}catch(InvalidOperationException e)when(e.Message.Contains(text,StringComparison.OrdinalIgnoreCase)){Count++;return;}throw new Exception("Operazione non rifiutata: "+text);}
    public static async Task Database()
    {
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        var options=new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using(var db=new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Postazioni.Add(new(){Id=100,Nome="Cassa",TipoPostazioneId=2,TipoRientroId=-1});
            db.Operatori.AddRange(new(){Id=100,Nome="Cassiere",PersonId=-1,Abilitato=true},new(){Id=101,Nome="Senza permesso",PersonId=-1,Abilitato=true});
            db.Permessi.Add(new(){OperatoreId=100,PostazioneId=100});db.Giornate.Add(new(){Id=100,Aperta=true,DataInizio=DateTime.Today,DataFine=DateTime.MaxValue});
            db.People.Add(new(){Id=100,FirstName="Mario",SurName="Rossi",UniqueParam="PAY100",Natoil=19900101,Soci=[new(){NumeroSocio="S100",Tessere=[new(){NumeroTessera="T100",Scadenza=20991231,Abilitato=true}]}]});
            db.Schede.Add(new(){Id=100,PersonId=100,Posizione="P100",NumeroTessera="T100",Nome="MARIO",Cognome="ROSSI",CheckoutTime=DateTime.MaxValue});
            db.Settori.Add(new(){Id=100,Nome="Bar",TipoSettoreId=-2});db.Reparti.Add(new(){PostazioneId=100,SettoreId=100});
            db.Tariffe.AddRange(new(){Id=100,Nome="Acqua",Label="Acqua",Prezzo=2.5m},new(){Id=101,Nome="Omaggio",Label="Omaggio",Prezzo=0});
            db.Listini.AddRange(new(){Id=100,TariffaId=100,SettoreId=100},new(){Id=101,TariffaId=101,SettoreId=100});
            db.SchedeConto.Add(new(){SchedaId=100,VoiceDesc="Ingresso già pagato",IsIngresso=true,VoicePrice=10,Pagato=true,DescSettore="Ingresso",DataOra=DateTime.Now});
            await db.SaveChangesAsync();
        }
        var repo=new CassaDatabaseRepository(()=>new AppDbContext(options));
        CassaRequest Req(CassaAction action,int id=0)=>new(){Action=action,PostazioneId=100,Posizione="P100",SchedaId=100,ContoId=id,ListinoId=100};
        async Task<CassaRequest> Quote(){var result=await repo.Execute(100,Req(CassaAction.OpenSheet));var q=Req(CassaAction.PagaTutto);q.ContoIds=result.Conto.Select(x=>x.Id).ToArray();q.PrezzoConfermato=result.Conto.Where(x=>!x.Pagato).Sum(x=>x.VoicePrice);return q;}
        async Task<int> Add(){var result=await repo.Execute(100,Req(CassaAction.AddCharge));return result.Conto.Max(x=>x.Id);}
        await Add();await Add();var paid=await Add();var pay=Req(CassaAction.SetPagamento,paid);pay.Pagato=true;await repo.Execute(100,pay);
        var canceled=await Add();await repo.Execute(100,Req(CassaAction.StornaConto,canceled));
        var free=Req(CassaAction.AddCharge);free.ListinoId=101;await repo.Execute(100,free);
        await using(var db=new AppDbContext(options))
        {
            db.SchedeConto.AddRange(new(){SchedaId=100,DescSettore="Bar",DescPostazione="Vecchia cassa",VoiceDesc="Pregresso",VoicePrice=7,DataOra=DateTime.Now},
                new(){SchedaId=100,DescSettore="Bar",DescPostazione="Vecchia cassa",VoiceDesc="Annullato",VoicePrice=3,DataOra=DateTime.Now},
                new(){SchedaId=100,DescSettore="Bar",DescPostazione="Vecchia cassa",VoiceDesc="STORNO - Annullato",VoicePrice=-3,DataOra=DateTime.Now});
            await db.SaveChangesAsync();
        }
        var quote=await Quote();Check(quote.PrezzoConfermato==12,"Riepilogo esclude pagati e annulla storni");
        await Reject(()=>repo.Execute(101,quote),"permesso");
        var wrong=await Quote();wrong.SchedaId=999;await Reject(()=>repo.Execute(100,wrong),"cambiata");
        var result=await repo.Execute(100,quote);Check(result.Conto.All(x=>x.Pagato)&&result.Scheda?.CheckoutTime==DateTime.MaxValue,"Paga tutte le righe senza chiudere la scheda");
        int movements;
        await using(var db=new AppDbContext(options))
        {
            var journals=await db.ContabilitaMovimenti.Include(x=>x.Righe).ToArrayAsync();movements=journals.Length;
            foreach(var movement in journals)movement.VerificaQuadratura();
            Check(journals.All(x=>x.Stato==StatoMovimentoContabile.Confermato),"Scritture quadrate e confermate");
            var cash=(await db.ContabilitaRighe.Where(x=>x.Conto.Codice=="100").ToArrayAsync()).Sum(x=>x.Dare-x.Avere);
            Check(cash==14.5m,"Cassa incassa solo 12 euro nuovi oltre ai 2,50 già pagati");
            var credit=(await db.ContabilitaRighe.Where(x=>x.Conto.Codice=="120").ToArrayAsync()).Sum(x=>x.Dare-x.Avere);
            Check(credit==0,"Partite saldate senza residui");
            Check(await db.SchedeConto.CountAsync()==quote.ContoIds.Length,"Nessuna duplicazione di righe operative");
        }
        await repo.Execute(100,quote);
        await using(var db=new AppDbContext(options))Check(await db.ContabilitaMovimenti.CountAsync()==movements,"Riprova non duplica incassi");
        var unpaid=Req(CassaAction.SetPagamento,paid);unpaid.Pagato=false;await repo.Execute(100,unpaid);await repo.Execute(100,await Quote());
        Check((await repo.Execute(100,Req(CassaAction.OpenSheet))).Conto.All(x=>x.Pagato),"Pagamento singolo annullabile e risaldabile in gruppo");
        var old=await Quote();await Add();await Reject(()=>repo.Execute(100,old),"conto è cambiato");
        var wrongTotal=await Quote();wrongTotal.PrezzoConfermato=0;await Reject(()=>repo.Execute(100,wrongTotal),"totale da pagare");
        await Add();var pending=await Quote();
        var interceptor=new FailSecondPayment();var failingOptions=new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(interceptor).Options;
        var failing=new CassaDatabaseRepository(()=>new AppDbContext(failingOptions));
        int before;await using(var db=new AppDbContext(options))before=await db.ContabilitaMovimenti.CountAsync();
        await Reject(()=>failing.Execute(100,pending),"Errore simulato");
        await using(var db=new AppDbContext(options))Check(await db.SchedeConto.CountAsync(x=>!x.Pagato)==2&&await db.ContabilitaMovimenti.CountAsync()==before,"Errore sulla seconda scrittura annulla il pagamento intero");
        await using(var db=new AppDbContext(options)){(await db.Schede.SingleAsync()).Blocco=true;await db.SaveChangesAsync();}
        await Reject(()=>repo.Execute(100,pending),"bloccata");
        await using(var db=new AppDbContext(options)){var sheet=await db.Schede.SingleAsync();sheet.Blocco=false;sheet.CheckoutTime=DateTime.Now;await db.SaveChangesAsync();}
        await Reject(()=>repo.Execute(100,pending),"chiusa");
        var json=JsonSerializer.Serialize(new DatabaseHostRequest{Cassa=pending},DatabaseProtocolJsonContext.Default.DatabaseHostRequest);
        Check(JsonSerializer.Deserialize(json,DatabaseProtocolJsonContext.Default.DatabaseHostRequest)?.Cassa?.ContoIds.SequenceEqual(pending.ContoIds)==true,"Protocollo AOT trasferisce righe e totale");
    }
    private sealed class FailSecondPayment:SaveChangesInterceptor
    {
        private int _payments;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        {
            if(data.Context!.ChangeTracker.Entries<MovimentoContabile>().Any(x=>x.State==EntityState.Added&&x.Entity.OrigineEvento=="PagamentoVoceSpesa")&&++_payments==2)throw new InvalidOperationException("Errore simulato");
            return ValueTask.FromResult(result);
        }
    }
    private static void Pump(Func<bool> done){var end=DateTime.UtcNow.AddSeconds(20);while(!done()){Dispatcher.UIThread.RunJobs();Thread.Sleep(1);if(DateTime.UtcNow>end)throw new TimeoutException("UI paga tutto");}}
    private static void Command(ReactiveCommand<RxVoid,RxVoid> command){var done=false;Exception? error=null;using var sub=command.Execute().SubscribePrimitives(_=>{},e=>{error=e;done=true;},()=>done=true);Pump(()=>done);if(error is not null)throw error;Dispatcher.UIThread.RunJobs();}
    public static void UI()
    {
        var fake=new Fake();var vm=new CassaViewModel(new Screen(),fake,100);var view=new CassaView{ViewModel=vm};var window=new Window{Width=1400,Height=1000,Content=view};window.Show();Pump(()=>vm.IsUiReady);
        var button=view.FindControl<ControlStyles.UiIconButton>("PayAllButton")!;
        Check(!button.IsEnabled,"Paga tutto disabilitato senza scheda");vm.Posizione="P100";Command(vm.OpenSheetCommand);
        Check(button.IsEnabled&&vm.CanPagaTutto,"Paga tutto disponibile con righe non pagate");
        Command(vm.SetBloccoSchedaCommand);Check(!button.IsEnabled,"Scheda bloccata disabilita pagamento totale");Command(vm.SetBloccoSchedaCommand);
        Command(vm.PagaTuttoCommand);Check(fake.LastBulk?.ContoIds.SequenceEqual(new[]{1,2})==true&&fake.LastBulk.PrezzoConfermato==5,"Richiesta include tutte le righe e solo il totale ancora dovuto");
        Check(vm.Conto.All(x=>x.Pagato)&&vm.DaPagare==0&&!button.IsEnabled&&vm.IsPostazione&&vm.HasSheet,"Interfaccia aggiorna flag, saldo e pulsante mantenendo la scheda");
        Check(view.FindControl<ControlStyles.UiTextBox>("PosizioneBox")!.IsFocused,"Dopo il pagamento il focus torna a Posizione");
        window.Close();Dispatcher.UIThread.RunJobs();
    }
    private sealed class Screen:ReactiveObject,IScreen{public RoutingState Router{get;}=new();}
    private sealed class Fake:ICassaRepository
    {
        public CassaRequest? LastBulk;private bool _locked,_paid;
        public Task<CassaResult> Execute(CassaRequest r,CancellationToken ct=default)
        {
            if(r.Action==CassaAction.SetBloccoScheda)_locked=r.BloccoScheda;
            if(r.Action==CassaAction.PagaTutto){LastBulk=r;_paid=true;}
            if(r.Action is CassaAction.OpenSheet or CassaAction.SetBloccoScheda or CassaAction.PagaTutto)return Task.FromResult(new CassaResult{Scheda=new(){Id=100,Posizione="P100",CheckoutTime=DateTime.MaxValue,Blocco=_locked},Conto=[new(){Id=1,VoiceDesc="Ingresso",VoicePrice=10,Pagato=true},new(){Id=2,VoiceDesc="Bar",VoicePrice=5,Pagato=_paid}]});
            return Task.FromResult(new CassaResult{PostazioneName="Cassa"});
        }
    }
}