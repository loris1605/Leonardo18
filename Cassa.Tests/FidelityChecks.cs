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
using Leonardo18.DatabaseHost.Servizi;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Models.Context;
using Models.Sospesi;
using Models.Tables;
using ReactiveUI;
using ReactiveUI.Primitives;
using Servizi.Core.Repository;
using Servizi.ViewModels;
using Servizi.Views;

internal static class FidelityChecks
{
    public static int Count;
    private static void Check(bool value,string text){if(!value)throw new Exception(text);Count++;}
    private static async Task Reject(Func<Task> action,string text)
    {try{await action();}catch(InvalidOperationException e)when(e.Message.Contains(text,StringComparison.OrdinalIgnoreCase)){Count++;return;}throw new Exception("Fidelity non rifiutata: "+text);}
    public static async Task Database()
    {
        using(var sql=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=localhost;Database=FidelitySchemaTest;Integrated Security=true;TrustServerCertificate=true").Options))
            Check(!sql.Database.HasPendingModelChanges(),"Snapshot Fidelity coerente");
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        var options=new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using(var db=new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Postazioni.AddRange(new Postazione{Id=100,Nome="Cassa Fidelity",TipoPostazioneId=2,TipoRientroId=-1},new Postazione{Id=200,Nome="Admin",TipoPostazioneId=1,TipoRientroId=-1});
            db.Operatori.AddRange(new Operatore{Id=100,Nome="Admin",PersonId=-1,Abilitato=true},new Operatore{Id=101,Nome="Cassa",PersonId=-1,Abilitato=true});
            db.Permessi.AddRange(new Permesso{OperatoreId=100,PostazioneId=100},new Permesso{OperatoreId=100,PostazioneId=200},new Permesso{OperatoreId=101,PostazioneId=100});
            db.Giornate.Add(new(){Id=100,Aperta=true,DataInizio=DateTime.Today,DataFine=DateTime.MaxValue});
            db.Settori.AddRange(new Settore{Id=100,Nome="Ingresso",TipoSettoreId=-1},new Settore{Id=101,Nome="Bar",TipoSettoreId=-2});
            db.Reparti.AddRange(new Reparto{PostazioneId=100,SettoreId=100},new Reparto{PostazioneId=100,SettoreId=101});
            db.Tariffe.AddRange(new Tariffa{Id=100,Nome="Ordinario",Label="Ordinario",Prezzo=10},new Tariffa{Id=101,Nome="Omaggio",Label="Omaggio",Prezzo=0},new Tariffa{Id=102,Nome="Acqua",Label="Acqua",Prezzo=5},new Tariffa{Id=103,Nome="Acquisto",Label="Acquisto",Prezzo=10});
            db.Listini.AddRange(new Listino{Id=100,TariffaId=100,SettoreId=100},new Listino{Id=101,TariffaId=101,SettoreId=100},new Listino{Id=102,TariffaId=102,SettoreId=101},new Listino{Id=103,TariffaId=103,SettoreId=101});
            for(var id=100;id<=105;id++)db.People.Add(new(){Id=id,FirstName="Mario",SurName="Rossi"+id,UniqueParam="FID"+id,Natoil=19900101,Soci=[new(){NumeroSocio="S"+id,Tessere=[new(){NumeroTessera="T"+id,Scadenza=20991231,Abilitato=true}]}]});
            db.TipiAbbonamento.Add(new(){Id=100,Nome="Tre ingressi",NumeroIngressi=3,DurataGiorni=30,Prezzo=30,Attivo=true});
            db.Abbonamenti.Add(new(){Id=100,Nome="Tre ingressi",TipoAbbonamentoId=100,PersonId1=100,Attivo=true,DataInizio=DateTime.Today,DataFine=DateTime.Today.AddDays(30),IngressiAcquistati=3,PagatoAllAcquisto=true,PrezzoAcquisto=30});
            await db.SaveChangesAsync();
        }
        var services=new ServiziDatabaseRepository(()=>new AppDbContext(options));var repo=new CassaDatabaseRepository(()=>new AppDbContext(options));
        ServiziRequest Config(string name,int prize,decimal value=0)=>new(){Action=ServiziAction.Add,Section=ServiziSection.Fidelity,Record=new(){Nome=name,NumeroIngressi=2,DurataGiorni=0,TipoPremioId=prize,Prezzo=value,Attivo=true}};
        await Reject(()=>services.Execute(101,Config("Non autorizzato",1)),"amministratore");
        var invalid=Config("Non valida",1);invalid.Record!.NumeroIngressi=0;await Reject(()=>services.Execute(100,invalid),"Verifica");
        await Reject(()=>services.Execute(100,Config("Sconto vuoto",2)),"Verifica");
        var configured=await services.Execute(100,Config("Ingresso premio",1));var freeType=configured.SelectedId;
        configured=await services.Execute(100,Config("Premio sconto",2,7.50m));var moneyType=configured.SelectedId;
        Check(configured.Records.Length==2&&configured.Records.Single(x=>x.Id==moneyType).Prezzo==7.50m,"Servizi configura traguardo e due tipi di premio");
        CassaRequest Find(int person=100)=>new(){Action=CassaAction.FindCard,PostazioneId=100,NumeroTessera="T"+person};
        CassaRequest Activate(int person,int type)=>new(){Action=CassaAction.ActivateFidelity,PostazioneId=100,NumeroTessera="T"+person,PersonId1=person,TipoFidelityId=type};
        CassaRequest Enter(int person,int tariff=100)=>new(){Action=CassaAction.RegisterEntry,PostazioneId=100,NumeroTessera="T"+person,Posizione="F"+person,TariffaId=tariff};
        async Task Exit(int person)
        {
            var ready=await repo.Execute(100,new(){Action=CassaAction.PrepareUscita,PostazioneId=100,Posizione="F"+person});
            await repo.Execute(100,new(){Action=CassaAction.RegisterUscita,PostazioneId=100,Posizione="F"+person,SchedaId=ready.Scheda!.Id,VersioneConto=ready.Uscita!.VersioneConto,UscitaId=Guid.NewGuid(),Pagato=true});
        }
        Check((await repo.Execute(100,Find())).Fidelity is null,"Fidelity non attivata automaticamente");
        await repo.Execute(100,Enter(103,101));await Exit(103);
        await using(var db=new AppDbContext(options))Check(!await db.FidelityEntries.AnyAsync(),"Nessun timbro prima di attivazione");
        var result=await repo.Execute(100,Activate(100,freeType));var freeId=result.Fidelity!.Id;
        Check(result.Fidelity is {Attiva:true,Punti:0,CanIngresso:false},"Attivazione esplicita senza punti regalati");
        await repo.Execute(100,Activate(100,freeType));
        await using(var db=new AppDbContext(options))Check((await db.Fidelities.SingleAsync()).Ciclo==1,"Riprova attivazione non azzera e non duplica la raccolta");
        var edited=configured.Records.Single(x=>x.Id==freeType).Copy();edited.NumeroIngressi=9;
        await services.Execute(100,new(){Action=ServiziAction.Update,Section=ServiziSection.Fidelity,RecordId=freeType,Record=edited});
        Check((await repo.Execute(100,Find())).Fidelity!.Traguardo==2,"Configurazione successiva non altera raccolta attiva");
        await Reject(()=>services.Execute(100,new(){Action=ServiziAction.Delete,Section=ServiziSection.Fidelity,RecordId=freeType}),"utilizzata");
        await repo.Execute(100,Enter(100));await Exit(100);
        result=await repo.Execute(100,Find());Check(result.Fidelity is {Punti:1,CanIngresso:false}&&result.Fidelity.Timbri.Single().DataOra is not null,"Un timbro con data e ora per ingresso ordinario");
        var subscription=Enter(100,0);subscription.AbbonamentoIngressoId=100;subscription.NumeroIngressoAbbonamento=1;
        await repo.Execute(100,subscription);await Exit(100);
        result=await repo.Execute(100,Find());Check(result.Fidelity is {Punti:2,CanIngresso:true}&&result.Fidelity.Timbri.Length==2,"Ingresso abbonamento timbra e raggiunge premio");
        var reward=Enter(100,0);reward.IngressoFidelityId=freeId;
        var entered=await repo.Execute(100,reward);
        await using(var db=new AppDbContext(options))Check((await db.SchedeConto.SingleAsync(x=>x.SchedaId==entered.Scheda!.Id)) is {VoicePrice:0,VoiceDesc:"INGRESSO FIDELITY"},"Premio ingresso gratuito registrato");
        Check(entered.Fidelity is {Punti:1}&&entered.Fidelity.Timbri.Length==3,"Ingresso Fidelity consuma premio e riceve un solo nuovo timbro");
        await Reject(()=>repo.Execute(100,reward),"già all'interno");
        await using(var db=new AppDbContext(options))Check(await db.FidelityEntries.CountAsync(x=>x.TipoMovimento==1)==3,"Riprova ingresso non duplica timbri");
        await Exit(100);
        await using(var db=new AppDbContext(options)){(await db.Postazioni.SingleAsync(x=>x.Id==100)).TipoRientroId=-2;await db.SaveChangesAsync();}
        var reentry=Enter(100,0);reentry.Rientro=true;await repo.Execute(100,reentry);await Exit(100);
        Check((await repo.Execute(100,Find())).Fidelity!.Timbri.Length==3,"Rientro escluso dai timbri");
        await using(var db=new AppDbContext(options)){(await db.Postazioni.SingleAsync(x=>x.Id==100)).TipoRientroId=-1;await db.SaveChangesAsync();}
        await repo.Execute(100,Activate(101,moneyType));
        var wrong=Enter(101,0);wrong.IngressoFidelityId=freeId;await Reject(()=>repo.Execute(100,wrong),"non disponibile");
        await repo.Execute(100,Enter(101));await Exit(101);var moneyEntry=await repo.Execute(100,Enter(101,101));
        Check(moneyEntry.Fidelity is {Punti:0,CreditoSconto:7.50m,CanIngresso:false},"Traguardo denaro matura credito senza creare ingresso omaggio");
        CassaRequest Purchase(int list)=>new(){Action=CassaAction.AddCharge,PostazioneId=100,Posizione="F101",SchedaId=moneyEntry.Scheda!.Id,ListinoId=list,UsaCreditoFidelity=true};
        result=await repo.Execute(100,Purchase(102));var zero=result.Conto.Single(x=>x.VoiceDesc=="Acqua");
        Check(zero.VoicePrice==0&&zero.CanStornare&&result.Fidelity?.CreditoSconto==2.50m,"Sconto fino al prezzo acquisto conserva residuo e storno disponibile");
        var reverse=new CassaRequest{Action=CassaAction.StornaConto,PostazioneId=100,Posizione="F101",SchedaId=moneyEntry.Scheda!.Id,ContoId=zero.Id};
        result=await repo.Execute(100,reverse);Check(result.Fidelity?.CreditoSconto==7.50m,"Storno acquisto interamente scontato ripristina credito");
        result=await repo.Execute(100,reverse);Check(result.Fidelity?.CreditoSconto==7.50m,"Storno ripetuto non duplica credito");
        result=await repo.Execute(100,Purchase(103));var partial=result.Conto.Single(x=>x.VoiceDesc=="Acquisto");
        Check(partial.VoicePrice==2.50m&&result.Fidelity?.CreditoSconto==0,"Sconto parziale registra solo importo netto dovuto");
        reverse.ContoId=partial.Id;result=await repo.Execute(100,reverse);Check(result.Fidelity?.CreditoSconto==7.50m,"Storno acquisto parziale ripristina credito e contabilità");
        await Exit(101);
        await using(var db=new AppDbContext(options)){(await db.Fidelities.SingleAsync(x=>x.PersonId==101)).ScadeIl=DateTime.Today.AddDays(-1);await db.SaveChangesAsync();}
        var discountedEntry=Enter(101);discountedEntry.UsaCreditoFidelity=true;entered=await repo.Execute(100,discountedEntry);
        Check(entered.Fidelity is {Attiva:false,CreditoSconto:0}&&entered.Fidelity.Timbri.Length==2,"Credito già maturato spendibile dopo scadenza, senza nuovi timbri");
        await using(var db=new AppDbContext(options))Check((await db.SchedeConto.SingleAsync(x=>x.SchedaId==entered.Scheda!.Id)).VoicePrice==2.50m,"Credito applicato anche all'ingresso ordinario");
        result=await repo.Execute(100,Activate(101,moneyType));Check(result.Fidelity is {Attiva:true,Punti:0}&&result.Fidelity.Timbri.Length==2,"Rinnovo conserva storico e inizia nuova raccolta");
        await repo.Execute(100,Activate(102,freeType));
        var failingOptions=new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(new FailStamp()).Options;
        var failing=new CassaDatabaseRepository(()=>new AppDbContext(failingOptions));
        await Reject(()=>failing.Execute(100,Enter(102)),"Errore timbro");
        await using(var db=new AppDbContext(options))Check(!await db.Schede.AnyAsync(x=>x.PersonId==102)&&!await db.FidelityEntries.AnyAsync(x=>x.Fidelity!.PersonId==102),"Errore timbro annulla ingresso e timbro insieme");
        await using(var db=new AppDbContext(options))
        {
            var type=await db.TipiFidelity.SingleAsync(x=>x.Id==freeType);type.Attivo=false;await db.SaveChangesAsync();
        }
        await Reject(()=>repo.Execute(100,Activate(104,freeType)),"non disponibile");
        Check((await repo.Execute(100,Find(102))).Fidelity!.Attiva,"Disattivare tipo non invalida raccolte già attivate");
        var historyRequest=new CassaRequest{Action=CassaAction.LoadFidelityHistory,PostazioneId=100,NumeroTessera="T101"};
        var history=await repo.Execute(100,historyRequest);
        Check(history.Socio?.PersonId==101&&history.Fidelity is {Punti:0,Attiva:true}&&history.MovimentiFidelity.Length>2,"Storico separato dal ciclo corrente dopo rinnovo");
        Check(new[]{1,3,4,5,6}.All(kind=>history.MovimentiFidelity.Any(x=>x.TipoMovimento==kind)),"Storico include timbri, premi, sconti e storni");
        Check(history.MovimentiFidelity.All(x=>x.DataOra is not null)&&history.MovimentiFidelity.Select(x=>x.DataOra).SequenceEqual(history.MovimentiFidelity.Select(x=>x.DataOra).OrderDescending()),"Movimenti datati in ordine dal più recente");
        historyRequest.NumeroTessera="T100";var freeHistory=await repo.Execute(100,historyRequest);
        Check(freeHistory.MovimentiFidelity.Any(x=>x.TipoMovimento==2&&x.Punti<0),"Storico include ingresso premio utilizzato");
        historyRequest.NumeroTessera="T103";Check((await repo.Execute(100,historyRequest)).Fidelity is null,"Consultazione socio senza Fidelity non attiva raccolte");
        historyRequest.NumeroTessera="";await Reject(()=>repo.Execute(100,historyRequest),"Inserisci");
        historyRequest.NumeroTessera="inesistente";await Reject(()=>repo.Execute(100,historyRequest),"non trovato");
        historyRequest.NumeroTessera="T101";var again=await repo.Execute(100,historyRequest);
        Check(again.MovimentiFidelity.Length==history.MovimentiFidelity.Length&&again.Fidelity?.Punti==history.Fidelity?.Punti,"Consultare storico non modifica timbri o credito");
        var historyJson=JsonSerializer.Serialize(new DatabaseHostResponse{Success=true,Cassa=history},DatabaseProtocolJsonContext.Default.DatabaseHostResponse);
        Check(JsonSerializer.Deserialize(historyJson,DatabaseProtocolJsonContext.Default.DatabaseHostResponse)?.Cassa?.MovimentiFidelity.Length==history.MovimentiFidelity.Length,"Protocollo conserva tutti i movimenti");
        await repo.Execute(100,Activate(105,moneyType));
        var cancelEntry=await repo.Execute(100,Enter(105));
        async Task<CassaResult> Cancel(int person,CassaResult entry)
        {
            var open=await repo.Execute(100,new(){Action=CassaAction.OpenSheet,PostazioneId=100,Posizione="F"+person});
            return await repo.Execute(100,new(){Action=CassaAction.StornaConto,PostazioneId=100,Posizione="F"+person,SchedaId=entry.Scheda!.Id,ContoId=open.Conto.Single(x=>x.IsIngresso).Id});
        }
        var cancelled=await Cancel(105,cancelEntry);
        Check(cancelled.Scheda?.IngressoAnnullato==true&&cancelled.Conto.Any(x=>x.VoiceDesc.Contains("NON ENTRA")),"Annullamento ingresso mostra NON ENTRA");
        Check(cancelled.Fidelity is {Punti:0}&&cancelled.Fidelity.Timbri.Length==0,"Storno annulla il timbro Fidelity senza cancellare lo storico");
        Check(!(await repo.Execute(100,new(){Action=CassaAction.ListInside,PostazioneId=100})).Schede.Any(x=>x.PersonId==105),"NON ENTRA escluso dai presenti");
        await Reject(()=>Cancel(105,cancelEntry),"già annullato");await Exit(105);
        await using(var db=new AppDbContext(options))
        {
            db.Abbonamenti.Add(new(){Id=105,Nome="Prova annullamento",TipoAbbonamentoId=100,PersonId1=105,Attivo=true,DataInizio=DateTime.Today,DataFine=DateTime.Today.AddDays(30),IngressiAcquistati=3,PagatoAllAcquisto=true,PrezzoAcquisto=30});await db.SaveChangesAsync();
        }
        var subEntry=Enter(105,0);subEntry.AbbonamentoIngressoId=105;subEntry.NumeroIngressoAbbonamento=1;
        var used=await repo.Execute(100,subEntry);cancelled=await Cancel(105,used);
        Check(cancelled.Conto.Any(x=>x.VoicePrice==0&&x.VoiceDesc.Contains("NON ENTRA")),"Ingresso abbonamento a zero stornabile");
        await using(var db=new AppDbContext(options))Check(!(await db.AbbonamentoConti.SingleAsync(x=>x.AbbonamentoId==105)).Entrato,"Bollino abbonamento restituito");
        await Exit(105);used=await repo.Execute(100,subEntry);
        Check(used.Fidelity is {Punti:1},"Bollino restituito riutilizzabile con un solo nuovo timbro");await Exit(105);
        var thresholdEntry=await repo.Execute(100,Enter(105));
        Check(thresholdEntry.Fidelity?.CreditoSconto==7.50m,"Premio denaro maturato al nuovo ingresso");
        var spent=await repo.Execute(100,new(){Action=CassaAction.AddCharge,PostazioneId=100,Posizione="F105",SchedaId=thresholdEntry.Scheda!.Id,ListinoId=103,UsaCreditoFidelity=true});
        await Reject(()=>Cancel(105,thresholdEntry),"già stato utilizzato");
        Check((await repo.Execute(100,new(){Action=CassaAction.OpenSheet,PostazioneId=100,Posizione="F105"})).Scheda?.IngressoAnnullato==false,"Premio speso: annullamento rifiutato senza modifiche parziali");
        var spentLine=spent.Conto.Single(x=>x.VoiceDesc=="Acquisto");
        await repo.Execute(100,new(){Action=CassaAction.StornaConto,PostazioneId=100,Posizione="F105",SchedaId=thresholdEntry.Scheda.Id,ContoId=spentLine.Id});
        cancelled=await Cancel(105,thresholdEntry);Check(cancelled.Fidelity is {Punti:1,CreditoSconto:0}&&cancelled.Fidelity.Timbri.Length==1,"Storno al traguardo revoca premio e timbro insieme");await Exit(105);
        var restoreType=(await services.Execute(100,Config("Premio annullabile",1))).SelectedId;
        await repo.Execute(100,Activate(104,restoreType));await repo.Execute(100,Enter(104));await Exit(104);await repo.Execute(100,Enter(104));await Exit(104);
        var rewardRequest=Enter(104,0);rewardRequest.IngressoFidelityId=(await repo.Execute(100,Find(104))).Fidelity!.Id;
        var rewardEntry=await repo.Execute(100,rewardRequest);cancelled=await Cancel(104,rewardEntry);
        Check(cancelled.Fidelity is {Punti:2,CanIngresso:true}&&cancelled.Fidelity.Timbri.Length==2,"Annullare ingresso premio restituisce premio e annulla solo il suo timbro");
        var cancellationHistory=await repo.Execute(100,new(){Action=CassaAction.LoadFidelityHistory,PostazioneId=100,NumeroTessera="T104"});
        Check(cancellationHistory.MovimentiFidelity.Any(x=>x.TipoMovimento==7)&&cancellationHistory.MovimentiFidelity.Any(x=>x.TipoMovimento==8),"Storico conserva annullamento timbro e premio restituito");
        var json=JsonSerializer.Serialize(new DatabaseHostResponse{Success=true,Cassa=result},DatabaseProtocolJsonContext.Default.DatabaseHostResponse);
        Check(JsonSerializer.Deserialize(json,DatabaseProtocolJsonContext.Default.DatabaseHostResponse)?.Cassa?.Fidelity?.Timbri.Length==2,"Protocollo conserva storico timbri");
    }
    private sealed class FailStamp:SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        {if(data.Context!.ChangeTracker.Entries<FidelityConto>().Any(x=>x.State==EntityState.Added))throw new InvalidOperationException("Errore timbro simulato");return ValueTask.FromResult(result);}
    }
    private static void Pump(Func<bool> done){var until=DateTime.UtcNow.AddSeconds(15);while(!done()){Dispatcher.UIThread.RunJobs();Thread.Sleep(1);if(DateTime.UtcNow>until)throw new TimeoutException("UI Fidelity");}}
    private static void Command(ReactiveCommand<RxVoid,RxVoid> command){var done=false;using var sub=command.Execute().SubscribePrimitives(_=>{},ex=>throw ex,()=>done=true);Pump(()=>done);Dispatcher.UIThread.RunJobs();}
    private static void Shot(Window window,string name)
    {using var frame=window.CaptureRenderedFrame();var path=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","previews",name+".png"));Directory.CreateDirectory(Path.GetDirectoryName(path)!);frame!.Save(path,Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
    public static void UI()
    {
        var sf=new ServicesFake();var svm=new ServiziViewModel(null,sf);var sw=new Window{Width=1400,Height=900,Content=new ServiziView{ViewModel=svm}};sw.Show();Pump(()=>svm.GroupEnabled);
        Command(svm.FidelitySectionCommand);Check(svm.IsFidelity&&sf.Last?.Section==ServiziSection.Fidelity,"Servizi apre sezione Fidelity");
        Command(svm.AddCommand);svm.Nome="Raccolta sconto";svm.NumeroIngressi=2;svm.DurataGiorni=30;svm.SelectedPremio="Sconto in denaro";svm.Prezzo=7.50m;Shot(sw,"servizi-fidelity");
        Command(svm.SaveCommand);Check(sf.Last?.Record is {TipoPremioId:2,NumeroIngressi:2,Prezzo:7.50m},"Editor invia soglia e premio Fidelity");
        Command(svm.AbbonamentiSectionCommand);Check(!svm.IsFidelity&&sf.Last?.Section==ServiziSection.Abbonamenti,"Sezioni Servizi mantengono richieste distinte");sw.Close();Dispatcher.UIThread.RunJobs();
        var fake=new CassaFake();var vm=new CassaViewModel(new Screen(),fake,100);var view=new CassaView{ViewModel=vm};var window=new Window{Width=1300,Height=950,Content=view};window.Show();Pump(()=>vm.IsUiReady);
        Command(vm.ApriStoricoFidelityCommand);Check(vm.IsStoricoFidelity&&vm.MovimentiFidelity.Count==0,"Vedi Fidelity apre ricerca dalla postazione");
        vm.TesseraStoricoFidelity="T100";Command(vm.CercaStoricoFidelityCommand);
        Check(vm.MovimentiFidelity.Count==2&&vm.RiepilogoStoricoFidelity.Contains("Mario"),"Ricerca storico mostra socio e movimenti");Shot(window,"storico-fidelity");
        vm.TesseraStoricoFidelity="T101";Check(vm.MovimentiFidelity.Count==0&&!vm.RiepilogoStoricoFidelity.Contains("Mario"),"Cambio tessera cancella storico del socio precedente");
        Command(vm.CercaStoricoFidelityCommand);Check(vm.RiepilogoStoricoFidelity.Contains("mai attivata"),"Socio senza Fidelity ha messaggio dedicato");
        Command(vm.BackCommand);Check(vm.IsPostazione,"Storico torna alla postazione");
        Command(vm.EntraSocioCommand);vm.NumeroTessera="T100";Command(vm.FindCardCommand);Check(vm.CanActivateFidelity&&!vm.CanIngressoFidelity,"Attivazione proposta prima dell'uso");
        Command(vm.AttivaFidelityCommand);Check(vm.Fidelity is {Attiva:true,Punti:0}&&!vm.CanActivateFidelity,"Comando attiva raccolta senza timbri");
        fake.Reward=true;Command(vm.FindCardCommand);vm.EntryPosition="F100";Check(vm.CanIngressoFidelity&&vm.Fidelity?.Timbri.Length==2,"Premio raggiunto e storico visibili");
        Check(view.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text?.Contains(DateTime.Today.ToString("dd/MM/yyyy"))==true&&x.IsEffectivelyVisible),"Data timbri visibile");
        Command(vm.IngressoFidelityCommand);Check(vm.SelectedIngresso is null&&vm.SceltaIngresso.Contains("Fidelity"),"Ingresso Fidelity selezionabile in alternativa alla tariffa");Shot(window,"entra-fidelity");
        Command(vm.RegisterEntryCommand);Check(fake.LastEntry is {IngressoFidelityId:1,TariffaId:0,AbbonamentoIngressoId:0},"Ingresso invia scelta premio esclusiva");
        Command(vm.EntraSocioCommand);vm.NumeroTessera="T100";fake.Return=true;Command(vm.FindCardCommand);Check(vm.HasRientro&&!vm.ShowIngressoFidelity,"Rientro mantiene precedenza sul premio Fidelity");
        vm.NumeroTessera="T101";Check(!vm.HasFidelity,"Cambio tessera cancella raccolta precedente");window.Close();Dispatcher.UIThread.RunJobs();
    }
    private sealed class Screen:ReactiveObject,IScreen{public RoutingState Router{get;}=new();}
    private sealed class ServicesFake:IServiziRepository
    {
        public ServiziRequest? Last;
        public Task<ServiziResult> Execute(ServiziRequest r,CancellationToken ct=default){Last=r;return Task.FromResult(new ServiziResult{Records=r.Record is {} record?[record]:[]});}
    }
    private sealed class CassaFake:ICassaRepository
    {
        public bool Active,Reward,Return;public CassaRequest? LastEntry;
        public Task<CassaResult> Execute(CassaRequest r,CancellationToken ct=default)=>Task.FromResult(Reply(r));
        private CassaResult Reply(CassaRequest r)
        {
            if(r.Action==CassaAction.LoadFidelityHistory)return new(){Socio=new(){PersonId=100,Nome="Mario",Cognome="Rossi",NumeroTessera=r.NumeroTessera},Fidelity=r.NumeroTessera=="T101"?null:new(){Id=1,Nome="Raccolta ingressi",Attiva=true,Traguardo=10,Punti=2,AttivataIl=DateTime.Today},MovimentiFidelity=r.NumeroTessera=="T101"?[]:[new(){Id=2,Ciclo=1,TipoMovimento=1,Punti=1,DataOra=DateTime.Today.AddHours(10)},new(){Id=1,Ciclo=1,TipoMovimento=1,Punti=1,DataOra=DateTime.Today.AddHours(9)}]};
            if(r.Action==CassaAction.PrepareEntry)return new(){Ingressi=[new(){Id=100,Etichetta="Ordinario",Prezzo=10}]};
            if(r.Action==CassaAction.ActivateFidelity)Active=true;
            if(r.Action is CassaAction.ActivateFidelity or CassaAction.FindCard)return new(){Socio=new(){PersonId=100,Nome="Mario",Cognome="Rossi",NumeroTessera=r.NumeroTessera,ScadenzaTessera=20991231},Rientro=Return?new(){Tipo="Giornata",UltimoIngresso=DateTime.Now.AddHours(-1)}:null,
                TipiFidelity=[new(){Id=100,Nome="Ingresso premio",Traguardo=2,TipoPremioId=1}],Fidelity=!Active?null:new(){Id=1,Nome="Ingresso premio",Attiva=true,Traguardo=2,TipoPremioId=1,Punti=Reward?2:0,Timbri=Reward?[new(){DataOra=DateTime.Today.AddHours(9)},new(){DataOra=DateTime.Today.AddHours(10)}]:[]}};
            if(r.Action==CassaAction.RegisterEntry){LastEntry=r;return new(){Scheda=new(){Id=1,Posizione=r.Posizione}};}
            if(r.Action==CassaAction.OpenSheet)return new(){Scheda=new(){Id=1,Posizione=r.Posizione}};
            return new(){PostazioneName="Cassa"};
        }
    }
}
