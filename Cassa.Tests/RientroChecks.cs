using System.Text.Json;
using AppServices.Database;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cassa.Core.Repository;
using Cassa.ViewModels;
using Cassa.Views;
using Leonardo18.DatabaseHost.Cassa;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Models.Context;
using Models.Sospesi;
using Models.Tables;
using ReactiveUI;
using ReactiveUI.Primitives;

internal static class RientroChecks
{
    public static int Count;
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Count++;}
    private static async Task Reject(Func<Task> action,string text)
    {
        try{await action();}catch(InvalidOperationException e)when(e.Message.Contains(text,StringComparison.OrdinalIgnoreCase)){Count++;return;}
        throw new Exception("Rientro non rifiutato: "+text);
    }
    public static async Task Database()
    {
        using(var sql=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=localhost;Database=RientroSchemaTest;Integrated Security=true;TrustServerCertificate=true").Options))
        {
            Check(!sql.Database.HasPendingModelChanges(),"Snapshot rientri coerente con il modello");
            var migrations=sql.GetService<IMigrationsAssembly>();var latest=migrations.Migrations.Single(x=>x.Key.EndsWith("_AddRientri"));
            Check(latest.Key.EndsWith("_AddRientri"),"Migrazione rientri aggiuntiva");
            var migration=migrations.CreateMigration(latest.Value,sql.Database.ProviderName!);
            Check(migration.UpOperations.OfType<AddColumnOperation>().Count()==4&&!migration.UpOperations.Any(x=>x is DropTableOperation or DropColumnOperation or DeleteDataOperation),"Aggiornamento rientri conserva dati preesistenti");
        }
        var fixedNow=new DateTime(2026,10,17,2,0,0);var origin=fixedNow.AddHours(-3);
        Check(CassaRientroRules.Disponibile(100,3,origin,1,2,fixedNow.Date,fixedNow),"Durata ore include il limite esatto e attraversa la mezzanotte");
        Check(!CassaRientroRules.Disponibile(100,3,origin,1,2,fixedNow.Date,fixedNow.AddTicks(1)),"Oltre DurataOre il rientro scade");
        Check(CassaRientroRules.Disponibile(-2,0,origin,2,2,fixedNow.AddHours(-4),fixedNow),"Giornata aperta valida oltre la mezzanotte");
        Check(!CassaRientroRules.Disponibile(-2,0,fixedNow.AddMinutes(-1),1,2,fixedNow.Date,fixedNow),"Giornata diversa non valida anche nella stessa data");
        Check(!CassaRientroRules.Disponibile(-1,3,origin,2,2,fixedNow.Date,fixedNow),"Nessuno non abilita rientri");
        Check(!CassaRientroRules.Disponibile(100,3,fixedNow.AddMinutes(1),2,2,fixedNow.Date,fixedNow),"Ingresso futuro non abilita rientro");
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        var options=new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;var now=DateTime.Now;
        await using(var db=new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.TipiRientro.Add(new(){Id=100,Nome="Tre ore",DurataOre=3});
            db.Postazioni.AddRange(new Postazione{Id=100,Nome="Giornata",TipoPostazioneId=2,TipoRientroId=-2},new Postazione{Id=101,Nome="Ore",TipoPostazioneId=2,TipoRientroId=100},new Postazione{Id=102,Nome="Nessuno",TipoPostazioneId=2,TipoRientroId=-1});
            db.Operatori.Add(new(){Id=100,Nome="Operatore",PersonId=-1,Abilitato=true});
            foreach(var id in new[]{100,101,102})db.Permessi.Add(new(){OperatoreId=100,PostazioneId=id});
            db.Giornate.Add(new(){Id=100,Aperta=true,DataInizio=now.Date.AddDays(-1).AddHours(20),DataFine=DateTime.MaxValue});
            for(var id=100;id<=107;id++)db.People.Add(new(){Id=id,FirstName="Socio",SurName="Rientri"+id,UniqueParam="RIENTRO"+id,Natoil=19900101,
                Soci=[new(){NumeroSocio="S"+id,Tessere=[new(){NumeroTessera="T"+id,Scadenza=20991231,Abilitato=true}]}]});
            SchedaUscita Archive(int id,int person,DateTime at,bool reentry=false,int? day=100)=>new(){Id=id,IdOperazione=Guid.NewGuid(),VersioneConto="test",PersonId=person,NumeroTessera="T"+person,Posizione="OLD"+id,CheckinTime=at,CheckoutTime=now.AddMinutes(-5),IsRientro=reentry,GiornataIngressoId=day,OperatoreId=100,PostazioneId=100,GiornataId=100};
            db.SchedeUscite.AddRange(Archive(100,100,now.Date.AddMinutes(-30)),Archive(101,101,now.AddHours(-1),day:99),
                Archive(102,102,now.AddHours(-2)),Archive(202,102,now.AddHours(-8)),Archive(103,103,now.AddHours(-4)),
                Archive(203,103,now.AddMinutes(-10),true),Archive(104,104,now.AddMinutes(-30),day:null),Archive(106,106,now.AddHours(-1)));
            db.Sospesi.Add(new(){IdOperazione=Guid.NewGuid(),Origine=OrigineSospeso.Manuale,PersonId=106,Importo=5,Descrizione="Da saldare",Nominativo="Socio",DataOra=now,DataCompetenza=now.Date});
            await db.SaveChangesAsync();
        }
        var repo=new CassaDatabaseRepository(()=>new AppDbContext(options));
        CassaRequest Find(int person,int station=100)=>new(){Action=CassaAction.FindCard,PostazioneId=station,NumeroTessera="T"+person};
        CassaRequest Enter(int person,int station=100)=>new(){Action=CassaAction.RegisterEntry,PostazioneId=station,NumeroTessera="T"+person,Posizione="R"+person,Rientro=true};
        var result=await repo.Execute(100,Find(100));Check(result.Rientro?.UltimoIngresso==now.Date.AddMinutes(-30)&&result.Rientro.ValidoFino is null,"Rientro giornata dalla giornata di ingresso");
        Check((await repo.Execute(100,Find(101))).Rientro is null,"Ingresso di un'altra giornata non abilita rientro");
        Check((await repo.Execute(100,Find(102,101))).Rientro?.UltimoIngresso==now.AddHours(-2),"Ore calcolate sull'ultimo ingresso ordinario");
        Check((await repo.Execute(100,Find(103,101))).Rientro is null,"Rientro recente non prolunga l'ingresso originario scaduto");
        Check((await repo.Execute(100,Find(104))).Rientro is not null,"Ingresso storico senza giornata esplicita riconosciuto dall'apertura");
        Check((await repo.Execute(100,Find(105))).Rientro is null,"Primo ingresso senza storico");
        Check((await repo.Execute(100,Find(100,102))).Rientro is null,"Regola della postazione corrente Nessuno");
        await Reject(()=>repo.Execute(100,Enter(105)),"non più disponibile");
        await Reject(()=>repo.Execute(100,Enter(106)),"sospesi");
        var ordinary=Enter(100);ordinary.Rientro=false;await Reject(()=>repo.Execute(100,ordinary),"diritto al rientro");
        var mixed=Enter(100);mixed.AbbonamentoIngressoId=1;mixed.NumeroIngressoAbbonamento=1;await Reject(()=>repo.Execute(100,mixed),"solo il rientro");
        result=await repo.Execute(100,Enter(102,101));var sheetId=result.Scheda!.Id;
        await using(var db=new AppDbContext(options))
        {
            var sheet=await db.Schede.Include(x=>x.SchedeConto).SingleAsync();
            Check(sheet.IsRientro&&sheet.GiornataIngressoId==100&&sheet.SchedeConto.Single() is {Pagato:true,VoicePrice:0,VoiceDesc:"RIENTRO GRATUITO"},"Rientro gratuito salvato con giornata e tipo accesso");
            Check(!await db.ContabilitaMovimenti.AnyAsync()&&!await db.AbbonamentoConti.AnyAsync(),"Rientro senza incassi né consumo abbonamento");
        }
        await Reject(()=>repo.Execute(100,Enter(102,101)),"non più disponibile");
        var prepared=await repo.Execute(100,new(){Action=CassaAction.PrepareUscita,PostazioneId=101,Posizione="R102"});
        await repo.Execute(100,new(){Action=CassaAction.RegisterUscita,PostazioneId=101,Posizione="R102",SchedaId=sheetId,UscitaId=Guid.NewGuid(),VersioneConto=prepared.Uscita!.VersioneConto,Pagato=true});
        await using(var db=new AppDbContext(options))Check((await db.SchedeUscite.SingleAsync(x=>x.Id==sheetId)) is {IsRientro:true,GiornataIngressoId:100},"Uscita conserva la distinzione del rientro");
        result=await repo.Execute(100,Find(102,101));Check(result.Rientro?.UltimoIngresso==now.AddHours(-2),"Secondo rientro riferito allo stesso ingresso originale");
        await using(var db=new AppDbContext(options))
        {
            var original=await db.SchedeUscite.SingleAsync(x=>x.Id==102);original.CheckinTime=now.AddHours(-4);
            await db.SaveChangesAsync();
        }
        await Reject(()=>repo.Execute(100,Enter(102,101)),"non più disponibile");
        await using(var db=new AppDbContext(options))
        {
            var old=await db.Giornate.SingleAsync(x=>x.Id==100);old.Aperta=false;old.DataFine=DateTime.Now;
            db.Giornate.Add(new(){Id=101,Aperta=true,DataInizio=DateTime.Now,DataFine=DateTime.MaxValue});await db.SaveChangesAsync();
        }
        Check((await repo.Execute(100,Find(100))).Rientro is null,"Chiusura giornata estingue il rientro giornaliero");
        await Reject(()=>repo.Execute(100,Enter(100)),"non più disponibile");
        result.Rientro=new(){Tipo="Ore",UltimoIngresso=now.AddHours(-2),ValidoFino=now.AddHours(1),GiornataId=101};
        var json=JsonSerializer.Serialize(new DatabaseHostResponse{Success=true,Cassa=result},DatabaseProtocolJsonContext.Default.DatabaseHostResponse);
        Check(JsonSerializer.Deserialize(json,DatabaseProtocolJsonContext.Default.DatabaseHostResponse)?.Cassa?.Rientro?.UltimoIngresso==now.AddHours(-2),"Protocollo trasporta diritto e scadenza del rientro");
    }
    public static void UI()
    {
        var repo=new Fake();var vm=new CassaViewModel(new Screen(),repo,100);var view=new CassaView{ViewModel=vm};
        var window=new Window{Width=1200,Height=800,Content=view};window.Show();
        void Pump(Func<bool> done){var end=DateTime.UtcNow.AddSeconds(15);while(!done()){Dispatcher.UIThread.RunJobs();Thread.Sleep(1);if(DateTime.UtcNow>end)throw new TimeoutException("Test rientro UI");}}
        void Execute(ReactiveCommand<RxVoid,RxVoid> command){var done=false;using var sub=command.Execute().SubscribePrimitives(_=>{},ex=>throw ex,()=>done=true);Pump(()=>done);Dispatcher.UIThread.RunJobs();}
        Pump(()=>vm.IsUiReady);Execute(vm.EntraSocioCommand);vm.NumeroTessera="T100";Execute(vm.FindCardCommand);vm.EntryPosition="R100";
        Check(vm.HasRientro&&vm.Ingressi.Count==1&&vm.SelectedIngresso is {Prezzo:0,Etichetta:"RIENTRO GRATUITO"},"EntraSocio propone solo rientro gratuito");
        Check(!vm.HasAbbonamentiIngresso,"Rientro nasconde anche i bollini abbonamento");
        Check(view.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.IsEffectivelyVisible&&x.Text?.StartsWith("RIENTRO GRATUITO ·")==true),"Indicazione del rientro visibile");
        using(var frame=window.CaptureRenderedFrame())
        {
            var path=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","previews","ingresso-rientro.png"));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);frame!.Save(path,Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        }
        Execute(vm.RegisterEntryCommand);Check(repo.LastEntry is {Rientro:true,TariffaId:0,AbbonamentoIngressoId:0},"Conferma invia solo la scelta rientro");
        Execute(vm.EntraSocioCommand);vm.NumeroTessera="T101";Execute(vm.FindCardCommand);
        Check(!vm.HasRientro&&vm.Ingressi.Single().Id==100,"Cambio socio ripristina ingressi ordinari");
        window.Close();Dispatcher.UIThread.RunJobs();
    }
    private sealed class Screen:ReactiveObject,IScreen{public RoutingState Router{get;}=new();}
    private sealed class Fake:ICassaRepository
    {
        public CassaRequest? LastEntry;
        public Task<CassaResult> Execute(CassaRequest r,CancellationToken ct=default)=>Task.FromResult(Reply(r));
        private CassaResult Reply(CassaRequest r)
        {
            if(r.Action==CassaAction.PrepareEntry)return new(){Ingressi=[new(){Id=100,Etichetta="Ordinario",Prezzo=10}]};
            if(r.Action==CassaAction.FindCard)return new(){Socio=new(){PersonId=100,Nome="Mario",Cognome="Rossi",NumeroTessera=r.NumeroTessera,ScadenzaTessera=20991231},Rientro=r.NumeroTessera=="T100"?new(){Tipo="Giornata",UltimoIngresso=DateTime.Now.AddHours(-1),GiornataId=100}:null,AbbonamentiIngresso=[new(){Id=1,Nome="Tre ingressi",Bollini=[new(){AbbonamentoId=1,Numero=1}]}]};
            if(r.Action==CassaAction.RegisterEntry){LastEntry=r;return new(){Scheda=new(){Id=100,Posizione=r.Posizione}};}
            if(r.Action==CassaAction.OpenSheet)return new(){Scheda=new(){Id=100,Posizione=r.Posizione}};
            return new(){PostazioneName="Cassa"};
        }
    }
}
