using AppServices.Database;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Leonardo18.DatabaseHost.Menu;
using Leonardo18.DatabaseHost.Report;
using Menu.Core.Repository;
using Menu.ViewModels;
using Menu.Views;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Models.Context;
using Models.Contabilita;
using Models.Entity.Global;
using Models.Tables;
using ReactiveUI;
using ReactiveUI.Primitives;
using Report.Printing;
using System.Text.Json;

internal static class CassaInizialeChecks
{
    public static int Count;
    private static void Check(bool ok,string text){if(!ok)throw new Exception(text);Count++;}
    private static async Task Reject(Func<Task> run,string text)
    {try{await run();}catch(InvalidOperationException e){Check(e.Message.Contains(text,StringComparison.OrdinalIgnoreCase),e.Message);return;}throw new Exception("Accettata richiesta non valida: "+text);}
    private static MenuAperturaRequest Request()=>new(){IdOperazione=Guid.NewGuid(),Tagli=CassaInizialeRules.TagliConsentiti.Select(t=>new TaglioEuro{Taglio=t,Quantita=1}).ToArray()};
    public static async Task Database()
    {
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        var options=new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using(var db=new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Postazioni.Add(new(){Id=100,Nome="Cassa",TipoPostazioneId=2,TipoRientroId=-1});
            db.Operatori.AddRange(new(){Id=100,Nome="Cassiere",PersonId=-1,Abilitato=true},new(){Id=101,Nome="Nessun permesso",PersonId=-1,Abilitato=true},new(){Id=102,Nome="Disabilitato",PersonId=-1,Abilitato=false});
            db.Permessi.Add(new(){OperatoreId=100,PostazioneId=100});await db.SaveChangesAsync();
        }
        var repo=new MenuDatabaseRepository(()=>new AppDbContext(options));
        var request=Request();
        Check(request.Tagli.Length==12&&request.Tagli.Min(x=>x.Taglio)==0.10m&&request.Tagli.Sum(x=>x.Totale)==888.8m,"Dodici tagli da 500 euro a 10 centesimi, compresi 1/2/5 euro");
        await Reject(()=>repo.Execute(100,"menu-open"),"conteggio");
        await Reject(()=>repo.Execute(101,"menu-open",apertura:request),"permesso");
        await Reject(()=>repo.Execute(102,"menu-open",apertura:request),"abilitato");
        var wrong=Request();wrong.Tagli[0].Quantita=-1;await Reject(()=>repo.Execute(100,"menu-open",apertura:wrong),"Conteggio non valido");
        wrong=Request();wrong.Tagli[0].Taglio=0.05m;await Reject(()=>repo.Execute(100,"menu-open",apertura:wrong),"Conteggio non valido");
        wrong=Request();wrong.Tagli[0].Taglio=wrong.Tagli[1].Taglio;await Reject(()=>repo.Execute(100,"menu-open",apertura:wrong),"Conteggio non valido");
        var json=JsonSerializer.Serialize(new DatabaseHostRequest{Operation="menu-open",AperturaGiornata=request},DatabaseProtocolJsonContext.Default.DatabaseHostRequest);
        Check(JsonSerializer.Deserialize(json,DatabaseProtocolJsonContext.Default.DatabaseHostRequest)?.AperturaGiornata?.Tagli.Last().Taglio==0.1m,"Protocollo AOT preserva i centesimi e il conteggio");
        var opened=await repo.Execute(100,"menu-open",apertura:request);Check(opened.Day?.Open==true,"Conferma apre la giornata");
        await using(var db=new AppDbContext(options))
        {
            var day=await db.Giornate.SingleAsync();Check(day.CassaIniziale==888.8m&&day.AperturaId==request.IdOperazione,"Totale e identificativo salvati nella giornata");
            Check(JsonSerializer.Deserialize(day.ConteggioCassaIniziale,DatabaseProtocolJsonContext.Default.TaglioEuroArray)?.Length==12,"Dettaglio tagli conservato");
            var movement=await db.ContabilitaMovimenti.Include(x=>x.Righe).ThenInclude(x=>x.Conto).SingleAsync();movement.VerificaQuadratura();
            Check(movement.GiornataId==day.Id&&movement.OperatoreId==100&&movement.Stato==StatoMovimentoContabile.Confermato,"Scrittura collegata alla giornata e all'operatore");
            Check(movement.Righe.Single(x=>x.Conto.Codice=="100").Dare==888.8m&&movement.Righe.Single(x=>x.Conto.Codice=="310").Avere==888.8m,"Cassa a Dare e Cassa Iniziale a Avere");
            Check(movement.Righe.All(x=>x.Conto.Natura is NaturaConto.Attivita or NaturaConto.PatrimonioNetto),"Fondo esclusivamente patrimoniale");
            Check(await db.SchedeConto.CountAsync()==0,"Nessun addebito sulle schede dei soci");
        }
        await repo.Execute(100,"menu-open",apertura:request);
        await using(var db=new AppDbContext(options))Check(await db.Giornate.CountAsync()==1&&await db.ContabilitaMovimenti.CountAsync()==1,"Conferma ripetuta non duplica giornata o scritture");
        await Reject(()=>repo.Execute(100,"menu-open",apertura:Request()),"già aperta");
        var changed=new MenuAperturaRequest{IdOperazione=request.IdOperazione,Tagli=Request().Tagli};changed.Tagli[0].Quantita=2;
        await Reject(()=>repo.Execute(100,"menu-open",apertura:changed),"già aperta");
        var reportRepo=new ReportDatabaseRepository(()=>new AppDbContext(options));
        var report=await reportRepo.Execute(100,new());
        Check(report.CassaIniziale==888.8m&&report.TagliCassaIniziale.Length==12&&report.DisponibilitaMonetaria==888.8m,"Report mostra totale, tagli e disponibilità iniziale");
        Check(report.Incassi==0&&report.Ricavi==0&&report.TotaleOperativo==0&&report.SaldoMonetario==0,"Fondo non gonfia incassi, ricavi o operativo");
        var html=ReportPrintDocument.Create(report);Check(html.Contains("Cassa Iniziale")&&html.Contains("888,80")&&html.Contains("0,10"),"Report stampabile include fondo e monete");
        await repo.Execute(100,"menu-close");
        Check((await reportRepo.Execute(100,new(){GiornataId=opened.Day!.Id})).CassaIniziale==888.8m,"Report conserva il fondo della giornata chiusa");
        await Reject(()=>repo.Execute(100,"menu-open",apertura:request),"già chiusa");
        var zero=Request();foreach(var t in zero.Tagli)t.Quantita=0;
        var second=await repo.Execute(100,"menu-open",apertura:zero);await repo.Execute(100,"menu-open",apertura:zero);
        await using(var db=new AppDbContext(options))Check(await db.Giornate.CountAsync()==2&&await db.ContabilitaMovimenti.CountAsync()==1,"Apertura con cassa zero valida e senza righe contabili a zero");
        Check((await reportRepo.Execute(100,new(){GiornataId=second.Day!.Id})).CassaIniziale==0,"Fondo separato per giornata");
        await repo.Execute(100,"menu-close");
        var badOptions=new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(new FailPosting()).Options;
        await Reject(()=>new MenuDatabaseRepository(()=>new AppDbContext(badOptions)).Execute(100,"menu-open",apertura:Request()),"Errore simulato");
        await using(var db=new AppDbContext(options))Check(await db.Giornate.CountAsync()==2&&!await db.Giornate.AnyAsync(x=>x.Aperta)&&await db.ContabilitaMovimenti.CountAsync()==1,"Errore contabile annulla anche l'apertura della giornata");
    }
    private sealed class FailPosting:SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e,InterceptionResult<int> result,CancellationToken ct=default)
        {if(e.Context!.ChangeTracker.Entries<MovimentoContabile>().Any(x=>x.State==EntityState.Added))throw new InvalidOperationException("Errore simulato scrittura");return ValueTask.FromResult(result);}
    }
    private static void Command(ReactiveCommand<RxVoid,RxVoid> command)
    {var done=false;Exception? error=null;using var sub=command.Execute().SubscribePrimitives(_=>{},e=>{error=e;done=true;},()=>done=true);var end=DateTime.UtcNow.AddSeconds(15);while(!done){Dispatcher.UIThread.RunJobs();if(DateTime.UtcNow>end)throw new TimeoutException();}if(error!=null)throw error;Dispatcher.UIThread.RunJobs();}
    public static void UI()
    {
        GlobalValuesC.MySetting=new(){IDOPERATORE=100};
        var fake=new Fake();var vm=new MenuViewModel(fake,new Screen());var view=new MenuView{ViewModel=vm};var window=new Window{Width=1000,Height=850,Content=view};window.Show();Dispatcher.UIThread.RunJobs();
        Check(vm.IsMenuReady&&!vm.IsOpeningCash,"Menu caricato senza apertura automatica");
        Command(vm.ApriGiornataCommand);Check(vm.IsOpeningCash&&!vm.IsMenuReady&&fake.Opens==0,"Apri Giornata mostra il conteggio prima di scrivere");
        Check(vm.TagliCassa.Count==12&&view.GetVisualDescendants().OfType<ControlStyles.UiIntegerTextBox>().Count()==12,"Vista mostra una quantità per ciascun taglio");
        var fields=view.GetVisualDescendants().OfType<ControlStyles.UiIntegerTextBox>().ToArray();
        Check(fields[4].IsFocused&&fields[4].SelectionStart==0&&fields[4].SelectionEnd==fields[4].Text!.Length,"Apertura mette il focus sui 20 euro selezionando il valore");
        fields[4].Text="";window.KeyPress(Key.Enter,RawInputModifiers.None,PhysicalKey.Enter,null);Dispatcher.UIThread.RunJobs();
        Check(fields[4].Text=="0"&&vm.TagliCassa[4].Quantita==0&&vm.TagliCassa[4].IsValid&&fields[5].IsFocused,"Quantità vuota aggiorna anche il valore associato a zero con Invio");
        fields[0].Text="3";Dispatcher.UIThread.RunJobs();
        Check(vm.TagliCassa[0].Quantita==3&&vm.TotaleCassaIniziale==1500,"Testo intero aggiorna quantità e totale");
        fields[0].Focus();window.KeyPress(Key.Enter,RawInputModifiers.None,PhysicalKey.Enter,null);Dispatcher.UIThread.RunJobs();
        Check(fields[1].IsFocused,"Invio passa al campo quantità successivo");
        fields[^1].Focus();window.KeyPress(Key.Enter,RawInputModifiers.None,PhysicalKey.Enter,null);Dispatcher.UIThread.RunJobs();
        Check(fields[0].IsFocused,"Invio sull'ultimo taglio torna al primo");
        fields[^1].Focus();window.KeyPress(Key.Tab,RawInputModifiers.None,PhysicalKey.Tab,null);Dispatcher.UIThread.RunJobs();
        Check(fields[0].IsFocused,"Tab ciclico salta i pulsanti e torna al primo taglio");
        window.KeyPress(Key.Tab,RawInputModifiers.Shift,PhysicalKey.Tab,null);Dispatcher.UIThread.RunJobs();
        Check(fields[^1].IsFocused,"Maiusc Tab ciclico torna all'ultimo taglio");
        Check(view.GetVisualDescendants().OfType<ControlStyles.UiIconButton>().Where(x=>x.IsEffectivelyVisible).All(x=>!x.IsTabStop),"Pulsanti esclusi dalla navigazione con Tab");
        foreach(var t in vm.TagliCassa)t.Quantita=1;
        Check(vm.TotaleCassaIniziale==888.8m,"Totale aggiornato durante il conteggio");
        vm.TagliCassa[0].Quantita=1.5m;Command(vm.ConfermaAperturaCommand);Check(fake.Opens==0&&vm.InfoError.Contains("intere"),"Quantità frazionarie rifiutate prima della scrittura");vm.TagliCassa[0].Quantita=1;
        fake.Fail=true;Command(vm.ConfermaAperturaCommand);var id=fake.Last!.IdOperazione;
        Check(vm.IsOpeningCash&&vm.IsOpeningReady&&vm.TotaleCassaIniziale==888.8m,"Errore conserva conteggio per riprovare");
        var folder=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","previews"));Directory.CreateDirectory(folder);
        foreach(var size in new[]{(1000,850),(900,650)}){window.Width=size.Item1;window.Height=size.Item2;Dispatcher.UIThread.RunJobs();using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(folder,$"cassa-iniziale-{size.Item1}.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
        fake.Fail=false;Command(vm.ConfermaAperturaCommand);
        Check(fake.Last!.IdOperazione==id&&fake.Last.Tagli.Sum(x=>x.Totale)==888.8m,"Riprova conserva identificativo e conteggio");
        Check(vm.IsMenuReady&&!vm.IsOpeningCash&&vm.SessioneContabile.Contains("aperta"),"Dopo la conferma torna al menu con giornata aperta");
        Command(vm.ChiudiGiornataCommand);Command(vm.ApriGiornataCommand);Check(vm.TotaleCassaIniziale==0,"Nuova apertura riparte da quantità zero");Check(fields[4].IsFocused,"Ogni nuova apertura riporta il focus sui 20 euro");var before=fake.Opens;Command(vm.AnnullaAperturaCommand);
        Check(!vm.IsOpeningCash&&vm.IsMenuReady&&fake.Opens==before,"Annulla torna al menu senza aprire giornata");
        window.Close();Dispatcher.UIThread.RunJobs();
    }
    private sealed class Screen:ReactiveObject,IScreen{public RoutingState Router{get;}=new();}
    private sealed class Fake:IMenuRepository
    {
        public int Opens;public bool Fail;public MenuAperturaRequest? Last;private bool _open;
        private MenuState State()=>new(){Stations=[new(){Id=100,Name="Cassa",TypeId=2}],Day=_open?new(){Id=1,Open=true,Start=DateTime.Now}:null};
        public Task<MenuState> Execute(int id,string operation,CancellationToken ct=default){if(operation=="menu-close")_open=false;return Task.FromResult(State());}
        public Task<MenuState> OpenDay(int id,MenuAperturaRequest request,CancellationToken ct=default){Opens++;Last=request;if(Fail)throw new InvalidOperationException("Errore prova");_open=true;return Task.FromResult(State());}
    }
}
