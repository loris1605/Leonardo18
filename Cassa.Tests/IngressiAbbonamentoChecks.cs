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
using Microsoft.EntityFrameworkCore.Diagnostics;
using Models.Context;
using Models.Sospesi;
using Models.Tables;
using ReactiveUI;
using ReactiveUI.Primitives;

internal static class IngressiAbbonamentoChecks
{
    public static int Count;
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Count++;}
    private static async Task Reject(Func<Task> action,string text)
    {
        try{await action();}catch(InvalidOperationException e)when(e.Message.Contains(text,StringComparison.OrdinalIgnoreCase)){Count++;return;}
        throw new Exception("Ingresso non rifiutato: "+text);
    }
    public static async Task Database()
    {
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        var options=new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using(var db=new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Postazioni.Add(new(){Id=100,Nome="Cassa bollini",TipoPostazioneId=2,TipoRientroId=-1});
            db.Operatori.Add(new(){Id=100,Nome="Operatore",PersonId=-1,Abilitato=true});
            db.Permessi.Add(new(){OperatoreId=100,PostazioneId=100});
            db.Giornate.Add(new(){Id=100,Aperta=true,DataInizio=DateTime.Today,DataFine=DateTime.MaxValue});
            for(var id=100;id<104;id++)db.People.Add(new(){Id=id,FirstName="Socio",SurName="Bollini"+id,UniqueParam="BOLL"+id,Natoil=19900101,
                Soci=[new(){NumeroSocio="S"+id,Tessere=[new(){NumeroTessera="T"+id,Scadenza=20991231,Abilitato=true}]}]});
            db.TipiAbbonamento.Add(new(){Id=100,Nome="Tre ingressi",NumeroIngressi=3,DurataGiorni=30,Prezzo=30,Attivo=true});
            for(var id=100;id<=104;id++)db.Abbonamenti.Add(new(){Id=id,Nome="Tessera "+id,TipoAbbonamentoId=100,PersonId1=id==104?102:100,PersonId2=id==100?101:null,
                Attivo=true,DataInizio=DateTime.Today.AddDays(-2),DataFine=id==102?DateTime.Today.AddDays(-1):DateTime.Today.AddDays(28),
                IngressiAcquistati=3,PrezzoAcquisto=30,PagatoAllAcquisto=id!=101,AcquistoId=Guid.NewGuid()});
            db.AbbonamentoConti.Add(new(){AbbonamentoId=100,NumeroIngresso=1,Entrato=true,DataIngresso=DateTime.Today.AddDays(-1),Nome="Ingresso 1"});
            db.Sospesi.Add(new(){IdOperazione=Guid.NewGuid(),Origine=OrigineSospeso.Abbonamento,AbbonamentoId=103,PersonId=100,Importo=30,Descrizione="Non saldato",Nominativo="Socio",DataOra=DateTime.Now,DataCompetenza=DateTime.Today});
            await db.SaveChangesAsync();
        }
        // Pending of any origin continues to block entry; remove the seeded inconsistent credit after checking eligibility.
        var repo=new CassaDatabaseRepository(()=>new AppDbContext(options));
        CassaRequest Find(string card="T100")=>new(){Action=CassaAction.FindCard,PostazioneId=100,NumeroTessera=card};
        CassaRequest Enter(int subscription=100,int number=2,string card="T100",string position="B01")=>new(){Action=CassaAction.RegisterEntry,PostazioneId=100,NumeroTessera=card,Posizione=position,AbbonamentoIngressoId=subscription,NumeroIngressoAbbonamento=number};
        var result=await repo.Execute(100,Find());
        Check(result.AbbonamentiIngresso.Length==1&&result.AbbonamentiIngresso[0].Id==100,"Solo abbonamento attivo, pagato, proprio e senza sospeso");
        Check(result.AbbonamentiIngresso[0] is {Disponibili:2}&&result.AbbonamentiIngresso[0].Bollini[0].DataIngresso==DateTime.Today.AddDays(-1),"Bollini usati datati e disponibili");
        await Reject(()=>repo.Execute(100,Enter()),"sospesi");
        await using(var db=new AppDbContext(options)){db.Sospesi.RemoveRange(db.Sospesi);await db.SaveChangesAsync();}
        await Reject(()=>repo.Execute(100,Enter(101)),"non disponibile");
        await Reject(()=>repo.Execute(100,Enter(102)),"non disponibile");
        await Reject(()=>repo.Execute(100,Enter(104)),"non disponibile");
        await Reject(()=>repo.Execute(100,Enter(number:4)),"non valido");
        var mixed=Enter();mixed.TariffaId=123;await Reject(()=>repo.Execute(100,mixed),"solo tipo");
        var failOptions=new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(new FailSheet()).Options;
        var failing=new CassaDatabaseRepository(()=>new AppDbContext(failOptions));
        await Reject(()=>failing.Execute(100,Enter()),"Errore simulato");
        await using(var db=new AppDbContext(options))Check(!await db.AbbonamentoConti.AnyAsync(x=>x.AbbonamentoId==100&&x.NumeroIngresso==2)&&!await db.Schede.AnyAsync(),"Errore ingresso non consuma il bollino");
        result=await repo.Execute(100,Enter());Check(result.Scheda?.Posizione=="B01","Ingresso abbonamento confermato");
        await using(var db=new AppDbContext(options))
        {
            Check(await db.AbbonamentoConti.CountAsync(x=>x.AbbonamentoId==100&&x.NumeroIngresso==2&&x.Entrato)==1,"Bollino consumato una volta");
            Check((await db.SchedeConto.SingleAsync()).VoicePrice==0&&!await db.ContabilitaMovimenti.AnyAsync(),"Nessun nuovo incasso per ingresso già pagato");
        }
        await Reject(()=>repo.Execute(100,Enter(card:"T101",position:"B02")),"già stato usato");
        result=await repo.Execute(100,Find("T101"));Check(result.AbbonamentiIngresso.Single().Disponibili==1,"Abbonamento condiviso aggiornato per il secondo socio");
        await repo.Execute(100,Enter(number:3,card:"T101",position:"B02"));
        result=await repo.Execute(100,Find("T101"));Check(result.AbbonamentiIngresso.Single().Disponibili==0,"Tessera esaurita mostra solo bollini usati");
        await using(var db=new AppDbContext(options))
        {
            var unpaid=await db.Abbonamenti.SingleAsync(x=>x.Id==101);
            db.Sospesi.Add(new(){IdOperazione=unpaid.AcquistoId!.Value,Origine=OrigineSospeso.Abbonamento,AbbonamentoId=101,PersonId=100,NumeroTessera="T100",Importo=30,Descrizione="Da saldare",Nominativo="Socio",DataOra=DateTime.Now,DataCompetenza=DateTime.Today});
            await db.SaveChangesAsync();
        }
        long pendingId;await using(var db=new AppDbContext(options))pendingId=(await db.Sospesi.SingleAsync()).Id;
        var settled=await repo.Execute(100,new(){Action=CassaAction.SaldaSospeso,PostazioneId=100,NumeroTessera="T100",SospesoId=pendingId});
        Check(settled.AbbonamentiIngresso.Any(x=>x.Id==101&&x.Disponibili==3),"Abbonamento inizialmente sospeso disponibile subito dopo il saldo");
        var json=JsonSerializer.Serialize(new DatabaseHostResponse{Success=true,Cassa=result},DatabaseProtocolJsonContext.Default.DatabaseHostResponse);
        Check(JsonSerializer.Deserialize(json,DatabaseProtocolJsonContext.Default.DatabaseHostResponse)?.Cassa?.AbbonamentiIngresso.Single().Bollini.All(x=>x.Usato)==true,"Protocollo trasporta bollini e date");
        var request=Enter();json=JsonSerializer.Serialize(new DatabaseHostRequest{Cassa=request},DatabaseProtocolJsonContext.Default.DatabaseHostRequest);
        Check(JsonSerializer.Deserialize(json,DatabaseProtocolJsonContext.Default.DatabaseHostRequest)?.Cassa is {AbbonamentoIngressoId:100,NumeroIngressoAbbonamento:2},"Protocollo trasporta il bollino scelto");
    }
    private sealed class FailSheet:SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        {if(data.Context!.ChangeTracker.Entries<Scheda>().Any(x=>x.State==EntityState.Added))throw new InvalidOperationException("Errore simulato");return ValueTask.FromResult(result);}
    }
    public static void UI()
    {
        var repo=new Fake();var vm=new CassaViewModel(new Screen(),repo,100);var view=new CassaView{ViewModel=vm};
        var window=new Window{Width=1200,Height=900,Content=view};window.Show();
        void Pump(Func<bool> done){var end=DateTime.UtcNow.AddSeconds(15);while(!done()){Dispatcher.UIThread.RunJobs();Thread.Sleep(1);if(DateTime.UtcNow>end)throw new TimeoutException("Test bollini UI");}}
        void Execute(ReactiveCommand<RxVoid,RxVoid> command){var done=false;using var sub=command.Execute().SubscribePrimitives(_=>{},ex=>throw ex,()=>done=true);Pump(()=>done);Dispatcher.UIThread.RunJobs();}
        Pump(()=>vm.IsUiReady);Execute(vm.EntraSocioCommand);vm.NumeroTessera="T100";Execute(vm.FindCardCommand);vm.EntryPosition="B01";
        Check(vm.AbbonamentiIngresso.Single().Bollini.Length==3,"Tessera a tre bollini in EntraSocio");
        var used=vm.AbbonamentiIngresso.Single().Bollini[0];var available=vm.AbbonamentiIngresso.Single().Bollini[1];
        Check(!used.Disponibile&&used.Stato.Contains(DateTime.Today.AddDays(-1).ToString("dd/MM/yyyy")),"Bollino usato disabilitato e datato");
        var buttons=view.GetVisualDescendants().OfType<Button>().Where(x=>x.DataContext is BollinoIngressoViewModel).ToArray();
        Check(buttons.Length==3&&buttons.Where(x=>ReferenceEquals(x.DataContext,used)).All(x=>!x.IsEffectivelyEnabled),"Grafica bollini e blocco bollini usati");
        var choose=buttons.Single(x=>ReferenceEquals(x.DataContext,available));
        ((System.Windows.Input.ICommand)choose.Command!).Execute(choose.CommandParameter);Dispatcher.UIThread.RunJobs();
        Check(available.Selezionato&&vm.SelectedIngresso is null&&((System.Windows.Input.ICommand)vm.RegisterEntryCommand).CanExecute(null),"Bollino selezionato abilita ingresso senza tariffa");
        using(var frame=window.CaptureRenderedFrame())
        {
            Check(frame is not null,"Anteprima tessera disponibile");
            var path=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","previews","ingresso-abbonamento.png"));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);frame!.Save(path,Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        }
        vm.SelectedIngresso=vm.Ingressi.Single();Check(!available.Selezionato,"Scelta tariffa annulla bollino");
        ((System.Windows.Input.ICommand)choose.Command!).Execute(choose.CommandParameter);Dispatcher.UIThread.RunJobs();
        Execute(vm.RegisterEntryCommand);Check(repo.LastEntry is {AbbonamentoIngressoId:100,NumeroIngressoAbbonamento:2,TariffaId:0},"Conferma invia esclusivamente il bollino selezionato");
        Execute(vm.EntraSocioCommand);vm.NumeroTessera="T101";Execute(vm.FindCardCommand);
        Check(vm.AbbonamentiIngresso.Count==0,"Cambio socio non conserva tessera precedente");
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
            if(r.Action==CassaAction.FindCard)return new(){Socio=new(){PersonId=100,NumeroTessera=r.NumeroTessera,ScadenzaTessera=20991231},AbbonamentiIngresso=r.NumeroTessera=="T100"?[new(){Id=100,Nome="Tre ingressi",DataFine=DateTime.Today.AddDays(30),Bollini=[new(){AbbonamentoId=100,Numero=1,Usato=true,DataIngresso=DateTime.Today.AddDays(-1)},new(){AbbonamentoId=100,Numero=2},new(){AbbonamentoId=100,Numero=3}]}]:[]};
            if(r.Action==CassaAction.RegisterEntry){LastEntry=r;return new(){Scheda=new(){Id=100,Posizione=r.Posizione}};}
            if(r.Action==CassaAction.OpenSheet)return new(){Scheda=new(){Id=100,Posizione=r.Posizione}};
            return new(){PostazioneName="Cassa"};
        }
    }
}
