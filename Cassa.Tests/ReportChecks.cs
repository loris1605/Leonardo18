using Report.Printing;
using System.Text.Json;
using AppServices.Database;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Leonardo18.DatabaseHost.Report;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Models.Context;
using Models.Contabilita;
using Models.Sospesi;
using Models.Tables;
using ReactiveUI;
using ReactiveUI.Primitives;
using Report.Core.Repository;
using Report.ViewModels;
using Report.Views;

internal static class ReportChecks
{
    public static int Count;
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Count++;}
    private static async Task Reject(Func<Task> action)
    {try{await action();}catch(InvalidOperationException){Count++;return;}throw new Exception("Report non autorizzato o giornata inesistente accettati.");}
    public static async Task Database()
    {
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        var options=new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var start=new DateTime(2025,4,3,20,0,0);var end=start.AddHours(8);
        await using(var db=new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Postazioni.Add(new(){Id=100,Nome="Cassa report",TipoPostazioneId=2,TipoRientroId=-1});
            db.Operatori.AddRange(new Operatore{Id=100,Nome="Abilitato",PersonId=-1,Abilitato=true},new Operatore{Id=101,Nome="Senza permessi",PersonId=-1,Abilitato=true},new Operatore{Id=102,Nome="Disabilitato",PersonId=-1,Abilitato=false});
            db.Permessi.AddRange(new Permesso{PostazioneId=100,OperatoreId=100},new Permesso{PostazioneId=100,OperatoreId=102});
            await db.SaveChangesAsync();
        }
        var repository=new ReportDatabaseRepository(()=>new AppDbContext(options));
        Check((await repository.Execute(100,new())).Giornate.Length==0,"Report senza giornate: stato vuoto");
        await Reject(()=>repository.Execute(101,new()));await Reject(()=>repository.Execute(102,new()));
        await using(var db=new AppDbContext(options))
        {
            db.Giornate.AddRange(new Giornata{Id=100,DataInizio=start.AddDays(-1),DataFine=end.AddDays(-1)},new Giornata{Id=101,DataInizio=start,DataFine=end});
            db.Settori.AddRange(new Settore{Id=100,Nome="Ingresso",TipoSettoreId=-1},new Settore{Id=101,Nome="Bar",TipoSettoreId=-2},new Settore{Id=102,Nome="Cucina",TipoSettoreId=-2});
            for(var i=100;i<104;i++)db.People.Add(new(){Id=i,UniqueParam="REPORT"+i,FirstName="Nome",SurName="Socio",Natoil=19900101});
            db.Schede.AddRange(new Scheda{Id=100,PersonId=100,Posizione="A",CheckinTime=start.AddMinutes(5),CheckoutTime=start.AddHours(2)},
                new Scheda{Id=101,PersonId=101,Posizione="B",CheckinTime=start.AddHours(1)},
                new Scheda{Id=102,PersonId=102,Posizione="C",CheckinTime=start.AddDays(-1)},
                new Scheda{Id=103,PersonId=103,Posizione="D",CheckinTime=start.AddDays(-2),CheckoutTime=start.AddHours(3)});
            db.SchedeConto.AddRange(Line("Ingresso","Standard",12,true,start.AddMinutes(5)),Line("Bar","Bibita",8,false,start.AddHours(1)),
                Line("Bar","STORNO - Bibita",-3,false,start.AddHours(2)),Line("Bar","Caffè",4,true,start.AddHours(5)),Line("Bar","Omaggio",0,true,start.AddHours(6)),
                Line("Bar","Giorno precedente",100,true,start.AddDays(-1).AddHours(1)),Line("Bar","Dopo chiusura",100,true,end));
            var entry=Movement(100,101,12,"Ingresso",-1,-6);
            var credit=Movement(101,101,5,"Bar",-3,-6);
            var subscription=Movement(102,101,50,null,-1,-6);foreach(var row in subscription.Righe)row.AbbonamentoId=200;
            var cost=Movement(103,101,7,null,-7,-1);
            var draft=Movement(104,101,999,"Bar",-1,-6);draft.Stato=StatoMovimentoContabile.Bozza;
            db.ContabilitaMovimenti.AddRange(entry,credit,subscription,cost,draft,Movement(105,100,100,"Bar",-1,-6),Movement(106,101,9,null,-1,-2));
            db.TipiAbbonamento.Add(new(){Id=100,Nome="Dieci",Attivo=true,NumeroIngressi=10,DurataGiorni=30,Prezzo=50});
            db.Abbonamenti.Add(new(){Id=200,Nome="Dieci",TipoAbbonamentoId=100,PersonId1=100,DataInizio=start.Date,DataFine=end.AddDays(30),DataAcquisto=start.AddHours(1),PrezzoAcquisto=50,AcquistoId=Guid.NewGuid()});
            db.Sospesi.AddRange(Pending(100,30,start.AddDays(-2)),Pending(101,20,start.AddHours(1)),
                Pending(102,40,start.AddHours(1),TipoSospeso.Debito),Pending(103,900,end.AddDays(1)));
            var settled=Pending(104,50,start.AddDays(-1));settled.Stato=StatoSospeso.Saldato;settled.DataSaldo=start.AddHours(1);settled.MovimentoSaldo=subscription;
            var later=Pending(105,15,start.AddHours(1));later.Stato=StatoSospeso.Saldato;later.DataSaldo=end.AddDays(1);later.MovimentoSaldo=entry;
            db.Sospesi.AddRange(settled,later);await db.SaveChangesAsync();

            SchedaConto Line(string sector,string label,decimal value,bool paid,DateTime at)=>new(){SchedaId=100,DescSettore=sector,DescPostazione=sector=="Ingresso"?"Porta":"Banco",VoiceDesc=label,VoicePrice=value,Pagato=paid,DataOra=at};
            MovimentoContabile Movement(long id,int day,decimal amount,string? sector,int debit,int credit)=>new(){Id=id,IdOperazione=Guid.NewGuid(),GiornataId=day,DataOra=start.AddHours(1),DataCompetenza=start.Date,Stato=StatoMovimentoContabile.Confermato,Righe=[new(){NumeroRiga=1,ContoId=debit,Dare=amount,NomeSettore=sector},new(){NumeroRiga=2,ContoId=credit,Avere=amount,NomeSettore=sector}]};
            Sospeso Pending(long id,decimal amount,DateTime at,TipoSospeso type=TipoSospeso.Credito)=>new(){Id=id,IdOperazione=Guid.NewGuid(),Tipo=type,Origine=OrigineSospeso.Manuale,Importo=amount,DataOra=at,DataCompetenza=at.Date,PersonId=100,PersonId2=101,Nominativo="Socio"};
        }
        var result=await repository.Execute(100,new());
        Check(result.GiornataId==101&&result.Giornate.Length==2,"Selezione predefinita dell'ultima giornata");
        Check(result.Ingressi==2&&result.Uscite==2&&result.Presenti==2,"Ingressi, uscite e presenti comprendono i passaggi fra giornate");
        Check(result.TotaleOperativo==21&&result.PagatoOperativo==16&&result.DaPagareOperativo==5,"Operativo: pagato e da pagare riconciliati con gli storni");
        var bar=result.Settori.Single(x=>x.Nome=="Bar");
        Check(bar.Lordo==12&&bar.Storni==3&&bar.Netto==9&&bar.Operazioni==4,"Settore Bar: lordo, storni, netto e omaggi");
        Check(result.Settori.Single(x=>x.Nome=="Cucina").Operazioni==0,"Settori configurati senza operazioni visibili");
        Check(result.Voci.Any(x=>x.Descrizione=="Caffè"&&x.Pagato==4)&&!result.Voci.Any(x=>x.Descrizione=="Dopo chiusura"),"Etichette e orario notturno corretti");
        Check(result.Postazioni.Sum(x=>x.Netto)==result.TotaleOperativo&&result.Postazioni.Length==2,"Riepilogo di tutte le postazioni");
        Check(result.Ricavi==67&&result.Costi==7&&result.Incassi==62&&result.UsciteMonetarie==7&&result.SaldoMonetario==55,"Contabilità separata: ricavi, incassi e trasferimenti interni senza doppioni");
        Check(result.Contabilita.Single(x=>x.Settore=="Abbonamenti").Ricavi==50,"Abbonamenti attribuiti al settore contabile dedicato");
        Check(result.NumeroSospesi==4&&result.SospesiCrediti==65&&result.SospesiDebiti==40&&result.SospesiIncassati==50,"Sospesi storici: pregressi, secondo beneficiario e saldi successivi senza duplicati");
        Check(result.AbbonamentiVenduti==1&&result.ValoreAbbonamenti==50,"Acquisti abbonamenti distinti dagli incassi");
        Check((await repository.Execute(100,new(){GiornataId=100})).TotaleOperativo==100,"Cambio giornata senza commistione di periodi");
        await Reject(()=>repository.Execute(100,new(){GiornataId=999}));
        var json=JsonSerializer.Serialize(new DatabaseHostResponse{Success=true,Report=result},DatabaseProtocolJsonContext.Default.DatabaseHostResponse);
        Check(JsonSerializer.Deserialize(json,DatabaseProtocolJsonContext.Default.DatabaseHostResponse)?.Report?.TotaleOperativo==21,"Protocollo report compatibile con serializzazione AOT");
        await using(var db=new AppDbContext(options))
        {
            Check(await db.ContabilitaMovimenti.CountAsync()==7&&await db.SchedeConto.CountAsync()==7&&await db.Sospesi.CountAsync()==6,"Report non modifica dati operativi o contabili");
            db.Giornate.Add(new(){Id=102,DataInizio=DateTime.Now.AddMinutes(-1),DataFine=DateTime.MaxValue,Aperta=true});await db.SaveChangesAsync();
        }
        Check((await repository.Execute(100,new())).GiornataId==102,"Giornata aperta selezionata automaticamente");
    }
    public static void UI()
    {
        var fake=new Fake();var printer=new PrintFake();var vm=new ReportViewModel(new Screen(),fake,printer);var view=new ReportView{ViewModel=vm};var window=new Window{Width=1200,Height=800,Content=view};
        window.Show();Dispatcher.UIThread.RunJobs();
        Check(vm.HasData&&vm.Giornate.Count==2&&vm.Data.TotaleOperativo==21,"Schermata report caricata");
        Check(view.GetVisualDescendants().OfType<TabItem>().Count()==6,"Report con sezioni settori, voci, postazioni, contabilità e sospesi");
        Check(vm.CanPrint,"Stampa disponibile solo con dati caricati");
        vm.SelectedGiornata=vm.Giornate.Last();Execute(vm.PrintCommand);
        Check(printer.Last?.GiornataId==101&&fake.LastId==0,"Stampa copia la giornata visualizzata senza rileggere né usare la selezione non caricata");
        printer.Fail=true;Execute(vm.PrintCommand);Check(vm.InfoLabel.Contains("Impossibile aprire")&&vm.CanPrint,"Errore stampa recuperabile");printer.Fail=false;
        vm.SelectedGiornata=vm.Giornate.Last();Execute(vm.RefreshCommand);
        Check(fake.LastId==100&&vm.Data.GiornataId==100,"Selettore e aggiornamento caricano la giornata richiesta");
        vm.SelectedGiornata=vm.Giornate.First();Execute(vm.RefreshCommand);
        foreach(var size in new[]{(1200,800),(900,650)})
        {
            window.Width=size.Item1;window.Height=size.Item2;Dispatcher.UIThread.RunJobs();
            using var frame=window.CaptureRenderedFrame();
            var folder=Path.Combine(AppContext.BaseDirectory,"..","..","..","previews");Directory.CreateDirectory(folder);
            frame!.Save(Path.Combine(folder,$"report-{size.Item1}.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            Check(view.GetVisualDescendants().OfType<ControlStyles.UiIconButton>().Where(x=>x.IsEffectivelyVisible).All(x=>x.Bounds.Width>0&&x.Bounds.Height>0),"Comandi Report visibili alle dimensioni della finestra");
        }
        fake.Fail=true;Execute(vm.RefreshCommand);Check(vm.InfoLabel.Contains("Errore report")&&!vm.IsBusy,"Errore di lettura gestito senza bloccare la schermata");
        var menus=0;using var sub=vm.ReportToMenu.SubscribePrimitives(_=>menus++);Execute(vm.MenuCommand);Check(menus==1,"Ritorno al menu");
        window.Close();Dispatcher.UIThread.RunJobs();
        var slow=new Fake{Delay=new TaskCompletionSource<ReportResult>()};var lateVm=new ReportViewModel(new Screen(),slow);var lateWindow=new Window{Content=new ReportView{ViewModel=lateVm}};lateWindow.Show();Dispatcher.UIThread.RunJobs();lateWindow.Close();Dispatcher.UIThread.RunJobs();slow.Delay.SetResult(new());Dispatcher.UIThread.RunJobs();
        Check(!lateVm.HasData,"Risposta tardiva ignorata dopo la chiusura");
        Check(!lateVm.CanPrint,"Stampa non disponibile dopo chiusura o senza giornata");
    }
    public static void PrintDocument()
    {
        var data=new ReportResult{GiornataId=123,Giornate=[new(){Id=123,Inizio=new(2026,10,16,18,0,0),Aperta=false}],SituazioneAl=new(2026,10,17,4,30,0),AggiornatoAl=new(2026,10,17,12,0,0),Ingressi=150,
            Settori=[new(){Nome="Bar <test> & caffè",Lordo=1234.56m,Storni=10,Pagato=1200,DaPagare=24.56m}],
            Postazioni=[new(){Nome="Cassa principale",Lordo=1234.56m}],
            Voci=Enumerable.Range(1,150).Select(i=>new ReportVoce{Settore="Bar",Descrizione="Voce "+i,Operazioni=i,Totale=i}).ToArray(),
            Contabilita=[new(){Settore="Ricavi",Ricavi=1234.56m,Costi=10}],NumeroSospesi=3,SospesiCrediti=25,AbbonamentiVenduti=2,ValoreAbbonamenti=100};
        var html=ReportPrintDocument.Create(data);
        Check(html.Contains("Voce 150")&&html.Contains("Voce 1</td>"),"Stampa comprende tutte le righe fuori schermo");
        Check(html.Contains("Bar &lt;test&gt; &amp; caff")&&!html.Contains("Bar <test>"),"Nomi e descrizioni HTML codificati");
        Check(html.Contains("1.234,56")&&html.Contains("16/10/2026 18:00")&&html.Contains("17/10/2026 04:30"),"Importi italiani e periodo notturno espliciti");
        Check(new[]{"Settori","Voci di spesa","Postazioni","Contabilit","Sospesi e abbonamenti"}.All(html.Contains),"Tutte le sezioni presenti nella stampa");
        Check(html.Contains("A4 landscape")&&html.Contains("table-header-group")&&html.Contains("window.print()"),"Formato pagina, intestazioni ripetute e comando stampa");
        Check(ReportPrintDocument.Create(new(){GiornataId=1}).Contains("Nessun movimento nel periodo."),"Sezioni vuote con messaggio dedicato");
        try{ReportPrintDocument.Create(new());throw new Exception("Stampa senza giornata accettata");}catch(InvalidOperationException){Count++;}
        var folder=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","previews"));Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"report-stampa.html"),html);
    }
    private sealed class PrintFake:IReportPrinter
    {
        public bool Fail;public ReportResult? Last;
        public Task OpenAsync(ReportResult data,CancellationToken ct=default){if(Fail)throw new InvalidOperationException("Errore stampa simulato");Last=data;return Task.CompletedTask;}
    }
    private static void Execute(ReactiveCommand<RxVoid,RxVoid> command)
    {var done=false;Exception? error=null;using var sub=command.Execute().SubscribePrimitives(_=>{},ex=>{error=ex;done=true;},()=>done=true);while(!done)Dispatcher.UIThread.RunJobs();if(error!=null)throw error;Dispatcher.UIThread.RunJobs();}
    private sealed class Screen:ReactiveObject,IScreen{public RoutingState Router {get;}=new();}
    private sealed class Fake:IReportRepository
    {
        public int LastId;public bool Fail;public TaskCompletionSource<ReportResult>? Delay;
        public Task<ReportResult> Execute(ReportRequest request,CancellationToken ct=default)
        {
            LastId=request.GiornataId;if(Delay!=null)return Delay.Task;if(Fail)throw new InvalidOperationException("Errore report simulato");
            return Task.FromResult(new ReportResult{GiornataId=request.GiornataId==0?101:request.GiornataId,Giornate=[new(){Id=101,Inizio=DateTime.Today.AddHours(18),Aperta=true},new(){Id=100,Inizio=DateTime.Today.AddDays(-1).AddHours(18)}],AggiornatoAl=DateTime.Now,SituazioneAl=DateTime.Now,Ingressi=18,Presenti=16,Uscite=2,
                Settori=[new(){Nome="Ingresso",Operazioni=18,Lordo=12,Pagato=12},new(){Nome="Bar",Operazioni=4,Lordo=12,Storni=3,Pagato=4,DaPagare=5},new(){Nome="Cucina"}],Ricavi=67,Incassi=62});
        }
    }
}
