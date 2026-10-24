using AppServices.Database;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cassa.Core.Repository;
using Cassa.ViewModels;
using Cassa.Views;
using Leonardo18.DatabaseHost.Cassa;
using Leonardo18.DatabaseHost.Report;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Models.Context;
using Models.Contabilita;
using Models.Sospesi;
using Models.Tables;
using ReactiveUI;
using ReactiveUI.Primitives;
using System.Text.Json;

internal static class UscitaChecks
{
    public static int Count;
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Count++;}
    private static async Task Reject(Func<Task> action,string fragment)
    {try{await action();}catch(InvalidOperationException ex)when(ex.Message.Contains(fragment,StringComparison.OrdinalIgnoreCase)){Count++;return;}throw new Exception("Uscita non rifiutata: "+fragment);}
    public static async Task Database()
    {
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        var options=new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using(var db=new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Postazioni.Add(new(){Id=100,Nome="Cassa uscita",TipoPostazioneId=2,TipoRientroId=-1});
            db.Operatori.AddRange(new Operatore{Id=100,Nome="Cassiere",PersonId=-1,Abilitato=true},new Operatore{Id=101,Nome="Senza permessi",PersonId=-1,Abilitato=true});
            db.Permessi.Add(new(){OperatoreId=100,PostazioneId=100});
            db.Giornate.Add(new(){Id=100,Aperta=true,DataInizio=DateTime.Today.AddDays(-1).AddHours(20),DataFine=DateTime.MaxValue});
            db.Settori.AddRange(new Settore{Id=100,Nome="Ingresso",TipoSettoreId=-1},new Settore{Id=110,Nome="Bar",TipoSettoreId=-2});
            db.Reparti.AddRange(new Reparto{PostazioneId=100,SettoreId=100},new Reparto{PostazioneId=100,SettoreId=110});
            db.Tariffe.AddRange(new Tariffa{Id=100,Nome="Ingresso",Label="Standard",Prezzo=10},new Tariffa{Id=110,Nome="Nome interno",Label="Acqua",Prezzo=2.50m});
            db.Listini.AddRange(new Listino{Id=100,TariffaId=100,SettoreId=100},new Listino{Id=110,TariffaId=110,SettoreId=110});
            for(var i=100;i<108;i++)
            {
                db.People.Add(new(){Id=i,FirstName="Mario",SurName="Rossi",UniqueParam="USCITA"+i,Natoil=19900101,Soci=[new(){NumeroSocio="S"+i,Tessere=[new(){NumeroTessera="T"+i,Abilitato=true,Scadenza=20991231}]}]});
                db.Schede.Add(new(){Id=i,PersonId=i,Posizione="E"+i,NumeroTessera="T"+i,Nome="Mario",Cognome="Rossi",Natoil=19900101,CheckinTime=DateTime.Now.AddMinutes(-20),Grb1=12,
                    SchedeConto=[new(){VoiceDesc="Standard",VoicePrice=10,Pagato=true,DescSettore="Ingresso",DescPostazione="Cassa uscita",DataOra=DateTime.Now.AddMinutes(-20)}]});
            }
            await db.SaveChangesAsync();
        }
        var repo=new CassaDatabaseRepository(()=>new AppDbContext(options));
        CassaRequest Request(CassaAction action,int id)=>new(){Action=action,PostazioneId=100,Posizione="E"+id,SchedaId=id,ListinoId=110};
        async Task<CassaResult> Prepare(int id)=>await repo.Execute(100,Request(CassaAction.PrepareUscita,id));
        CassaRequest Confirm(int id,CassaResult prepared,bool paid)=>new(){Action=CassaAction.RegisterUscita,PostazioneId=100,SchedaId=id,UscitaId=Guid.NewGuid(),VersioneConto=prepared.Uscita!.VersioneConto,Pagato=paid};
        async Task<decimal> Net(string code){await using var db=new AppDbContext(options);return(await db.ContabilitaRighe.Where(x=>x.Conto.Codice==code).Select(x=>new{x.Dare,x.Avere}).ToArrayAsync()).Sum(x=>x.Dare-x.Avere);}
        await Reject(()=>repo.Execute(101,Request(CassaAction.PrepareUscita,100)),"permesso");
        await repo.Execute(100,Request(CassaAction.AddCharge,100));await repo.Execute(100,Request(CassaAction.AddCharge,100));
        var byCard=await repo.Execute(100,new(){Action=CassaAction.PrepareUscita,PostazioneId=100,NumeroTessera="T100"});
        Check(byCard.Scheda?.Id==100&&byCard.Uscita?.DaPagare==5&&byCard.Conto.Count(x=>!x.Pagato)==2,"Ricerca uscita tramite tessera e conto residuo");
        var request=Confirm(100,byCard,true);var before=await new ReportDatabaseRepository(()=>new AppDbContext(options)).Execute(100,new());
        var result=await repo.Execute(100,request);
        Check(result.Uscita is {Confermata:true,Incassato:5,Sospeso:0}&&result.Conto.All(x=>x.Pagato),"Uscita con incasso completo confermata");
        Check(await Net("400")==-5&&await Net("100")==5&&await Net("120")==0,"Incasso chiude il credito senza un secondo ricavo");
        await using(var db=new AppDbContext(options))
        {
            Check(!await db.Schede.AnyAsync(x=>x.Id==100)&&!await db.SchedeConto.AnyAsync(x=>x.SchedaId==100),"Scheda attiva rimossa e posizione liberata");
            var history=await db.SchedeUscite.Include(x=>x.Conto).SingleAsync();
            Check(history.Id==100&&history.Conto.Count==3&&history.CheckoutTime>=history.CheckinTime&&history.Grb1==12&&history.OperatoreId==100&&history.GiornataId==100,"Storico completo con guardaroba, conto e contesto uscita");
        }
        result=await repo.Execute(100,request);Check(result.Uscita?.IdOperazione==request.UscitaId&&await Net("100")==5,"Riprova uscita non duplica l'incasso");
        request.Pagato=false;await Reject(()=>repo.Execute(100,request),"dati diversi");request.Pagato=true;
        await Reject(()=>repo.Execute(100,Confirm(100,byCard,true)),"già uscita");
        var after=await new ReportDatabaseRepository(()=>new AppDbContext(options)).Execute(100,new());
        Check(after.Ingressi==before.Ingressi&&after.Uscite==before.Uscite+1&&after.Presenti==before.Presenti-1&&after.TotaleOperativo==before.TotaleOperativo,"Report mantiene ingressi e consumi dopo l'archiviazione");
        var card=await repo.Execute(100,new(){Action=CassaAction.FindCard,PostazioneId=100,NumeroTessera="T100"});Check(card.Socio?.PosizioneOccupata=="","Socio non più presente dopo uscita");
        await repo.Execute(100,new(){Action=CassaAction.RegisterEntry,PostazioneId=100,NumeroTessera="T100",Posizione="E100",TariffaId=100});Count++;

        await repo.Execute(100,Request(CassaAction.AddCharge,101));var income=await Net("400");var cash=await Net("100");
        result=await repo.Execute(100,Confirm(101,await Prepare(101),false));
        Check(result.Uscita is {Confermata:true,Incassato:0,Sospeso:2.50m}&&await Net("100")==cash&&await Net("400")==income,"Uscita non pagata conserva credito senza incasso o ricavo aggiuntivo");
        long suspended;
        await using(var db=new AppDbContext(options))
        {
            var pending=await db.Sospesi.SingleAsync();suspended=pending.Id;
            Check(pending.Origine==OrigineSospeso.Uscita&&pending.PartitaContabileId!=null&&pending.PersonId==101&&pending.SchedaId==101&&pending.Descrizione.Contains("Acqua"),"Sospeso uscita collegato al credito contabile originale");
        }
        await Reject(()=>repo.Execute(100,new(){Action=CassaAction.RegisterEntry,PostazioneId=100,NumeroTessera="T101",Posizione="E101",TariffaId=100}),"sospesi");
        var pendingList=await repo.Execute(100,new(){Action=CassaAction.LoadSospesi,PostazioneId=100,NumeroTessera="T101"});Check(pendingList.Sospesi.Single().CanIncassare,"Sospeso di uscita incassabile dalla Scheda");
        await repo.Execute(100,new(){Action=CassaAction.SaldaSospeso,PostazioneId=100,SospesoId=suspended});
        Check(await Net("100")==cash+2.50m&&await Net("400")==income&&await Net("120")==0,"Saldo successivo incassa soltanto il credito senza duplicare il ricavo");
        await using(var db=new AppDbContext(options))Check((await db.SchedeUsciteConto.Where(x=>x.SchedaId==101).ToArrayAsync()).All(x=>x.Pagato),"Saldo successivo aggiorna lo stato del conto storico");
        await repo.Execute(100,new(){Action=CassaAction.SaldaSospeso,PostazioneId=100,SospesoId=suspended});Check(await Net("100")==cash+2.50m,"Saldo ripetuto non duplica il denaro");
        await repo.Execute(100,new(){Action=CassaAction.RegisterEntry,PostazioneId=100,NumeroTessera="T101",Posizione="E101",TariffaId=100});Count++;

        async Task Legacy(int id,decimal amount){await using var db=new AppDbContext(options);db.SchedeConto.Add(new(){SchedaId=id,VoiceDesc=amount<0?"STORNO - Pregresso":"Pregresso",VoicePrice=amount,DescSettore="Bar",DescPostazione="Vecchia cassa",DataOra=DateTime.Now});await db.SaveChangesAsync();}
        await Legacy(102,3);await Legacy(102,-1);income=await Net("400");cash=await Net("100");
        await repo.Execute(100,Confirm(102,await Prepare(102),true));Check(await Net("400")==income-2&&await Net("100")==cash+2,"Voci senza contabilità preesistente: incasso e ricavo al saldo del netto");
        await Legacy(103,4);income=await Net("400");cash=await Net("100");
        await repo.Execute(100,Confirm(103,await Prepare(103),false));Check(await Net("400")==income&&await Net("100")==cash,"Voci pregresse non pagate restano solo in Sospesi");
        var legacyDetail=await repo.Execute(100,new(){Action=CassaAction.FindCard,PostazioneId=100,NumeroTessera="T103"});
        Check(legacyDetail.Sospesi.Single().Dettaglio.Contains("Pregresso")&&legacyDetail.Sospesi[0].Dettaglio.Contains(4m.ToString("N2")),"EntraSocio riceve le singole voci del sospeso pregresso");
        await using(var db=new AppDbContext(options))suspended=(await db.Sospesi.SingleAsync(x=>x.SchedaId==103)).Id;
        await repo.Execute(100,new(){Action=CassaAction.SaldaSospeso,PostazioneId=100,SospesoId=suspended});Check(await Net("400")==income-4&&await Net("100")==cash+4,"Saldo pregresso riconosce ricavo e incasso una sola volta");
        var zero=await repo.Execute(100,Confirm(104,await Prepare(104),true));Check(zero.Uscita is {Incassato:0,Sospeso:0,Confermata:true},"Scheda già pagata: uscita senza movimento monetario fittizio");
        var stale=await Prepare(105);await repo.Execute(100,Request(CassaAction.AddCharge,105));await Reject(()=>repo.Execute(100,Confirm(105,stale,true)),"conto è cambiato");
        stale=await Prepare(105);var wardrobe=Request(CassaAction.SaveGuardaroba,105);wardrobe.Grb1=99;await repo.Execute(100,wardrobe);
        await Reject(()=>repo.Execute(100,Confirm(105,stale,true)),"conto è cambiato");
        stale=await Prepare(105);await using(var db=new AppDbContext(options)){(await db.Schede.SingleAsync(x=>x.Id==105)).Blocco=true;await db.SaveChangesAsync();}
        await Reject(()=>repo.Execute(100,Confirm(105,stale,true)),"bloccata");
        await Legacy(106,-1);var negative=await Prepare(106);await Reject(()=>repo.Execute(100,Confirm(106,negative,true)),"rimborso");
        await repo.Execute(100,Request(CassaAction.AddCharge,107));var prepared=await Prepare(107);cash=await Net("100");
        var failing=new CassaDatabaseRepository(()=>new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(new FailArchive()).Options));
        await Reject(()=>failing.Execute(100,Confirm(107,prepared,true)),"Errore archivio simulato");
        await using(var db=new AppDbContext(options))Check(await db.Schede.AnyAsync(x=>x.Id==107)&&!await db.SchedeUscite.AnyAsync(x=>x.Id==107)&&await Net("100")==cash,"Errore finale: rollback anche dell'incasso e nessuna presenza perduta");
        var json=JsonSerializer.Serialize(new DatabaseHostRequest{Cassa=request},DatabaseProtocolJsonContext.Default.DatabaseHostRequest);
        Check(JsonSerializer.Deserialize(json,DatabaseProtocolJsonContext.Default.DatabaseHostRequest)?.Cassa?.UscitaId==request.UscitaId,"Protocollo uscita compatibile con AOT");
    }
    private sealed class FailArchive:SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        {if(data.Context!.ChangeTracker.Entries<SchedaUscita>().Any(x=>x.State==EntityState.Added))throw new InvalidOperationException("Errore archivio simulato");return ValueTask.FromResult(result);}
    }
    public static void UI()
    {
        var fake=new Fake();var vm=new CassaViewModel(new Screen(),fake,100);var view=new CassaView{ViewModel=vm};var window=new Window{Width=1200,Height=800,Content=view};window.Show();Dispatcher.UIThread.RunJobs();
        Check(view.GetVisualDescendants().OfType<ControlStyles.UiIconButton>().Any(x=>x.XLabel=="ESCE SOCIO"&&x.IsEffectivelyVisible),"Pulsante Esce Socio presente in CassaPostazione");
        Check(!vm.CanEsceSocio&&!((System.Windows.Input.ICommand)vm.EsceSocioCommand).CanExecute(null),"Esce Socio disattivato senza scheda aperta");
        vm.Posizione="E01";Check(!vm.CanEsceSocio,"Digitare la posizione non abilita Esce Socio senza aprire la scheda");
        Command(vm.OpenSheetCommand);Check(vm.CanEsceSocio&&((System.Windows.Input.ICommand)vm.EsceSocioCommand).CanExecute(null),"Aprire la scheda abilita Esce Socio");
        Command(vm.ClearSheetCommand);Check(!vm.CanEsceSocio,"Chiudere la scheda disattiva Esce Socio");
        vm.Posizione="E01";Command(vm.OpenSheetCommand);Command(vm.EsceSocioCommand);Check(vm.IsUscita&&vm.HasSocioUscita,"Esce Socio usa la scheda già aperta");
        vm.PosizioneUscita="E01";Command(vm.CercaUscitaCommand);Check(vm.DaPagareUscita==5&&vm.ContoUscita.Count==2&&((System.Windows.Input.ICommand)vm.SospendiUscitaCommand).CanExecute(null),"Riepilogo e comandi incasso/sospeso abilitati");
        foreach(var size in new[]{(1200,800),(900,650)}){window.Width=size.Item1;window.Height=size.Item2;Dispatcher.UIThread.RunJobs();using var frame=window.CaptureRenderedFrame();var folder=Path.Combine(AppContext.BaseDirectory,"..","..","..","previews");Directory.CreateDirectory(folder);frame!.Save(Path.Combine(folder,$"uscita-socio-{size.Item1}.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
        fake.OmitConfirmation=true;Command(vm.IncassaUscitaCommand);var key=fake.Last!.UscitaId;
        Check(!vm.UscitaConfermata&&vm.InfoLabel.Contains("Conferma"),"Conferma assente mantiene il riepilogo per una riprova sicura");
        Command(vm.IncassaUscitaCommand);Check(vm.UscitaConfermata&&fake.Last!.UscitaId==key&&fake.Commits==1&&!vm.HasSheet,"Riprova conserva il codice e libera la Scheda una sola volta");
        Check(!((System.Windows.Input.ICommand)vm.IncassaUscitaCommand).CanExecute(null),"Conferma disabilitata dopo l'uscita");
        Command(vm.BackCommand);Check(!vm.CanEsceSocio,"Uscita completata disabilita Esce Socio");vm.Posizione="E01";Command(vm.OpenSheetCommand);Command(vm.EsceSocioCommand);vm.TesseraUscita="T101";Command(vm.CercaUscitaCommand);Command(vm.SospendiUscitaCommand);
        Check(vm.UscitaConfermata&&fake.Last is {Pagato:false}&&vm.AvvisoUscita.Contains(5m.ToString("N2")),"Uscita con sospeso mostra il debito lasciato");
        Command(vm.BackCommand);Check(vm.IsPostazione,"Ritorno alla Scheda dopo l'uscita");window.Close();Dispatcher.UIThread.RunJobs();
    }
    private static void Command(ReactiveCommand<RxVoid,RxVoid> command)
    {var done=false;Exception? error=null;using var sub=command.Execute().SubscribePrimitives(_=>{},ex=>{error=ex;done=true;},()=>done=true);var end=DateTime.UtcNow.AddSeconds(10);while(!done&&DateTime.UtcNow<end){Dispatcher.UIThread.RunJobs();Thread.Sleep(5);}if(error!=null)throw error;if(!done)throw new TimeoutException();Dispatcher.UIThread.RunJobs();}
    private sealed class Screen:ReactiveObject,IScreen{public RoutingState Router {get;}=new();}
    private sealed class Fake:ICassaRepository
    {
        public CassaRequest? Last;public bool OmitConfirmation;public int Commits;private readonly HashSet<Guid> _ids=[];
        public Task<CassaResult> Execute(CassaRequest r,CancellationToken ct=default)
        {
            if(r.Action==CassaAction.Load)return Task.FromResult(new CassaResult{PostazioneName="Cassa test",AbbonamentiDisponibili=true});
            if(r.Action==CassaAction.StopStrisciate)return Task.FromResult(new CassaResult());
            if(r.Action==CassaAction.RegisterUscita){Last=r;if(_ids.Add(r.UscitaId))Commits++;if(OmitConfirmation){OmitConfirmation=false;return Task.FromResult(new CassaResult());}}
            var success=r.Action==CassaAction.RegisterUscita;
            return Task.FromResult(new CassaResult{Scheda=new(){Id=1,PersonId=100,Posizione="E01",NumeroTessera="T100",Nome="Mario",Cognome="Rossi",Natoil=19900101,CheckinTime=DateTime.Now.AddHours(-2),CheckoutTime=success?DateTime.Now:DateTime.MaxValue,Grb1=12},
                Conto=[new(){Id=1,VoiceDesc="Standard",DescSettore="Ingresso",DescPostazione="Cassa",VoicePrice=10,Pagato=true},new(){Id=2,VoiceDesc="Bibita",DescSettore="Bar",DescPostazione="Banco",VoicePrice=5,Pagato=success&&r.Pagato}],
                Uscita=new(){VersioneConto="Versione1",IdOperazione=r.UscitaId,Confermata=success,DaPagare=success&&r.Pagato?0:5,Incassato=success&&r.Pagato?5:0,Sospeso=success&&!r.Pagato?5:0,DataUscita=success?DateTime.Now:null}});
        }
    }
}
