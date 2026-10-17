using Avalonia.Headless;
using System.Text.Json;
using AppServices.Database;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cassa.Core.Repository;
using Cassa.ViewModels;
using Cassa.Views;
using Leonardo18.DatabaseHost.Cassa;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Models.Context;
using Models.Contabilita;
using Models.Sospesi;
using Models.Tables;
using ReactiveUI;
using ReactiveUI.Primitives;

internal static class AbbonamentoChecks
{
    public static int Count;
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Count++;}
    private static async Task Reject(Func<Task> action,string fragment)
    {
        try{await action();}catch(InvalidOperationException ex)when(ex.Message.Contains(fragment,StringComparison.OrdinalIgnoreCase)){Count++;return;}
        throw new Exception("Acquisto o incasso non rifiutato: "+fragment);
    }
    public static async Task Database()
    {
        using(var sql=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=localhost;Database=SospesiSchemaTest;Integrated Security=true;TrustServerCertificate=true").Options))
        {
            Check(!sql.Database.HasPendingModelChanges(),"Snapshot Sospesi coerente");
            var entity=sql.Model.FindEntityType(typeof(Sospeso))!;
            Check(entity.GetTableName()=="Sospesi"&&entity.GetForeignKeys().All(x=>x.DeleteBehavior==DeleteBehavior.NoAction&&x.PrincipalEntityType.ClrType.Namespace=="Models.Contabilita"),"Sospesi separati dall'operativo senza cancellazioni in cascata");
            var assembly=sql.GetService<IMigrationsAssembly>();var last=assembly.Migrations.Single(x=>x.Key.EndsWith("_AddSospesiAbbonamenti"));
            var migration=assembly.CreateMigration(last.Value,sql.Database.ProviderName!);
            Check(migration.UpOperations.OfType<CreateTableOperation>().Single().Name=="Sospesi"&&migration.UpOperations.OfType<AddColumnOperation>().Count()==6,"Migrazione aggiuntiva: Sospesi e dati acquisto");
            Check(!migration.UpOperations.Any(x=>x is DropTableOperation or DropColumnOperation or DeleteDataOperation),"Nessun dato esistente rimosso");
            var script=sql.GetService<IMigrator>().GenerateScript(assembly.Migrations.First().Key,last.Key);
            Check(script.Contains("CK_Sospesi_Stato")&&script.Contains("IX_Abbonamenti_AcquistoId")&&script.Contains("decimal(18,2)"),"SQL Server include vincoli e chiavi contro i duplicati");
        }
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        var options=new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using(var db=new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Postazioni.Add(new(){Id=100,Nome="Cassa abbonamenti",TipoPostazioneId=2,TipoRientroId=-1});
            db.Operatori.AddRange(new Operatore{Id=100,Nome="Acquisto",PersonId=-1,Abilitato=true},new Operatore{Id=101,Nome="Saldo",PersonId=-1,Abilitato=true});
            db.Permessi.AddRange(new Permesso{OperatoreId=100,PostazioneId=100},new Permesso{OperatoreId=101,PostazioneId=100});
            db.Giornate.Add(new(){Id=100,Aperta=true,DataInizio=DateTime.Today,DataFine=DateTime.MaxValue});
            db.TipiAbbonamento.AddRange(new TipoAbbonamento{Id=100,Nome="Dieci ingressi",NumeroIngressi=10,DurataGiorni=30,Prezzo=50,Attivo=true},
                new TipoAbbonamento{Id=101,Nome="Gratuito",NumeroIngressi=1,DurataGiorni=1,Prezzo=0,Attivo=true},
                new TipoAbbonamento{Id=102,Nome="Inattivo",NumeroIngressi=1,DurataGiorni=1,Prezzo=1,Attivo=false});
            for(var i=100;i<106;i++)db.People.Add(new(){Id=i,FirstName="Nome"+i,SurName="Cognome"+i,UniqueParam="ABBO"+i,Natoil=19900101,
                Soci=new(){new(){NumeroSocio="S"+i,Tessere=new(){new(){NumeroTessera="T"+i,Scadenza=i==104?20200101:20991231,Abilitato=i!=105}}}}});
            await db.SaveChangesAsync();
        }
        var repository=new CassaDatabaseRepository(()=>new AppDbContext(options));
        var available=await repository.Execute(100,new(){Action=CassaAction.Load,PostazioneId=100});
        Check(available.AbbonamentiDisponibili==true,"Abbonamenti disponibili al caricamento cassa");
        var catalog=await repository.Execute(100,new(){Action=CassaAction.PrepareAbbonamento,PostazioneId=100});
        Check(catalog.TipiAbbonamento.Length==2&&catalog.TipiAbbonamento.Any(x=>x.Prezzo==0),"Vendibili: attivi validi, inclusi gratuiti");
        await using(var db=new AppDbContext(options)){await db.TipiAbbonamento.Where(x=>x.Id>0).ExecuteUpdateAsync(x=>x.SetProperty(t=>t.Attivo,false));}
        available=await repository.Execute(100,new(){Action=CassaAction.Load,PostazioneId=100});
        Check(available.AbbonamentiDisponibili==false,"Nessun abbonamento vendibile: pulsante disabilitabile");
        await using(var db=new AppDbContext(options)){await db.TipiAbbonamento.Where(x=>x.Id==100||x.Id==101).ExecuteUpdateAsync(x=>x.SetProperty(t=>t.Attivo,true));}
        decimal price=50;var ingressi=10;var duration=30;
        CassaRequest Buy(bool paid,string first="T100",int person=100,string second="",int person2=0,int type=100)=>new()
        {
            Action=CassaAction.BuyAbbonamento,PostazioneId=100,AcquistoId=Guid.NewGuid(),TipoAbbonamentoId=type,
            NumeroTessera=first,PersonId1=person,NumeroTessera2=second,PersonId2=person2,
            PrezzoConfermato=type==101?0:price,IngressiConfermati=type==101?1:ingressi,DurataConfermata=type==101?1:duration,Pagato=paid,Note="Nota acquisto"
        };
        var paid=Buy(true);var result=await repository.Execute(100,paid);var paidId=result.AcquistoAbbonamento!.AbbonamentoId;
        Check(result.AcquistoAbbonamento is {Pagato:true,Prezzo:50,SospesoId:null},"Acquisto pagato confermato");
        await using(var db=new AppDbContext(options))
        {
            var subscription=await db.Abbonamenti.SingleAsync();var movement=await db.ContabilitaMovimenti.Include(x=>x.Righe).SingleAsync();
            Check(subscription.AcquistoId==paid.AcquistoId&&subscription.PrezzoAcquisto==50&&subscription.PagatoAllAcquisto==true&&subscription.IngressiAcquistati==10,"Dati acquisto e ingressi conservati");
            Check(subscription.PersonId1==100&&subscription.TesseraId1!=null&&subscription.PersonId2==null&&subscription.Attivo,"Abbonamento associato alla tessera");
            Check(movement.IdOperazione==paid.AcquistoId&&movement.OrigineEntita=="Abbonamento"&&movement.OrigineId==paidId.ToString()&&movement.PersonId==100,"Acquisto e incasso collegati");
            var cash=await db.ContabilitaPianoConti.Where(x=>x.Codice=="100").Select(x=>x.Id).SingleAsync();var revenue=await db.ContabilitaPianoConti.Where(x=>x.Codice=="400").Select(x=>x.Id).SingleAsync();
            Check(movement.Righe.Single(x=>x.ContoId==cash).Dare==50&&movement.Righe.Single(x=>x.ContoId==revenue).Avere==50,"Acquisto pagato: Cassa Dare, Ricavi Avere");
            Check(movement.Righe.All(x=>x.AbbonamentoId==paidId&&x.NomeAbbonamento=="Dieci ingressi")&&!await db.Sospesi.AnyAsync()&&!await db.ContabilitaPartite.AnyAsync(),"Nessun sospeso o debito contabile per acquisto pagato");
            movement.VerificaQuadratura();Count++;
        }
        result=await repository.Execute(100,paid);Check(result.AcquistoAbbonamento!.AbbonamentoId==paidId,"Riprova acquisto restituisce lo stesso abbonamento");
        await Reject(()=>repository.Execute(101,paid),"dati diversi");paid.Note="Diversa";await Reject(()=>repository.Execute(100,paid),"dati diversi");paid.Note="Nota acquisto";
        var unpaid=Buy(false,"T101",101,"T102",102);result=await repository.Execute(100,unpaid);var unpaidId=result.AcquistoAbbonamento!.AbbonamentoId;var pendingId=result.AcquistoAbbonamento.SospesoId!.Value;
        Check(!result.AcquistoAbbonamento.Pagato&&result.AcquistoAbbonamento.HasSecondoSocio&&result.AcquistoAbbonamento.Socio2.Contains("102"),"Acquisto non pagato per due soci");
        await using(var db=new AppDbContext(options))
        {
            Check(await db.Abbonamenti.CountAsync()==2&&await db.ContabilitaMovimenti.CountAsync()==1&&!await db.ContabilitaPartite.AnyAsync(),"Non pagato: nessuna scrittura contabile né ricavo anticipato");
            var pending=await db.Sospesi.SingleAsync();
            Check(pending.Id==pendingId&&pending.AbbonamentoId==unpaidId&&pending.PersonId==101&&pending.PersonId2==102&&pending.Importo==50&&pending.Tipo==TipoSospeso.Credito&&pending.Origine==OrigineSospeso.Abbonamento,"Un solo credito condiviso e collegato ai soci");
            Check(pending.MovimentoSaldoId==null&&pending.PartitaContabileId==null&&pending.Stato==StatoSospeso.Aperto&&pending.NumeroTessera2=="T102","Sospeso fuori dalla contabilità fino al saldo");
            db.Schede.Add(new(){Id=100,PersonId=102,NumeroTessera="T102",Posizione="A102",Nome="Nome102",Cognome="Cognome102",CheckoutTime=DateTime.MaxValue});
            var socio=await db.Soci.SingleAsync(x=>x.PersonId==102);db.Tessere.Add(new(){SocioId=socio.Id,NumeroTessera="T202",Scadenza=20991231,Abilitato=true});
            await db.SaveChangesAsync();
        }
        result=await repository.Execute(100,unpaid);Check(result.AcquistoAbbonamento!.SospesoId==pendingId,"Riprova non duplica il sospeso");
        foreach(var card in new[]{"T101","T102","T202"})
        {
            result=await repository.Execute(100,new(){Action=CassaAction.FindCard,PostazioneId=100,NumeroTessera=card});
            Check(result.Sospesi.Single().Id==pendingId&&result.Sospesi[0].Importo==50&&!string.IsNullOrWhiteSpace(result.Sospesi[0].Descrizione),"Identificazione restituisce dettaglio sospeso: "+card); Check(result.Socio is {SospesiAperti:1,TotaleSospesi:50,HasSospesi:true},"Richiamo del credito su socio e nuova tessera: "+card);
            result=await repository.Execute(100,new(){Action=CassaAction.LoadSospesi,PostazioneId=100,NumeroTessera=card});
            Check(result.Sospesi.Single().Id==pendingId&&result.Sospesi[0].CanIncassare,"Ricerca stesso sospeso da entrambe le tessere: "+card);
        }
        result=await repository.Execute(100,new(){Action=CassaAction.OpenSheet,PostazioneId=100,Posizione="A102"});Check(result.Scheda is {SospesiAperti:1,TotaleSospesi:50},"Richiamo del credito aprendo la scheda");
        await Reject(()=>repository.Execute(100,Buy(false,"T102",102,"T202",102)),"distinti");
        await Reject(()=>repository.Execute(100,Buy(true,"T104",104)),"primo socio");await Reject(()=>repository.Execute(100,Buy(true,"T105",105)),"primo socio");
        await Reject(()=>repository.Execute(100,Buy(true,"IGNOTA",100)),"primo socio");await Reject(()=>repository.Execute(100,Buy(true,"T100",103)),"primo socio");
        await Reject(()=>repository.Execute(100,Buy(true,type:102)),"non disponibile");
        var invalid=Buy(true);invalid.AcquistoId=Guid.Empty;await Reject(()=>repository.Execute(100,invalid),"Identificativo");
        invalid=Buy(true);invalid.Note=new string('A',51);await Reject(()=>repository.Execute(100,invalid),"note");
        invalid=Buy(true);invalid.PrezzoConfermato=99;await Reject(()=>repository.Execute(100,invalid),"cambiate");
        invalid=Buy(true);invalid.PersonId2=102;await Reject(()=>repository.Execute(100,invalid),"Manca");
        await using(var db=new AppDbContext(options))
        {
            (await db.Giornate.SingleAsync()).Aperta=false;db.Giornate.Add(new(){Id=101,Aperta=true,DataInizio=DateTime.Today.AddDays(3),DataFine=DateTime.MaxValue});
            var type=await db.TipiAbbonamento.SingleAsync(x=>x.Id==100);type.Nome="Nuovo listino";type.Prezzo=75;type.NumeroIngressi=20;type.DurataGiorni=40;
            (await db.ContabilitaPianoConti.SingleAsync(x=>x.Codice=="100")).Attivo=false;await db.SaveChangesAsync();
        }
        price=75;ingressi=20;duration=40;
        var settle=new CassaRequest{Action=CassaAction.SaldaSospeso,PostazioneId=100,SospesoId=pendingId,NumeroTessera="T202"};
        await Reject(()=>repository.Execute(101,settle),"conti 100");
        await using(var db=new AppDbContext(options))
        {
            Check((await db.Sospesi.SingleAsync()).Stato==StatoSospeso.Aperto&&await db.ContabilitaMovimenti.CountAsync()==1,"Incasso rifiutato conserva credito aperto e contabilità");
            (await db.ContabilitaPianoConti.SingleAsync(x=>x.Codice=="100")).Attivo=true;await db.SaveChangesAsync();
        }
        var failureOptions=new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(new FailAccounting()).Options;
        var failing=new CassaDatabaseRepository(()=>new AppDbContext(failureOptions));
        await Reject(()=>failing.Execute(101,settle),"errore simulato");
        await using(var db=new AppDbContext(options))Check((await db.Sospesi.SingleAsync()).Stato==StatoSospeso.Aperto&&await db.ContabilitaMovimenti.CountAsync()==1,"Errore nel saldo non chiude il sospeso né lascia incassi");
        result=await repository.Execute(101,settle);Check(result.SospesoSaldatoId==pendingId&&result.Sospesi.Length==0,"Saldo rimuove il sospeso dalla lista degli aperti");
        Check(result.SociConSospesi.Length==2&&result.SociConSospesi.All(x=>!x.HasSospesi),"Saldo dalla seconda tessera aggiorna i richiami di entrambi i soci");
        await repository.Execute(101,settle);
        await using(var db=new AppDbContext(options))
        {
            var pending=await db.Sospesi.SingleAsync();var movement=await db.ContabilitaMovimenti.Include(x=>x.Righe).SingleAsync(x=>x.Id==pending.MovimentoSaldoId);
            Check(pending.Stato==StatoSospeso.Saldato&&pending.DataSaldo!=null&&pending.OperatoreSaldoId==101&&pending.GiornataSaldoId==101,"Storico del saldo con nuovo operatore e giornata");
            Check(movement.DataCompetenza==DateTime.Today.AddDays(3)&&movement.Righe.Sum(x=>x.Dare)==50&&movement.Righe.All(x=>x.NomeAbbonamento=="Dieci ingressi"),"Saldo dopo tre giorni conserva prezzo e nome originali");
            Check(await db.ContabilitaMovimenti.CountAsync()==2&&await db.Sospesi.CountAsync()==1&&!await db.ContabilitaPartite.AnyAsync(),"Saldo ripetuto non duplica ricavo, incasso o sospeso");
        }
        result=await repository.Execute(100,unpaid);Check(result.AcquistoAbbonamento is {Pagato:true,SospesoId:not null}&&result.AcquistoAbbonamento.AbbonamentoId==unpaidId,"Recupero risposta acquisto dopo il saldo");
        result=await repository.Execute(100,paid);Check(result.AcquistoAbbonamento!.AbbonamentoId==paidId&&result.AcquistoAbbonamento.Prezzo==50,"Riprova pagato conserva condizioni storiche dopo cambio listino");
        result=await repository.Execute(100,new(){Action=CassaAction.FindCard,PostazioneId=100,NumeroTessera="T202"});Check(result.Socio is {HasSospesi:false,TotaleSospesi:0},"Richiamo sparisce dopo l'incasso");
        var free=Buy(false,type:101);result=await repository.Execute(100,free);Check(result.AcquistoAbbonamento is {Prezzo:0,Pagato:true,SospesoId:null},"Abbonamento gratuito senza debito");await repository.Execute(100,free);
        await using(var db=new AppDbContext(options))Check(await db.Abbonamenti.CountAsync()==3&&await db.ContabilitaMovimenti.CountAsync()==2&&await db.Sospesi.CountAsync()==1,"Gratuito e riprova non generano importi o duplicati");
        await using(var db=new AppDbContext(options)){(await db.ContabilitaPianoConti.SingleAsync(x=>x.Codice=="100")).Attivo=false;await db.SaveChangesAsync();}
        await Reject(()=>repository.Execute(100,Buy(true)),"conti 100");
        await using(var db=new AppDbContext(options))
        {
            Check(await db.Abbonamenti.CountAsync()==3,"Incasso fallito annulla anche l'abbonamento già salvato");
            (await db.ContabilitaPianoConti.SingleAsync(x=>x.Codice=="100")).Attivo=true;await db.SaveChangesAsync();
        }
        var failedPending=Buy(false,"T103",103);await Reject(()=>failing.Execute(100,failedPending),"errore simulato");
        await using(var db=new AppDbContext(options))Check(await db.Abbonamenti.CountAsync()==3&&await db.Sospesi.CountAsync()==1,"Errore sospeso annulla anche l'acquisto operativo");
        result=await repository.Execute(100,failedPending);var newPending=result.AcquistoAbbonamento!.SospesoId;await repository.Execute(100,failedPending);
        await using(var db=new AppDbContext(options))
        {
            Check(await db.Abbonamenti.CountAsync()==4&&await db.Sospesi.CountAsync()==2&&await db.ContabilitaMovimenti.CountAsync()==2,"Riprova dopo errore crea un solo sospeso senza anticipare ricavi");
            db.Sospesi.Add(new(){IdOperazione=Guid.NewGuid(),Tipo=TipoSospeso.Credito,Origine=OrigineSospeso.Uscita,Importo=10,PersonId=100,Nominativo="Socio uscita",DataOra=DateTime.Now,DataCompetenza=DateTime.Today,SchedaId=900,SchedaContoId=901,Descrizione="Credito uscita futura"});
            db.Sospesi.Add(new(){IdOperazione=Guid.NewGuid(),Tipo=TipoSospeso.Debito,Origine=OrigineSospeso.Manuale,Importo=5,PersonId=100,Nominativo="Socio",DataOra=DateTime.Now,DataCompetenza=DateTime.Today,Descrizione="Debito futuro"});
            await db.SaveChangesAsync();
        }
        result=await repository.Execute(100,new(){Action=CassaAction.LoadSospesi,PostazioneId=100});
        Check(result.Sospesi.Count(x=>x.CanIncassare)==1&&result.Sospesi.Any(x=>x.Origine=="Uscita"&&!x.CanIncassare)&&result.Sospesi.Any(x=>x.Tipo=="Debito"&&!x.CanIncassare),"Tabella pronta per uscita e debiti senza attivare funzioni non richieste");
        var future=result.Sospesi.Single(x=>x.Origine=="Uscita");await Reject(()=>repository.Execute(100,new(){Action=CassaAction.SaldaSospeso,PostazioneId=100,SospesoId=future.Id}),"Storico uscita");
        await Reject(()=>repository.Execute(100,new(){Action=CassaAction.SaldaSospeso,PostazioneId=100,SospesoId=long.MaxValue}),"non trovato");
        var json=JsonSerializer.Serialize(new DatabaseHostResponse{Success=true,Cassa=new(){Sospesi=result.Sospesi,SospesoSaldatoId=pendingId}},DatabaseProtocolJsonContext.Default.DatabaseHostResponse);
        var roundtrip=JsonSerializer.Deserialize(json,DatabaseProtocolJsonContext.Default.DatabaseHostResponse);
        Check(roundtrip?.Cassa?.Sospesi.Length==3&&roundtrip.Cassa.SospesoSaldatoId==pendingId,"Protocollo compatibile con liste e saldo sospesi");
        json=JsonSerializer.Serialize(new DatabaseHostRequest{Cassa=new(){Action=CassaAction.SaldaSospeso,SospesoId=newPending!.Value}},DatabaseProtocolJsonContext.Default.DatabaseHostRequest);
        Check(JsonSerializer.Deserialize(json,DatabaseProtocolJsonContext.Default.DatabaseHostRequest)?.Cassa?.SospesoId==newPending,"Protocollo trasferisce identità del sospeso");
    }

    private sealed class FailAccounting:SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        {
            if(data.Context!.ChangeTracker.Entries<MovimentoContabile>().Any(x=>x.State==EntityState.Added)||data.Context.ChangeTracker.Entries<Sospeso>().Any(x=>x.State==EntityState.Added))throw new InvalidOperationException("Errore simulato");
            return ValueTask.FromResult(result);
        }
    }

    private static void Command(ReactiveCommand<RxVoid,RxVoid> command)
    {
        Exception? error=null;var done=false;using var sub=command.Execute().SubscribePrimitives(_=>{},ex=>{error=ex;done=true;},()=>done=true);
        while(!done)Dispatcher.UIThread.RunJobs();if(error is not null)throw error;Dispatcher.UIThread.RunJobs();
    }
    private static void Identify(CassaViewModel vm,string slot)
    {
        Exception? error=null;var done=false;using var sub=vm.IdentificaTesseraAbbonamentoCommand.Execute(slot).SubscribePrimitives(_=>{},ex=>{error=ex;done=true;},()=>done=true);
        while(!done)Dispatcher.UIThread.RunJobs();if(error is not null)throw error;Dispatcher.UIThread.RunJobs();
    }
    public static void EntryPendingUI()
    {
        var repository=new Fake();var vm=new CassaViewModel(new Screen(),repository,100);
        var view=new CassaView{ViewModel=vm};var window=new Window{Width=1200,Height=800,Content=view};window.Show();
        void Pump(Func<bool> done){var deadline=DateTime.UtcNow.AddSeconds(10);while(!done()){Dispatcher.UIThread.RunJobs();Thread.Sleep(1);if(DateTime.UtcNow>deadline)throw new TimeoutException("Verifica EntraSocio");}}
        void Execute(ReactiveCommand<RxVoid,RxVoid> command){var done=false;using var sub=command.Execute().SubscribePrimitives(_=>{},ex=>throw ex,()=>done=true);Pump(()=>done);Dispatcher.UIThread.RunJobs();}
        Pump(()=>vm.IsUiReady);Execute(vm.EntraSocioCommand);vm.NumeroTessera="T100";Execute(vm.FindCardCommand);
        Check(!vm.ShowIngressi&&vm.SospesiIngresso.Count==1,"Sospeso identificato");
        var rates=view.GetVisualDescendants().OfType<ListBox>().Single(x=>ReferenceEquals(x.ItemsSource,vm.Ingressi));
        Check(!rates.IsEffectivelyVisible,"Vista ingressi nascosta");
        var button=view.GetVisualDescendants().OfType<ControlStyles.UiIconButton>().Single(x=>x.XLabel=="SALDA SOSPESO"&&x.IsEffectivelyVisible);
        Check(button.Command is not null&&button.IsEffectivelyEnabled,"Comando saldo visibile e collegato");
        var pending=vm.SospesiIngresso.Single();
        void Settle(){var done=false;using var sub=vm.IncassaSospesoCommand.Execute(pending).SubscribePrimitives(_=>{},ex=>throw ex,()=>done=true);Pump(()=>done);Dispatcher.UIThread.RunJobs();}
        repository.OmitConfirmation=true;Settle();Check(!vm.ShowIngressi&&vm.SospesiIngresso.Count==1,"Saldo senza conferma mantiene il blocco");
        Settle();Check(vm.IsEntry&&vm.ShowIngressi&&vm.CanSelectIngresso&&rates.IsEffectivelyVisible&&vm.SospesiIngresso.Count==0,"Saldo confermato riapre le opzioni senza cambiare pagina");
        window.Close();Dispatcher.UIThread.RunJobs();
    }
    public static void UI()
    {
        var emptyVm=new CassaViewModel(new Screen(),new Fake{Available=false},100);
        var emptyView=new CassaView{ViewModel=emptyVm};var emptyWindow=new Window{Width=1200,Height=800,Content=emptyView};emptyWindow.Show();Dispatcher.UIThread.RunJobs();
        Check(!emptyVm.AcquistoAbbonamentiDisponibile&&!((System.Windows.Input.ICommand)emptyVm.AcquistaAbbonamentoCommand).CanExecute(null),"Comando disabilitato senza abbonamenti vendibili");
        Check(!emptyView.GetVisualDescendants().OfType<ControlStyles.UiIconButton>().Single(x=>x.XLabel=="ABBONAMENTI").IsEffectivelyEnabled,"Pulsante visibilmente disabilitato senza abbonamenti");
        emptyWindow.Close();Dispatcher.UIThread.RunJobs();
        var repository=new Fake();var vm=new CassaViewModel(new Screen(),repository,100);var view=new CassaView{ViewModel=vm};var window=new Window{Width=1200,Height=800,Content=view};window.Show();Dispatcher.UIThread.RunJobs();
        Check(vm.AcquistoAbbonamentiDisponibile&&((System.Windows.Input.ICommand)vm.AcquistaAbbonamentoCommand).CanExecute(null),"Acquisto abbonamenti riattivato");
        var button=view.GetVisualDescendants().OfType<ControlStyles.UiIconButton>().Single(x=>x.XLabel=="ABBONAMENTI");Check(button.IsEffectivelyEnabled,"Pulsante abbonamenti disponibile");
        Command(vm.AcquistaAbbonamentoCommand);Check(vm.IsAcquistoAbbonamento&&!((System.Windows.Input.ICommand)vm.ConfermaAbbonamentoCommand).CanExecute(null),"Conferma richiede identificazione socio");
        vm.TesseraAbbonamento1="T100";Identify(vm,"1");Check(((System.Windows.Input.ICommand)vm.ConfermaAbbonamentoCommand).CanExecute(null),"Conferma abilitata con dati validi");
        vm.SecondoSocioAbbonamento=true;Check(!((System.Windows.Input.ICommand)vm.ConfermaAbbonamentoCommand).CanExecute(null),"Secondo socio richiede identificazione");
        vm.TesseraAbbonamento2="T100";Identify(vm,"2");Check(vm.AbbonamentoWarning.Contains("distinti"),"Stessa persona non può occupare entrambi i beneficiari");
        vm.TesseraAbbonamento2="T101";Identify(vm,"2");Shot(window,"acquisto-abbonamento-due-soci");Command(vm.ConfermaAbbonamentoCommand);
        Check(vm.HasAcquistoAbbonamento&&vm.AcquistoAbbonamento is {Pagato:false,SospesoId:7},"Acquisto non pagato mostra il sospeso");Shot(window,"abbonamento-in-sospeso");
        Check(repository.LastPurchase is {Pagato:false,PersonId1:100,PersonId2:101,PrezzoConfermato:50}&&repository.LastPurchase.AcquistoId!=Guid.Empty,"Dati acquisto e stato pagato inviati correttamente");
        Command(vm.AcquistaAbbonamentoCommand);vm.TesseraAbbonamento1="T100";Identify(vm,"1");vm.AbbonamentoPagato=true;repository.FailNext=true;
        Command(vm.ConfermaAbbonamentoCommand);var attempt=repository.LastPurchase!.AcquistoId;
        Check(vm.HasErroreAcquisto&&vm.CanEditAbbonamento,"Errore mantiene modulo e dati per riprova");
        Command(vm.ConfermaAbbonamentoCommand);Check(vm.AcquistoAbbonamento is {Pagato:true}&&repository.LastPurchase!.AcquistoId==attempt,"Riprova mantiene identità acquisto");
                        Command(vm.BackCommand);Command(vm.EntraSocioCommand);vm.NumeroTessera="T100";Command(vm.FindCardCommand);
        var identified=vm.Member; Check(vm.SospesiIngresso.Count==1&&view.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="Abbonamento Dieci"&&x.IsEffectivelyVisible),"Dettaglio sospeso visibile in EntraSocio");
        Check(!view.GetVisualDescendants().OfType<ListBox>().Single(x=>ReferenceEquals(x.ItemsSource,vm.Ingressi)).IsEffectivelyEnabled,"Lista ingressi disabilitata con sospesi aperti");
        Check(!vm.CanSelectIngresso&&!((System.Windows.Input.ICommand)vm.RegisterEntryCommand).CanExecute(null),"Sospesi aperti bloccano scelta tariffa e ingresso");
        Check(identified is {HasSospesi:true}&&view.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text?.StartsWith("SOSPESI:")==true&&x.IsEffectivelyVisible),"Richiamo visibile appena viene identificato il socio");
        Check(!view.GetVisualDescendants().OfType<ControlStyles.UiIconButton>().Any(x=>(x.XLabel=="VEDI SOSPESI"||x.XLabel=="SOSPESI")&&x.IsEffectivelyVisible),"EntraSocio mostra il dettaglio senza comandi di gestione sospesi");
        Check(!vm.ShowIngressi&&!view.GetVisualDescendants().OfType<ListBox>().Single(x=>ReferenceEquals(x.ItemsSource,vm.Ingressi)).IsEffectivelyVisible,"Tariffe nascoste con sospesi");
        var entryPending=vm.SospesiIngresso.Single();
        var settle=view.GetVisualDescendants().OfType<ControlStyles.UiIconButton>().Single(x=>x.XLabel=="SALDA SOSPESO"&&x.IsEffectivelyVisible);
        Check(settle.Command is not null&&settle.IsEffectivelyEnabled,"Saldo disponibile in EntraSocio");
        repository.OmitConfirmation=true;
        void SettleEntry(){var finished=false;using var subscription=vm.IncassaSospesoCommand.Execute(entryPending).SubscribePrimitives(_=>{},ex=>throw ex,()=>finished=true);var deadline=DateTime.UtcNow.AddSeconds(10);while(!finished){Dispatcher.UIThread.RunJobs();if(DateTime.UtcNow>deadline)throw new TimeoutException("Saldo ingresso");}}
        SettleEntry();Check(!vm.ShowIngressi&&vm.SospesiIngresso.Count==1,"Conferma mancante mantiene il blocco ingresso");
        SettleEntry();Dispatcher.UIThread.RunJobs();
        Check(vm.IsEntry&&vm.ShowIngressi&&vm.CanSelectIngresso&&vm.SospesiIngresso.Count==0,"Saldo in EntraSocio mostra subito le tariffe");
        repository.Collections=0;Command(vm.FindCardCommand);
        var blockedDone=false;
        using(var blocked=vm.ApriSospesiSocioCommand.Execute("T100").SubscribePrimitives(_=>{},ex=>throw ex,()=>blockedDone=true))
            while(!blockedDone)Dispatcher.UIThread.RunJobs();
        Check(vm.IsEntry,"La gestione sospesi non si apre da EntraSocio");
        Command(vm.BackCommand);Command(vm.ApriSospesiCommand);
        Check(vm.IsSospesi&&vm.Sospesi.Count==1,"Gestione sospesi disponibile in postazione Scheda");
        Shot(window,"sospesi-incasso");
        repository.OmitConfirmation=true;
        var record=vm.Sospesi[0];
        var done=false;Exception? error=null;
        using(var sub=vm.IncassaSospesoCommand.Execute(record).SubscribePrimitives(_=>{},ex=>{error=ex;done=true;},()=>done=true))
            while(!done)Dispatcher.UIThread.RunJobs();
        if(error is not null)throw error;Dispatcher.UIThread.RunJobs();
        Check(vm.Sospesi.Count==1&&vm.InfoLabel.Contains("Conferma"),"Risposta senza conferma non mostra un falso incasso riuscito");
        var collect=view.GetVisualDescendants().OfType<ControlStyles.UiIconButton>().Single(x=>x.XLabel=="REGISTRA INCASSO"&&x.IsEffectivelyVisible);
        Check(collect.Command is not null&&collect.IsEffectivelyEnabled,"Pulsante incasso con binding compilato");
        ((System.Windows.Input.ICommand)collect.Command!).Execute(collect.CommandParameter);Dispatcher.UIThread.RunJobs();
        Check(vm.Sospesi.Count==0&&repository.Collections==1&&vm.InfoLabel.Contains("registrat"),"Incasso rimuove il sospeso dalla lista");
                Command(vm.BackCommand);
        Check(vm.IsPostazione,"Dopo il saldo si torna alla Scheda");
        Command(vm.EntraSocioCommand);vm.NumeroTessera="T100";Command(vm.FindCardCommand);
        Check(view.GetVisualDescendants().OfType<ListBox>().Single(x=>ReferenceEquals(x.ItemsSource,vm.Ingressi)).IsEffectivelyEnabled,"Lista ingressi riabilitata dopo il saldo");
        Check(vm.CanSelectIngresso,"Incasso riabilita la scelta dell'ingresso");
        vm.EntryPosition="E01";Command(vm.RegisterEntryCommand);
        Check(repository.LastEntry is {Action:CassaAction.RegisterEntry},"Ingresso consentito dopo il saldo gestito dalla Scheda");
        Command(vm.BackCommand);Check(vm.IsPostazione,"Ritorno alla postazione dopo sospesi");window.Close();Dispatcher.UIThread.RunJobs();
    }
    private static void Shot(Window window,string name)
    {
        Dispatcher.UIThread.RunJobs();
        using var frame=window.CaptureRenderedFrame();
        if(frame is null)throw new Exception("Anteprima non disponibile");
        var directory=Path.Combine(AppContext.BaseDirectory,"..","..","..","previews");
        Directory.CreateDirectory(directory);
        frame.Save(Path.Combine(directory,name+".png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }
    private sealed class Screen:ReactiveObject,IScreen{public RoutingState Router{get;}=new();}
    private sealed class Fake:ICassaRepository
    {
        public bool Available=true;public CassaRequest? LastPurchase;public CassaRequest? LastEntry;public bool FailNext;public bool OmitConfirmation;public int Collections;
        public Task<CassaResult> Execute(CassaRequest request,CancellationToken ct=default)
        {
            if(request.Action==CassaAction.PrepareEntry)return Task.FromResult(new CassaResult{Ingressi=[new(){Id=100,Nome="Ingresso",Etichetta="Ingresso",Prezzo=10}]});
            if(request.Action==CassaAction.RegisterEntry){LastEntry=request;return Task.FromResult(new CassaResult{Scheda=new(){Id=1,Posizione=request.Posizione}});}
            if(request.Action==CassaAction.BuyAbbonamento)
            {
                LastPurchase=request;if(FailNext){FailNext=false;throw new InvalidOperationException("Errore acquisto simulato");}
                return Task.FromResult(new CassaResult{AcquistoAbbonamento=new(){AbbonamentoId=1,Nome="Dieci",Prezzo=50,Ingressi=10,DataInizio=DateTime.Today,DataFine=DateTime.Today.AddDays(30),NumeroTessera1=request.NumeroTessera,NumeroTessera2=request.NumeroTessera2,Pagato=request.Pagato,SospesoId=request.Pagato?null:7}});
            }
            if(request.Action==CassaAction.PrepareAbbonamento)return Task.FromResult(new CassaResult{AbbonamentiDisponibili=true,TipiAbbonamento=new[]{new CassaTipoAbbonamento{Id=100,Nome="Dieci",Prezzo=50,NumeroIngressi=10,DurataGiorni=30}}});
            if(request.Action==CassaAction.FindCard)return Task.FromResult(new CassaResult{Sospesi=Collections==0?[new CassaSospeso{Id=7,Descrizione="Abbonamento Dieci",CanIncassare=true,Importo=50,Origine="Abbonamento",DataOra=DateTime.Now}]:[],Socio=new(){PersonId=request.NumeroTessera=="T100"?100:101,NumeroTessera=request.NumeroTessera,Cognome="Rossi",Nome="Mario",ScadenzaTessera=20991231,SospesiAperti=Collections==0?1:0,TotaleSospesi=Collections==0?50:0}});
            if(request.Action==CassaAction.LoadSospesi)return Task.FromResult(new CassaResult{Socio=string.IsNullOrEmpty(request.NumeroTessera)?null:new CassaSocio{PersonId=request.NumeroTessera=="T100"?100:101,NumeroTessera=request.NumeroTessera},Sospesi=Collections==0?new[]{new CassaSospeso{Id=7,PersonId=100,Nominativo="Rossi Mario",Descrizione="Abbonamento",Tipo="Credito",Origine="Abbonamento",Importo=50,CanIncassare=true,DataOra=DateTime.Now}}:[]});
                        if(request.Action==CassaAction.SaldaSospeso)
            {
                if(OmitConfirmation){OmitConfirmation=false;return Task.FromResult(new CassaResult());}
                Collections++;return Task.FromResult(new CassaResult{SospesoSaldatoId=request.SospesoId,SociConSospesi=new[]{new CassaSocio{PersonId=100},new CassaSocio{PersonId=101}}});
            }
            return Task.FromResult(new CassaResult{PostazioneName="Cassa test",AbbonamentiDisponibili=Available});
        }
    }
}
