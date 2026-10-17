using System.Text.Json;
using AppServices.Database;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Cassa.Core.Repository;
using Cassa.ViewModels;
using Cassa.Views;
using Leonardo18.DatabaseHost.Cassa;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Models.Context;
using Models.Entity.Global;
using Models.Tables;
using Menu.Core.Repository;
using Menu.ViewModels;
using ReactiveUI;
using ReactiveUI.Avalonia;
using ReactiveUI.Primitives;

internal static class Program
{
    private static int _checks;
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            if(args.Contains("--entry-sospesi") || args.Contains("--menu-xaml") || args.Contains("--bollini") || args.Contains("--rientri") || args.Contains("--fidelity") || args.Contains("--report") || args.Contains("--storno-ingresso"))
            {
                AppBuilder.Configure<TestApplication>().UseSkia()
                    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing=false })
                    .UseReactiveUI(_ => { }).SetupWithoutStarting();
                if(args.Contains("--storno-ingresso")) { Wait(StartChecks.Database()); Wait(IngressoContabilitaChecks.Database()); Wait(VociSpesaContabilitaChecks.Database()); Console.WriteLine($"PASS: {IngressoContabilitaChecks.Count} verifiche contabilità ingressi e {VociSpesaContabilitaChecks.Count} verifiche voci di spesa."); return 0; }
                if(args.Contains("--report")) { Wait(ReportChecks.Database()); ReportChecks.UI(); ReportChecks.PrintDocument(); Console.WriteLine($"PASS: {ReportChecks.Count} verifiche Report e stampa."); return 0; }
                if(args.Contains("--fidelity")) { Wait(StartChecks.Database()); Wait(FidelityChecks.Database()); FidelityChecks.UI(); Wait(ServiziChecks.Database()); ServiziChecks.UI(); Console.WriteLine($"PASS: {FidelityChecks.Count} verifiche Fidelity e {StartChecks.Count} verifiche migrazioni."); return 0; }
                if(args.Contains("--rientri")) { Wait(StartChecks.Database()); Wait(RientroChecks.Database()); RientroChecks.UI(); Console.WriteLine($"PASS: {RientroChecks.Count} verifiche rientri e {StartChecks.Count} verifiche migrazioni."); return 0; }
                if(args.Contains("--menu-xaml")) { var menu=new Menu.Views.MenuView(); if(menu.Content is null)throw new Exception("Menu senza contenuto XAML"); Console.WriteLine("PASS: MenuView carica il contenuto XAML compilato."); return 0; }
                if(args.Contains("--bollini")) { Wait(IngressiAbbonamentoChecks.Database()); IngressiAbbonamentoChecks.UI(); Console.WriteLine($"PASS: {IngressiAbbonamentoChecks.Count} verifiche ingressi abbonamento."); return 0; }
                AbbonamentoChecks.EntryPendingUI();
                Console.WriteLine("PASS: saldo sospesi in EntraSocio, conferma e riattivazione ingressi.");
                return 0;
            }
            Wait(DatabaseChecks()); Wait(ServiziChecks.Database()); Wait(AbbonamentoChecks.Database()); Wait(ContabilitaChecks.Database()); Wait(StartChecks.Database()); Wait(IngressoContabilitaChecks.Database()); Wait(VociSpesaContabilitaChecks.Database()); Wait(ReportChecks.Database()); Wait(UscitaChecks.Database()); Wait(RientroChecks.Database()); Wait(FidelityChecks.Database());
            ProtocolChecks();
            AppBuilder.Configure<TestApplication>().UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing=false })
                .UseReactiveUI(_ => { }).SetupWithoutStarting();
            UiChecks(); ServiziChecks.UI(); AbbonamentoChecks.UI(); ReportChecks.UI(); UscitaChecks.UI(); RientroChecks.UI(); FidelityChecks.UI();
            Console.WriteLine($"PASS: {_checks} verifiche Cassa (database isolato, protocollo, UI e chiusura).");
            Console.WriteLine($"PASS: {ServiziChecks.Count} verifiche Servizi (database isolato, protocollo, UI e chiusura).");
            Console.WriteLine($"PASS: {AbbonamentoChecks.Count} verifiche acquisto abbonamenti.");

            Console.WriteLine($"PASS: {ContabilitaChecks.Count} verifiche modello contabile.");
            Console.WriteLine($"PASS: {StartChecks.Count} verifiche ricostruzione Start.");
            Console.WriteLine($"PASS: {IngressoContabilitaChecks.Count} verifiche contabilizzazione ingressi.");
            Console.WriteLine($"PASS: {VociSpesaContabilitaChecks.Count} verifiche contabilità voci spesa.");
            Console.WriteLine($"PASS: {ReportChecks.Count} verifiche Report.");
            Console.WriteLine($"PASS: {UscitaChecks.Count} verifiche Esce Socio.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); _checks++; }
    private static void Wait(Task task)
    {
        var deadline=DateTime.UtcNow.AddSeconds(25);
        while (!task.IsCompleted)
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Verifica non completata.");
            if (Application.Current is not null) Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        task.GetAwaiter().GetResult();
    }
    private static void Command(ReactiveCommand<RxVoid,RxVoid> command)
    {
        var completion=new TaskCompletionSource();
        using var subscription=command.Execute().SubscribePrimitives(_ => { },
            ex => completion.TrySetException(ex), () => completion.TrySetResult());
        Wait(completion.Task); Dispatcher.UIThread.RunJobs();
    }
    private static void ProtocolChecks()
    {
        var json=JsonSerializer.Serialize(new DatabaseHostRequest {
            Operation="cassa",OperatorId=100,Cassa=new() { Action=CassaAction.RegisterEntry,
                PostazioneId=100,Posizione="A01",NumeroTessera="000123",TariffaId=100,Grb1=11,Grb2=22,Grb3=33,Grb4=44,ContoId=7,Pagato=true }
        }, DatabaseProtocolJsonContext.Default.DatabaseHostRequest);
        var request=JsonSerializer.Deserialize(json, DatabaseProtocolJsonContext.Default.DatabaseHostRequest);
        Check(request?.Cassa?.NumeroTessera == "000123" && request.Cassa.TariffaId == 100, "Protocollo richiesta Cassa.");
        Check(request?.Cassa is { Grb1:11,Grb2:22,Grb3:33,Grb4:44 },"Protocollo trasferisce i quattro campi guardaroba.");
        Check(request?.Cassa is { ContoId:7,Pagato:true },"Protocollo trasferisce identità e stato della voce conto.");
        json=JsonSerializer.Serialize(new DatabaseHostResponse { Success=true,Cassa=new() {
            Scheda=new() { Id=7,Nome="Mario",Cognome="Rossi" },Conto=[new() { VoicePrice=12.50m,Pagato=true }]
        } }, DatabaseProtocolJsonContext.Default.DatabaseHostResponse);
        var response=JsonSerializer.Deserialize(json, DatabaseProtocolJsonContext.Default.DatabaseHostResponse);
        Check(response?.Cassa?.Conto.Single().VoicePrice == 12.50m && response.Cassa.Scheda?.Anagrafica == "Rossi Mario", "Protocollo risposta Cassa.");
    }

    private static async Task DatabaseChecks()
    {
        await using var connection=new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options=new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using (var db=new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Postazioni.AddRange(new Postazione { Id=100,Nome="Cassa test",TipoPostazioneId=2,TipoRientroId=-1 },
                new Postazione { Id=101,Nome="Cassa senza permesso",TipoPostazioneId=2,TipoRientroId=-1 });
            db.Operatori.Add(new() { Id=100,Nome="Operatore test",PersonId=-1,Abilitato=true });
            db.Permessi.Add(new() { OperatoreId=100,PostazioneId=100 });
            db.Giornate.Add(new() { Aperta=true,DataInizio=DateTime.Now,DataFine=DateTime.MaxValue });
            db.Settori.Add(new() { Id=100,Nome="Ingresso test",TipoSettoreId=-1 });
            db.Reparti.Add(new() { PostazioneId=100,SettoreId=100 });
            db.Tariffe.AddRange(new Tariffa { Id=100,Nome="Ingresso standard",Label="Standard",Prezzo=12.50m,IsFreeDrink=true },
                new Tariffa { Id=101,Nome="Non in listino",Label="Non in listino",Prezzo=999 });
            db.Listini.Add(new() { SettoreId=100,TariffaId=100 });
            db.People.AddRange(Person(100,"000123",20991231,true),Person(101,"000124",20991231,true),
                Person(102,"SCADUTA",20200101,true),Person(103,"BLOCCATA",20991231,false));
            // Two swipes for the same new identity must produce one person, one membership and two cards.
            db.Strisciate.AddRange(new Strisciata { Cognome="Nuovo",Nome="Socio",Natoil=19900101,CodiceSocio="N1",NumeroTessera="N001",Scadenza=20991231 },
                new Strisciata { Cognome="Nuovo",Nome="Socio",Natoil=19900101,CodiceSocio="N1",NumeroTessera="N002",Scadenza=20991231 });
            await db.SaveChangesAsync();
        }
        var repository=new CassaDatabaseRepository(() => new AppDbContext(options));
        var result=await repository.Execute(100,new() { Action=CassaAction.Load,PostazioneId=100 });
        Check(result.PostazioneName == "Cassa test", "Caricamento postazione.");
        await Reject(() => repository.Execute(100,new() { Action=CassaAction.Load,PostazioneId=101 }),"permesso");
        await Reject(() => repository.Execute(999,new() { Action=CassaAction.Load,PostazioneId=100 }),"permesso");
        result=await repository.Execute(100,new() { Action=CassaAction.PrepareEntry,PostazioneId=100 });
        Check(result.Ingressi.Length == 1 && result.Ingressi[0].Id == 100 && result.Ingressi[0].FreeDrink, "Tariffe limitate al listino della postazione.");
        await using (var db=new AppDbContext(options))
        {
            Check(await db.Strisciate.CountAsync() == 0, "Importazione strisciate completata.");
            var person=await db.People.Include(x => x.Soci).ThenInclude(x => x.Tessere).SingleAsync(x => x.UniqueParam == "NUOSOC19900101");
            Check(person.Soci.Count == 1 && person.Soci[0].Tessere.Count == 2, "Importazione ripetuta senza duplicati nel batch.");
        }
        result=await repository.Execute(100,new() { Action=CassaAction.FindCard,PostazioneId=100,NumeroTessera="000123" });
        Check(result.Socio?.PersonId == 100 && result.Socio.NumeroSocio == "S100", "Identificazione tessera con zeri iniziali.");
        result=await repository.Execute(100,new() { Action=CassaAction.FindCard,PostazioneId=100,NumeroTessera="ASSENTE" });
        Check(result.Socio is null, "Tessera sconosciuta.");
        await Reject(() => repository.Execute(100,Entry("000123","A01",101)),"tariffa");
        await Reject(() => repository.Execute(100,Entry("SCADUTA","A01")),"scaduta");
        await Reject(() => repository.Execute(100,Entry("BLOCCATA","A01")),"abilitata");
        await Reject(() => repository.Execute(100,Entry("000123","")),"posizione");
        var write=Entry("000123"," A01 "); write.Note="Prova ingresso";
        result=await repository.Execute(100,write);
        Check(result.Scheda is { Id: > 0,Posizione:"A01",PersonId:100 }, "Registrazione ingresso e normalizzazione posizione.");
        result=await repository.Execute(100,new() { Action=CassaAction.OpenSheet,PostazioneId=100,Posizione="A01" });
        Check(result.Scheda?.NumeroTessera == "000123" && result.Conto.Length == 1
            && result.Conto[0].VoicePrice == 12.50m && result.Conto[0].VoiceDesc == "Standard" && result.Conto[0].Pagato
            && result.Conto[0].DescPostazione == "Cassa test", "Scheda e voce conto salvate insieme con tariffa autorevole.");
        await Reject(() => repository.Execute(100,Entry("000123","A02")),"già all'interno");
        await Reject(() => repository.Execute(100,Entry("000124","A01")),"occupata");
        result=await repository.Execute(100,new() { Action=CassaAction.ListInside,PostazioneId=100 });
        Check(result.Schede.Length == 1 && result.Schede[0].Note == "Prova ingresso", "Elenco soci presenti e nessuna scrittura parziale dopo rifiuti.");
        result=await repository.Execute(100,new() { Action=CassaAction.FindCard,PostazioneId=100,NumeroTessera="000123" });
        Check(result.Socio?.PosizioneOccupata == "A01", "Socio già presente individuato.");
        await Reject(() => repository.Execute(100,new() { Action=CassaAction.OpenSheet,PostazioneId=100,Posizione="ASSENTE" }),"Nessuna scheda");
        await using (var db=new AppDbContext(options))
        {
            db.Settori.Add(new() { Id=110,Nome="Bar",TipoSettoreId=-2 });
            db.Reparti.Add(new() { PostazioneId=100,SettoreId=110 });
            db.Tariffe.Add(new() { Id=110,Nome="Acqua naturale",Label="Acqua",Prezzo=2.50m });
            db.Listini.Add(new() { Id=110,SettoreId=110,TariffaId=110 });
            await db.SaveChangesAsync();
        }
        result=await repository.Execute(100,new() { Action=CassaAction.OpenSheet,PostazioneId=100,Posizione="A01" });
        Check(result.VociSpesa.Length==1 && result.VociSpesa[0].ListinoId==110,"Pulsanti limitati ai listini standard della postazione.");
        var charge=new CassaRequest { Action=CassaAction.AddCharge,PostazioneId=100,Posizione="A01",SchedaId=result.Scheda!.Id,ListinoId=110 };
        await using (var db=new AppDbContext(options)) { (await db.Tariffe.SingleAsync(t=>t.Id==110)).Prezzo=3m; await db.SaveChangesAsync(); }
        result=await repository.Execute(100,charge);
        Check(result.Conto.Length==2 && result.Conto.Last().VoicePrice==3m && !result.Conto.Last().Pagato
            && result.Conto.Last().DescSettore=="Bar" && result.Conto.Last().VoiceDesc=="Acqua","Addebito usa prezzo aggiornato dal database e ricarica il conto.");
        var payment=new CassaRequest { Action=CassaAction.SetPagamento,PostazioneId=100,Posizione="A01",
            SchedaId=charge.SchedaId,ContoId=result.Conto.Last().Id,Pagato=true };
        result=await repository.Execute(100,payment);
        Check(result.Conto.Length==2 && result.Conto.All(c=>c.Pagato),"Una voce può essere segnata come pagata.");
        result=await repository.Execute(100,payment);
        Check(result.Conto.Length==2 && result.Conto.All(c=>c.Pagato),"Ripetere lo stesso stato non inverte il pagamento né aggiunge righe.");
        payment.Pagato=false; result=await repository.Execute(100,payment);
        Check(!result.Conto.Single(c=>c.Id==payment.ContoId).Pagato && result.Conto.Single(c=>c.Id!=payment.ContoId).Pagato,
            "Ripristino da pagare modifica solo la voce richiesta.");
        var paymentLine=payment.ContoId; payment.ContoId=int.MaxValue;
        await Reject(()=>repository.Execute(100,payment),"voce di spesa"); payment.ContoId=paymentLine;
        await using (var db=new AppDbContext(options))
        {
            db.Schede.Add(new() { PersonId=101,Posizione="ALTRO_CONTO",SchedeConto=[new() { VoiceDesc="Altra scheda",VoicePrice=99m }] });
            await db.SaveChangesAsync();
            payment.ContoId=await db.SchedeConto.Where(c=>c.VoiceDesc=="Altra scheda").Select(c=>c.Id).SingleAsync();
        }
        await Reject(()=>repository.Execute(100,payment),"voce di spesa");
        await using (var db=new AppDbContext(options))
        {
            var foreignLine=await db.SchedeConto.SingleAsync(c=>c.Id==payment.ContoId);
            Check(!foreignLine.Pagato,"Pagamento non modifica righe di altre schede.");
            db.SchedeConto.Remove(foreignLine); db.Schede.Remove(await db.Schede.SingleAsync(s=>s.Posizione=="ALTRO_CONTO"));
            await db.SaveChangesAsync();
        }
        payment.ContoId=paymentLine; payment.SchedaId++;
        await Reject(()=>repository.Execute(100,payment),"cambiata"); payment.SchedaId--;
        payment.PostazioneId=101; await Reject(()=>repository.Execute(100,payment),"permesso"); payment.PostazioneId=100;
        charge.SchedaId++;
        await Reject(()=>repository.Execute(100,charge),"cambiata"); charge.SchedaId--;
        charge.ListinoId=100; await Reject(()=>repository.Execute(100,charge),"disponibile"); charge.ListinoId=110;
        charge.PostazioneId=101; await Reject(()=>repository.Execute(100,charge),"permesso"); charge.PostazioneId=100;
        await using (var db=new AppDbContext(options)) { (await db.Schede.SingleAsync()).Blocco=true; await db.SaveChangesAsync(); }
        await Reject(()=>repository.Execute(100,charge),"bloccata");
        await Reject(()=>repository.Execute(100,payment),"bloccata");
        await using (var db=new AppDbContext(options))
        {
            var sheet=await db.Schede.SingleAsync(); sheet.Blocco=false; sheet.CheckoutTime=DateTime.Now; await db.SaveChangesAsync();
        }
        await Reject(()=>repository.Execute(100,charge),"chiusa");
        await Reject(()=>repository.Execute(100,payment),"chiusa");
        await using (var db=new AppDbContext(options))
        {
            Check(await db.SchedeConto.CountAsync()==2,"Addebiti rifiutati non lasciano righe parziali.");
            (await db.Schede.SingleAsync()).CheckoutTime=DateTime.MaxValue;
            db.SchedeConto.Remove(await db.SchedeConto.SingleAsync(c=>c.VoiceDesc=="Acqua"));
            await db.SaveChangesAsync();
        }
        var guardaroba=new CassaRequest { Action=CassaAction.SaveGuardaroba,PostazioneId=100,Posizione="A01",
            SchedaId=charge.SchedaId,Grb1=101,Grb2=202,Grb3=303,Grb4=404 };
        result=await repository.Execute(100,guardaroba);
        Check(result.Scheda is { Grb1:101,Grb2:202,Grb3:303,Grb4:404 } && result.Conto.Length==0 && result.VociSpesa.Length==0,
            "Salvataggio guardaroba aggiorna la scheda senza addebitare voci.");
        guardaroba.Grb3=-1; await Reject(()=>repository.Execute(100,guardaroba),"negativi"); guardaroba.Grb3=303;
        guardaroba.SchedaId++; await Reject(()=>repository.Execute(100,guardaroba),"cambiata"); guardaroba.SchedaId--;
        guardaroba.PostazioneId=101; await Reject(()=>repository.Execute(100,guardaroba),"permesso"); guardaroba.PostazioneId=100;
        await using (var db=new AppDbContext(options)) { (await db.Schede.SingleAsync()).Blocco=true; await db.SaveChangesAsync(); }
        await Reject(()=>repository.Execute(100,guardaroba),"bloccata");
        await using (var db=new AppDbContext(options))
        {
            var sheet=await db.Schede.SingleAsync();
            Check(sheet.Grb1==101 && sheet.Grb2==202 && sheet.Grb3==303 && sheet.Grb4==404 && sheet.Note=="Prova ingresso",
                "Rifiuti guardaroba non modificano valori o altri dati della scheda.");
            sheet.Blocco=false; sheet.CheckoutTime=DateTime.Now; await db.SaveChangesAsync();
        }
        await Reject(()=>repository.Execute(100,guardaroba),"chiusa");
        await using (var db=new AppDbContext(options)) { (await db.Schede.SingleAsync()).CheckoutTime=DateTime.MaxValue; await db.SaveChangesAsync(); }
        guardaroba.Grb1=guardaroba.Grb2=guardaroba.Grb3=guardaroba.Grb4=0;
        result=await repository.Execute(100,guardaroba);
        Check(result.Scheda is { Grb1:0,Grb2:0,Grb3:0,Grb4:0 },"Zero libera i quattro campi guardaroba.");
        int legacyId,legacyReversalId,legacyOmaggioId;
        await using(var db=new AppDbContext(options))
        {
            var id=await db.Schede.Select(x=>x.Id).SingleAsync();
            var legacy=new SchedaConto { SchedaId=id,VoiceDesc="Acqua naturale",VoicePrice=3m,DataOra=DateTime.Now };
            var reversal=new SchedaConto { SchedaId=id,VoiceDesc="STORNO - Acqua naturale",VoicePrice=-3m,DataOra=DateTime.Now };
            var omaggio=new SchedaConto { SchedaId=id,VoiceDesc="INGRESSO OMAGGIO",VoicePrice=0m,DataOra=DateTime.Now };
            db.Tariffe.Add(new() { Id=111,Nome="INGRESSO OMAGGIO",Label="OMAGGIO",Prezzo=0m });
            db.SchedeConto.AddRange(legacy,reversal,omaggio); await db.SaveChangesAsync();
            legacyId=legacy.Id; legacyReversalId=reversal.Id; legacyOmaggioId=omaggio.Id;
        }
        result=await repository.Execute(100,new() { Action=CassaAction.OpenSheet,PostazioneId=100,Posizione="A01" });
        Check(result.Conto.Single(x=>x.Id==legacyId).VoiceDesc=="Acqua"
            && result.Conto.Single(x=>x.Id==legacyReversalId).VoiceDesc=="STORNO - Acqua",
            "Dettaglio mostra etichette anche per nomi storici e relativi storni.");
        Check(result.Conto.Single(x=>x.Id==legacyOmaggioId).VoiceDesc=="OMAGGIO",
            "Voce storica INGRESSO OMAGGIO viene mostrata come OMAGGIO anche con importo zero.");
        await using(var db=new AppDbContext(options))
        {
            Check((await db.SchedeConto.SingleAsync(x=>x.Id==legacyId)).VoiceDesc=="Acqua naturale",
                "Conversione delle etichette riguarda la visualizzazione e non riscrive le righe storiche.");
            db.SchedeConto.RemoveRange(db.SchedeConto.Where(x=>x.Id==legacyId || x.Id==legacyReversalId || x.Id==legacyOmaggioId));
            db.Tariffe.Remove(await db.Tariffe.SingleAsync(x=>x.Id==111)); await db.SaveChangesAsync();
        }
        _checks += await StornoChecks.Run(options,repository);
        _checks += await StrisciateChecks.Run(options, repository);
        await using (var db=new AppDbContext(options)) { foreach(var day in db.Giornate.Where(x => x.Aperta)) day.Aperta=false; await db.SaveChangesAsync(); }
        await Reject(() => repository.Execute(100,Entry("000124","A02")),"giornata");
        await Reject(()=>repository.Execute(100,guardaroba),"giornata");
        await Reject(()=>repository.Execute(100,payment),"giornata");
        await using (var db=new AppDbContext(options)) Check(await db.Schede.CountAsync() == 1 && await db.SchedeConto.CountAsync() == 1, "Giornata chiusa non modifica schede o conto.");
    }
    private static Person Person(int id,string card,int expiry,bool enabled) => new() {
        Id=id,FirstName="Mario",SurName="Rossi",Natoil=19900203,UniqueParam="TEST"+id,
        Soci=[new() { NumeroSocio="S"+id,Tessere=[new() { NumeroTessera=card,Scadenza=expiry,Abilitato=enabled }] }]
    };
    private static CassaRequest Entry(string card,string position,int rate=100) => new() {
        Action=CassaAction.RegisterEntry,PostazioneId=100,NumeroTessera=card,Posizione=position,TariffaId=rate
    };
    private static async Task Reject(Func<Task<CassaResult>> run,string expected)
    {
        try { await run(); }
        catch (InvalidOperationException ex) { Check(ex.Message.Contains(expected,StringComparison.OrdinalIgnoreCase),"Rifiuto: "+ex.Message); return; }
        throw new Exception("Richiesta non valida accettata: "+expected);
    }

    private static void UiChecks()
    {
        var repository=new FakeCassaRepository();
        var vm=new CassaViewModel(new Screen(),repository,100);
        var view=new CassaView { ViewModel=vm };
        var window=new Window { Width=1000,Height=760,Content=view };
        window.Show();
        WaitUntil(() => vm.IsUiReady);
        Check(vm.Title.Contains("Cassa test"),"Vista Cassa attivata e caricata.");
        vm.Posizione="A01"; Command(vm.OpenSheetCommand);
        Check(vm.HasSheet && vm.Sheet?.Anagrafica == "Rossi Mario" && vm.Totale == 12.50m, "Apertura scheda dalla vista.");
        Check(vm.HasVociSpesa && vm.VociSpesa.First().Descrizione=="Acqua","Voci di spesa caricate solo con la scheda aperta.");
        void Storna(CassaConto line)
        {
            var completion=new TaskCompletionSource();
            using var subscription=vm.StornaContoCommand.Execute(line).SubscribePrimitives(
                _=>{},ex=>completion.TrySetException(ex),()=>completion.TrySetResult());
            Wait(completion.Task); Dispatcher.UIThread.RunJobs();
        }
        var zeroStorno=new CassaConto { Id=3,VoicePrice=0m }; var negativeStorno=new CassaConto { Id=4,VoicePrice=-1m };
        vm.Conto.Add(zeroStorno); vm.Conto.Add(negativeStorno); var noOpActions=repository.Actions.Count;
        Dispatcher.UIThread.RunJobs();
        foreach(var line in vm.Conto.ToArray())
        {
            var row=view.GetVisualDescendants().OfType<StackPanel>()
                .Single(p=>p.Classes.Contains("contoRow") && ReferenceEquals(p.DataContext,line));
            var request=new Avalonia.Input.ContextRequestedEventArgs { RoutedEvent=Avalonia.Input.InputElement.ContextRequestedEvent };
            row.RaiseEvent(request); Dispatcher.UIThread.RunJobs();
            var menu=row.ContextMenu!;
            Check(request.Handled && menu.IsOpen,"Menu contestuale disponibile sulla riga senza scorrere alla colonna finale.");
            var item=menu.ItemsSource!.Cast<MenuItem>().Single();
            Check(ReferenceEquals(item.Command,vm.StornaContoCommand) && ReferenceEquals(item.CommandParameter,line)
                && item.IsEnabled==line.CanStornare,"Menu storna la riga indicata e disabilita importi zero o negativi.");
            menu.Close(); Dispatcher.UIThread.RunJobs();
            Check(row.ContextMenu is null,"Menu temporaneo rilasciato alla chiusura.");
        }
        Check(!view.GetVisualDescendants().OfType<Button>().Any(b=>Equals(b.Content,"STORNA")),"Colonna del pulsante Storna rimossa dalla griglia.");
        Storna(zeroStorno); Storna(negativeStorno);
        Check(repository.Actions.Count==noOpActions,"Storno di importi non positivi non invia richieste.");
        vm.Conto.Remove(zeroStorno); vm.Conto.Remove(negativeStorno);
        var stornoOriginal=vm.Conto.Single(); var stornoSectors=vm.SettoriSpesa; var stornoSheet=vm.Sheet;
        repository.FailStorno=true; Storna(stornoOriginal);
        Check(vm.Conto.Count==1 && vm.Totale==12.50m && vm.InfoLabel.Contains("prova storno"),"Errore storno non modifica il conto.");
        repository.FailStorno=false; vm.Grb1="88"; vm.SelectedSettoreSpesa=vm.SettoriSpesa.Last(); Storna(stornoOriginal);
        Check(vm.Conto.Count==2 && vm.Conto.Single(x=>x.VoicePrice<0) is { VoicePrice:-12.50m,VoiceDesc:"STORNO - Standard",Pagato:true },
            "Comando storno aggiunge descrizione e prezzo negativo.");
        Check(vm.Totale==0m && vm.DaPagare==0m && ReferenceEquals(stornoOriginal,vm.Conto.Single(x=>x.Id==1))
            && ReferenceEquals(stornoSectors,vm.SettoriSpesa) && ReferenceEquals(stornoSheet,vm.Sheet) && vm.Grb1=="88",
            "Storno aggiorna solo conto e totali mantenendo riga originale e pannelli.");
        Screenshot(window,"postazione-storno");
        vm.Grb1=""; Command(vm.OpenSheetCommand);
        var initialConto=vm.Conto.ToArray(); vm.Conto.Clear();
        vm.Conto.Add(new() { Id=1,DataOra=new(2026,1,1),VoicePrice=10m,DescPostazione="C",DescSettore="B",VoiceDesc="A",Pagato=true });
        vm.Conto.Add(new() { Id=2,DataOra=new(2025,1,1),VoicePrice=2m,DescPostazione="A",DescSettore="C",VoiceDesc="B",Pagato=false });
        vm.Conto.Add(new() { Id=3,DataOra=new(2024,1,1),VoicePrice=30m,DescPostazione="B",DescSettore="A",VoiceDesc="C",Pagato=true });
        foreach (var sort in new[] { ("SortPrezzoButton",2,3), ("SortDataButton",3,1), ("SortPostazioneButton",2,1),
            ("SortSettoreButton",3,2), ("SortDettaglioButton",1,3), ("SortStatoButton",2,3) })
        {
            var header=view.FindControl<Button>(sort.Item1)!;
            Check(header.Command?.CanExecute(header.CommandParameter)==true,"Intestazione ordinabile: " + sort.Item1);
            header.Command!.Execute(header.CommandParameter); Dispatcher.UIThread.RunJobs();
            Check(vm.Conto.First().Id==sort.Item2 && vm.Conto.Count==3,"Ordine crescente: " + sort.Item1);
            header.Command.Execute(header.CommandParameter); Dispatcher.UIThread.RunJobs();
            Check(vm.Conto.First().Id==sort.Item3 && vm.Conto.Count==3,"Ordine decrescente: " + sort.Item1);
        }
        var priceHeader=view.FindControl<Button>("SortPrezzoButton")!;
        priceHeader.Command!.Execute(priceHeader.CommandParameter); priceHeader.Command.Execute(priceHeader.CommandParameter);
        vm.Conto.Clear(); foreach(var line in initialConto) vm.Conto.Add(line);
        void SetPayment()
        {
            var completion=new TaskCompletionSource();
            using var subscription=vm.SetPagamentoCommand.Execute(vm.Conto.Single()).SubscribePrimitives(
                _=>{},ex=>completion.TrySetException(ex),()=>completion.TrySetResult());
            Wait(completion.Task); Dispatcher.UIThread.RunJobs();
        }
        Dispatcher.UIThread.RunJobs();
        var paymentRow=vm.Conto.Single(); var paymentSheet=vm.Sheet; var paymentSectors=vm.SettoriSpesa;
        var paymentCheckbox=view.GetVisualDescendants().OfType<CheckBox>().Single();
        var paymentCollectionChanges=0;
        System.Collections.Specialized.NotifyCollectionChangedEventHandler paymentCollectionHandler=(_,_)=>paymentCollectionChanges++;
        vm.Conto.CollectionChanged+=paymentCollectionHandler;
        SetPayment();
        vm.Conto.CollectionChanged-=paymentCollectionHandler;
        Check(paymentCollectionChanges==0 && ReferenceEquals(paymentRow,vm.Conto.Single())
            && ReferenceEquals(paymentCheckbox,view.GetVisualDescendants().OfType<CheckBox>().Single()),
            "Pagamento aggiorna la riga esistente senza ricreare collezione o casella.");
        Check(ReferenceEquals(paymentSheet,vm.Sheet) && ReferenceEquals(paymentSectors,vm.SettoriSpesa),
            "Pagamento non ricarica anagrafica o tab dei settori.");
        Check(!vm.Conto.Single().Pagato && vm.DaPagare==12.50m && vm.Totale==12.50m,"Casella pagamento aggiorna stato e da pagare lasciando invariato il totale.");
        Check(view.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked==false,"Casella mostra lo stato salvato.");
        repository.FailPayment=true;
        view.GetVisualDescendants().OfType<CheckBox>().Single().SetCurrentValue(Avalonia.Controls.Primitives.ToggleButton.IsCheckedProperty,true);
        SetPayment();
        Check(!vm.Conto.Single().Pagato && vm.DaPagare==12.50m && vm.InfoLabel.Contains("prova pagamento"),"Errore pagamento conserva stato e totali precedenti.");
        Check(view.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked==false,"Errore ripristina anche la casella visiva.");
        repository.FailPayment=false;
        vm.Grb1="77"; vm.SelectedSettoreSpesa=vm.SettoriSpesa.Last(); SetPayment();
        Check(vm.Conto.Single().Pagato && vm.DaPagare==0 && vm.Grb1=="77" && vm.SelectedSettoreSpesa?.Nome=="Guardaroba",
            "Pagamento inverso conserva modifiche guardaroba e settore selezionato.");
        var pendingPayment=new TaskCompletionSource<CassaResult>(); repository.Pending=pendingPayment;
        var paymentCompletion=new TaskCompletionSource();
        using (var inlineSubscription=vm.SetPagamentoCommand.Execute(vm.Conto.Single()).SubscribePrimitives(
            _=>{},ex=>paymentCompletion.TrySetException(ex),()=>paymentCompletion.TrySetResult()))
        {
            WaitUntil(()=>vm.IsPaymentBusy);
            Check(!vm.IsBusy && !vm.IsWaiting && vm.IsUiReady,"Pagamento non attiva il caricamento generale della pagina.");
            Check(!view.FindControl<Grid>("SheetAccount")!.IsEnabled && view.FindControl<Grid>("SheetCharges")!.IsEnabled,
                "Durante il salvataggio viene disabilitata soltanto la griglia del conto.");
            pendingPayment.SetResult(new() { Scheda=new() { Id=999,Nome="Non sostituire" },
                Conto=[new() { Id=paymentRow.Id,Pagato=false }] });
            Wait(paymentCompletion.Task);
        }
        repository.Pending=null;
        Check(ReferenceEquals(paymentSheet,vm.Sheet) && ReferenceEquals(paymentSectors,vm.SettoriSpesa)
            && ReferenceEquals(paymentRow,vm.Conto.Single()) && vm.Totale==12.50m && vm.DaPagare==12.50m,
            "Risposta pagamento aggiorna soltanto lo stato della riga e il residuo.");
        SetPayment();
        vm.Grb1=""; vm.SelectedSettoreSpesa=vm.SettoriSpesa.First();
        Check(vm.SettoriSpesa.Length==2 && vm.SettoriSpesa[0].Nome=="Bar" && vm.SettoriSpesa[0].Voci.Length==2
            && vm.SettoriSpesa[1].Nome=="Guardaroba" && vm.SettoriSpesa[1].Voci.Length==1,"Voci raggruppate per settore con un'intestazione per gruppo.");
        Screenshot(window,"postazione");
        window.Width=1400; window.Height=900; Dispatcher.UIThread.RunJobs();
        Screenshot(window,"postazione-ampia");
        var tabs=view.FindControl<TabControl>("SectorTabs")!;
        Check(tabs.ItemCount==2 && tabs.IsVisible,"Settori visualizzati come tab.");
        vm.SelectedSettoreSpesa=vm.SettoriSpesa.Last(); Dispatcher.UIThread.RunJobs();
        Check(ReferenceEquals(tabs.SelectedItem,vm.SelectedSettoreSpesa),"Tab selezionato collegato al settore corrente.");
        Screenshot(window,"postazione-settore");
        for (var i=0;i<60;i++) vm.Conto.Add(new() { VoiceDesc="Voce di prova " + i,VoicePrice=1m });
        Dispatcher.UIThread.RunJobs(); Screenshot(window,"postazione-conto-lungo");
        var accountScroll=view.FindControl<ScrollViewer>("AccountScroll")!;
        Check(accountScroll.Viewport.Height>0 && accountScroll.Extent.Height>accountScroll.Viewport.Height,
            "Conto lungo scorre all'interno della propria griglia.");
        var tabBottom=tabs.TranslatePoint(new Point(0,tabs.Bounds.Height),view);
        Check(tabBottom is { } bottom && bottom.Y<=view.Bounds.Height,
            "Con molte righe i tab restano nella vista fissa.");
        vm.Grb1="101"; vm.Grb2="202"; vm.Grb3="303"; vm.Grb4="404";
        var addebito=new TaskCompletionSource();
        using (var chargeSubscription=vm.AddChargeCommand.Execute(vm.VociSpesa.First()).SubscribePrimitives(
            _=>{ },ex=>addebito.TrySetException(ex),()=>addebito.TrySetResult())) Wait(addebito.Task);
        Check(vm.Conto.Count==2 && vm.DaPagare==3m && repository.Actions.Contains(CassaAction.AddCharge),"Pulsante di addebito ricarica conto e importo da pagare.");
        Check(vm.Conto.First().VoicePrice==12.50m && vm.PrezzoHeader.EndsWith("▼"),"Aggiornamento conto mantiene colonna e direzione scelte.");
        Check(vm.SelectedSettoreSpesa?.Nome=="Guardaroba","Addebito conserva il settore selezionato.");
        Check(vm.Grb1=="101" && vm.Grb2=="202" && vm.Grb3=="303" && vm.Grb4=="404","Addebito non perde modifiche guardaroba ancora da salvare.");
        vm.Grb1="2147483648";
        Command(vm.SaveGuardarobaCommand);
        Check(vm.GuardarobaMessage.Contains("Grb1") && !repository.Actions.Contains(CassaAction.SaveGuardaroba),"Guardaroba rifiuta valori fuori intervallo prima di inviarli.");
        var guardarobaSheet=vm.Sheet; var guardarobaRows=vm.Conto.ToArray(); var guardarobaSectors=vm.SettoriSpesa;
        vm.Grb1="101"; Command(vm.SaveGuardarobaCommand);
        Check(vm.Sheet is { Grb1:101,Grb2:202,Grb3:303,Grb4:404 },"Comando guardaroba aggiorna i quattro campi della scheda.");
        Check(ReferenceEquals(guardarobaSheet,vm.Sheet) && ReferenceEquals(guardarobaSectors,vm.SettoriSpesa)
            && vm.Conto.SequenceEqual(guardarobaRows) && vm.SelectedSettoreSpesa?.Nome=="Guardaroba",
            "Salva guardaroba conserva scheda, righe conto, ordinamento e tab esistenti.");
        var guardarobaPanel=view.FindControl<GuardarobaControl>("GuardarobaPanel")!;
        var guardarobaSave=guardarobaPanel.FindControl<ControlStyles.UiIconButton>("GuardarobaSaveButton")!;
        Check(guardarobaSave.IconSymbol==FluentIcons.Common.Symbol.Save && guardarobaSave.IconVariant==FluentIcons.Common.IconVariant.Filled,
            "Salva guardaroba usa una variante disponibile della icona Save.");
        Check(view.FindControl<ControlStyles.UiTextBox>("PosizioneBox")!.IsFocused,"Dopo salva guardaroba il focus torna su Posizione.");
        repository.FailGuardaroba=true; vm.Grb1="505"; Command(vm.SaveGuardarobaCommand);
        Check(vm.GuardarobaMessage.Contains("prova guardaroba") && vm.Grb1=="505" && vm.Sheet!.Grb1==101
            && vm.Conto.SequenceEqual(guardarobaRows),"Errore guardaroba resta locale e conserva modifiche e conto.");
        repository.FailGuardaroba=false; vm.Grb1="101";
        var wardrobePending=new TaskCompletionSource<CassaResult>(); repository.Pending=wardrobePending;
        var wardrobeCompletion=new TaskCompletionSource();
        using(var wardrobeSubscription=vm.SaveGuardarobaCommand.Execute().SubscribePrimitives(
            _=>{},ex=>wardrobeCompletion.TrySetException(ex),()=>wardrobeCompletion.TrySetResult()))
        {
            WaitUntil(()=>vm.IsGuardarobaBusy);
            Check(!vm.IsBusy && !vm.IsWaiting && vm.IsUiReady && !guardarobaPanel.IsEnabled
                && view.FindControl<Grid>("SheetAccount")!.IsEnabled && view.FindControl<Grid>("SheetCharges")!.IsEnabled,
                "Salvataggio attiva soltanto GuardarobaControl senza bloccare o nascondere il resto della pagina.");
            wardrobePending.SetResult(new() { Scheda=new() { Id=guardarobaSheet!.Id,Nome="Non sostituire",Grb1=101,Grb2=202,Grb3=303,Grb4=404 } });
            Wait(wardrobeCompletion.Task);
        }
        repository.Pending=null;
        Check(ReferenceEquals(guardarobaSheet,vm.Sheet) && ReferenceEquals(guardarobaSectors,vm.SettoriSpesa)
            && vm.Conto.SequenceEqual(guardarobaRows) && vm.Sheet!.Nome!="Non sostituire",
            "Risposta guardaroba aggiorna soltanto Grb1-Grb4 senza ricaricare anagrafica o griglia.");
        Screenshot(window,"postazione-guardaroba");
        Command(vm.ClearSheetCommand);
        Check(!vm.HasSheet && !vm.HasVociSpesa && vm.Conto.Count==0,"Chiusura scheda nasconde conto e pulsanti di addebito.");
        vm.Posizione="A01"; Command(vm.OpenSheetCommand);
        Check(vm.Grb1=="101" && vm.Grb4=="404","Riapertura scheda ricarica il guardaroba salvato.");
        window.Width=1000; window.Height=760; Dispatcher.UIThread.RunJobs();
        Command(vm.EntraSocioCommand); vm.NumeroTessera="000123"; Command(vm.FindCardCommand);
        vm.EntryPosition="A02";
        Check(vm.HasMember && vm.EntryWarning == "", "Ingresso identificato e validato.");
        Screenshot(window,"ingresso");
        vm.NumeroTessera="ALTRA";
        Check(!vm.HasMember && vm.EntryWarning.Contains("Identifica"),"Cambio numero tessera invalida il socio precedente.");
        vm.NumeroTessera="000123"; Command(vm.FindCardCommand);
        vm.EntryPosition="OCCUPATA";
        Check(vm.EntryWarning.Contains("occupata"),"Posizione occupata blocca l'ingresso.");
        vm.EntryPosition="A02"; Command(vm.RegisterEntryCommand);
        Check(vm.IsPostazione && vm.HasSheet && repository.Actions.Contains(CassaAction.RegisterEntry),"Ingresso torna alla scheda della postazione.");
        Command(vm.ListaSociCommand);
        Check(vm.IsList && vm.Schede.Count == 2,"Elenco soci nella vista.");
        vm.SearchText="Mario"; Check(vm.Schede.Count == 1,"Filtro per anagrafica.");
        Screenshot(window,"elenco");
        window.Width=720; window.Height=540; Screenshot(window,"elenco-compatto");
        window.Width=1000; window.Height=760;
        Command(vm.OpenSelectedCommand); Check(vm.IsPostazione && vm.HasSheet,"Elenco riapre la scheda selezionata.");
        // A pending response must not mutate collections or disposed signals after detaching the view.
        var pending=new TaskCompletionSource<CassaResult>(); repository.Pending=pending;
        var done=new TaskCompletionSource();
        using var subscription=vm.ListaSociCommand.Execute().SubscribePrimitives(_=>{},ex=>done.TrySetException(ex),()=>done.TrySetResult());
        Check(vm.IsBusy,"Richiesta pendente in corso.");
        window.Close();
        pending.SetResult(new() { Schede=[new() { Id=999 }] });
        Dispatcher.UIThread.RunJobs();
        WaitUntil(() => repository.PendingReturned);
        Check(repository.Actions.Contains(CassaAction.StopStrisciate),"Chiusura vista arresta il ricevitore della sessione.");
        Check(vm.Schede.Count == 0,"Risposta tardiva ignorata dopo distruzione della vista.");
        vm.EntryPosition="DOPO CHIUSURA"; vm.NumeroTessera="DOPO CHIUSURA"; vm.SearchText="DOPO CHIUSURA";
        Check(vm.Schede.Count == 0,"Binding tardivi non accedono ai segnali eliminati.");

        var failedVm=new CassaViewModel(new Screen(),new FailingRepository(),100);
        using var activation=failedVm.Activator.Activate(); WaitUntil(() => !failedVm.IsBusy && failedVm.InfoLabel.Length != 0);
        bool returned=false; using var menuSubscription=failedVm.CassaToMenu.SubscribePrimitives(_=>returned=true);
        Command(failedVm.MenuCommand); Check(returned,"Ritorno al menu disponibile anche se il caricamento fallisce.");

        var returnRepository=new FakeCassaRepository();
        var returnVm=new CassaViewModel(new Screen(),returnRepository,100);
        using var returnActivation=returnVm.Activator.Activate(); WaitUntil(() => returnVm.IsUiReady);
        var stoppedBeforeMenu=false;
        using var returnSubscription=returnVm.CassaToMenu.SubscribePrimitives(_ => stoppedBeforeMenu=returnRepository.Actions.Last() == CassaAction.StopStrisciate);
        Command(returnVm.MenuCommand);
        Check(stoppedBeforeMenu,"Ricevitore arrestato prima della navigazione al Menu.");

        GlobalValuesC.MySetting=new() { IDOPERATORE=100,POSTAZIONI=[new() { CODICEPOSTAZIONE=100,TIPOPOSTAZIONE=2 }] };
        var menuVm=new MenuViewModel(new FakeMenuRepository(),new Screen());
        using var menuActivation=menuVm.Activator.Activate(); WaitUntil(() => menuVm.IsMenuReady);
        string destination=""; using var navigation=menuVm.Navigation.SubscribePrimitives(x=>destination=x);
        Command(menuVm.CassaPostazioneCommand);
        Check(destination == "cassa" && GlobalValuesC.MyPostazione?.CODICEPOSTAZIONE == 100,"Menu apre la Cassa selezionata.");
        GlobalValuesC.MySetting=null; GlobalValuesC.MyPostazione=null;
    }
    private static void WaitUntil(Func<bool> condition)
    {
        var deadline=DateTime.UtcNow.AddSeconds(15);
        while (!condition()) { if (DateTime.UtcNow > deadline) throw new TimeoutException("Attesa UI."); Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); }
        Dispatcher.UIThread.RunJobs();
    }
    private static void Screenshot(Window window,string name)
    {
        // Captures use test data and are saved beside this runner, not in the user's database.
        Dispatcher.UIThread.RunJobs();
        using var bitmap=window.CaptureRenderedFrame();
        Check(bitmap is not null,"Rendering "+name);
        var directory=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../previews"));
        Directory.CreateDirectory(directory); bitmap!.Save(Path.Combine(directory,name+".png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }
    private sealed class Screen : ReactiveObject,IScreen { public RoutingState Router { get; }=new(); }
    private sealed class FailingRepository : ICassaRepository
    { public Task<CassaResult> Execute(CassaRequest request,CancellationToken ct=default) => Task.FromException<CassaResult>(new DatabaseHostException("Errore di prova")); }
    private sealed class FakeMenuRepository : IMenuRepository
    {
        public Task<MenuState> Execute(int operatorId,string operation,CancellationToken ct=default) => Task.FromResult(new MenuState {
            Stations=[new() { Id=100,Name="Cassa test",TypeId=2 }],Day=new() { Id=1,Open=true }
        });
    }
    private sealed class FakeCassaRepository : ICassaRepository
    {
        private int[] _wardrobe=[0,0,0,0];
        private bool _entryPaid=true;
        public bool FailPayment { get; set; }
        public bool FailStorno { get; set; }
        public bool FailGuardaroba { get; set; }
        public List<CassaAction> Actions { get; }=[];
        public TaskCompletionSource<CassaResult>? Pending { get; set; }
        public bool PendingReturned { get; private set; }
        public async Task<CassaResult> Execute(CassaRequest request,CancellationToken ct=default)
        {
            Actions.Add(request.Action);
            if (Pending is { } pending) { var result=await pending.Task; PendingReturned=true; return result; }
            if (request.Action==CassaAction.SaveGuardaroba)
            {
                if (FailGuardaroba) throw new DatabaseHostException("Errore di prova guardaroba");
                _wardrobe=[request.Grb1,request.Grb2,request.Grb3,request.Grb4];
            }
            if (request.Action==CassaAction.SetPagamento)
            {
                if (FailPayment) throw new DatabaseHostException("Errore di prova pagamento");
                _entryPaid=request.Pagato;
            }
            if (request.Action==CassaAction.StornaConto && FailStorno) throw new DatabaseHostException("Errore di prova storno");
            return request.Action switch {
                CassaAction.Load => new() { PostazioneName="Cassa test",StrisciateEndpoint="http://127.0.0.1:5000" },
                CassaAction.StopStrisciate => new(),
                CassaAction.StornaConto => new() { Conto=[
                    new() { Id=1,VoiceDesc="Standard",VoicePrice=12.50m,Pagato=_entryPaid },
                    new() { Id=2,VoiceDesc="STORNO - Standard",VoicePrice=-12.50m,Pagato=_entryPaid,DataOra=DateTime.Now }] },
                CassaAction.AddCharge => new() { Scheda=new() { Id=request.SchedaId,Posizione=request.Posizione,Nome="Mario",Cognome="Rossi" },
                    VociSpesa=[new() { ListinoId=110,Settore="Bar",Descrizione="Acqua",Prezzo=3m },
                        new() { ListinoId=111,Settore="Bar",Descrizione="Caffè",Prezzo=2m },
                        new() { ListinoId=112,Settore="Guardaroba",Descrizione="Deposito",Prezzo=5m }],
                    Conto=[new() { VoiceDesc="Standard",VoicePrice=12.50m,Pagato=true },new() { VoiceDesc="Acqua",VoicePrice=3m }] },
                CassaAction.OpenSheet or CassaAction.SaveGuardaroba or CassaAction.SetPagamento => new() { Scheda=new() { Id=1,Posizione=request.Posizione,Nome="Mario",Cognome="Rossi",
                    Grb1=_wardrobe[0],Grb2=_wardrobe[1],Grb3=_wardrobe[2],Grb4=_wardrobe[3],
                    NumeroTessera="000123",Natoil=19900203,CheckinTime=new(2026,10,14,19,15,0) },
                    VociSpesa=[new() { ListinoId=110,Settore="Bar",Descrizione="Acqua",Prezzo=3m },
                        new() { ListinoId=111,Settore="Bar",Descrizione="Caffè",Prezzo=2m },
                        new() { ListinoId=112,Settore="Guardaroba",Descrizione="Deposito",Prezzo=5m }],
                    Conto=[new() { Id=1,VoiceDesc="Standard",DescSettore="Ingresso",DescPostazione="Cassa test",VoicePrice=12.50m,Pagato=_entryPaid,
                        DataOra=new(2026,10,14,19,15,0),Note="Ingresso registrato" }] },
                CassaAction.PrepareEntry => new() { Ingressi=[new() { Id=100,Nome="Ingresso standard",Etichetta="Standard",Prezzo=12.50m,FreeDrink=true }],PosizioniOccupate=["OCCUPATA"] },
                CassaAction.FindCard => new() { Socio=new() { PersonId=100,NumeroTessera=request.NumeroTessera,NumeroSocio="S100",
                    Nome="Mario",Cognome="Rossi",Natoil=19900203,ScadenzaTessera=20991231 },PosizioniOccupate=["OCCUPATA"] },
                CassaAction.RegisterEntry => new() { Scheda=new() { Id=1,Posizione=request.Posizione } },
                CassaAction.ListInside => new() { Schede=[new() { Id=1,Posizione="A01",Nome="Mario",Cognome="Rossi",NumeroTessera="000123",Natoil=19900203,
                    CheckinTime=new(2026,10,14,19,15,0) },new() { Id=2,Posizione="A03",Nome="Giulia",Cognome="Verdi",NumeroTessera="000124" }] },
                _ => throw new Exception("Azione inattesa")
            };
        }
    }
}

public sealed class TestApplication : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(new Uri("avares://Cassa.Tests/")) { Source=new Uri("avares://ControlStyles/Themes/MyStyles.axaml") });
    }
}
