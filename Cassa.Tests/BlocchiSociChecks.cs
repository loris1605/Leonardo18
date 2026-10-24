using AppServices.Database;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cassa.Core.Repository;
using Cassa.ViewModels;
using Cassa.Views;
using Leonardo18.DatabaseHost.Cassa;
using Leonardo18.DatabaseHost.Soci;
using Leonardo18.DatabaseHost.Strisciate;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Models.Context;
using Soci.Core.Repository;
using Soci.ViewModels;
using Soci.Views;
using ReactiveUI;
using ReactiveUI.Primitives;
using System.Text.Json;

internal static class BlocchiSociChecks
{
    public static int Count;
    private static void Check(bool ok,string text){if(!ok)throw new Exception(text);Count++;}
    private static async Task Reject(Func<Task> action,string text)
    {
        try{await action();}
        catch(Exception e)when((e is SociValidationException or InvalidOperationException)&&e.Message.Contains(text,StringComparison.OrdinalIgnoreCase)){Count++;return;}
        throw new Exception("Operazione non rifiutata: "+text);
    }
    public static async Task Database()
    {
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        var options=new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using(var db=new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Postazioni.AddRange(new(){Id=100,Nome="Cassa",TipoPostazioneId=2,TipoRientroId=-1},new(){Id=200,Nome="Soci",TipoPostazioneId=1,TipoRientroId=-1},new(){Id=101,Nome="Altra cassa",TipoPostazioneId=2,TipoRientroId=-1});
            db.Operatori.AddRange(new(){Id=100,Nome="Gestore",PersonId=-1,Abilitato=true},new(){Id=101,Nome="Solo cassa",PersonId=-1,Abilitato=true});
            db.Permessi.AddRange(new(){OperatoreId=100,PostazioneId=100},new(){OperatoreId=100,PostazioneId=101},new(){OperatoreId=100,PostazioneId=200},new(){OperatoreId=101,PostazioneId=100});
            db.Giornate.Add(new(){Id=100,Aperta=true,DataInizio=DateTime.Today,DataFine=DateTime.MaxValue});
            db.People.AddRange(new(){Id=100,SurName="Rossi",FirstName="Mario",Natoil=19900101,UniqueParam="ROSMAR19900101",
                Soci=[new(){NumeroSocio="100",Tessere=[new(){NumeroTessera="T100",Scadenza=20991231,Abilitato=true},new(){NumeroTessera="ALT100",Scadenza=20991231,Abilitato=true}]},new(){NumeroSocio="200",Tessere=[new(){NumeroTessera="ALT200",Scadenza=20991231,Abilitato=true}]}]},
                new(){Id=101,SurName="Bianchi",FirstName="Paolo",Natoil=19900101,UniqueParam="BIAPAO19900101",Soci=[new(){NumeroSocio="101",Tessere=[new(){NumeroTessera="T101",Scadenza=20991231,Abilitato=true}]}]});
            db.Settori.Add(new(){Id=100,Nome="Ingresso",TipoSettoreId=-1});
            db.Reparti.AddRange(new(){PostazioneId=100,SettoreId=100},new(){PostazioneId=101,SettoreId=100});
            db.Tariffe.Add(new(){Id=100,Nome="Omaggio",Label="Omaggio",Prezzo=0});db.Listini.Add(new(){Id=100,SettoreId=100,TariffaId=100});
            db.TipiAbbonamento.Add(new(){Id=100,Nome="Due ingressi",NumeroIngressi=2,DurataGiorni=30,Prezzo=0,Attivo=true});
            db.TipiFidelity.Add(new(){Id=100,Nome="Fidelity",TipoFidelityInputId=1,TipoFidelityOutputId=1,Traguardo=2,Attivo=true});
            await db.SaveChangesAsync();
        }
        var soci=new SociDatabaseRepository(()=>new AppDbContext(options));var cassa=new CassaDatabaseRepository(()=>new AppDbContext(options));
        SociRequest Block(bool active,DateTime? until=null,string? reason=null)=>new(){Action="person-block",Record=new(){Id=100,BloccoSocio=active,BloccoSocioFino=until,BloccoSocioMotivo=reason??"Comportamento grave"}};
        CassaRequest Find(string card="T100")=>new(){Action=CassaAction.FindCard,PostazioneId=100,NumeroTessera=card};
        CassaRequest Entry(string card="T100")=>new(){Action=CassaAction.RegisterEntry,PostazioneId=101,NumeroTessera=card,Posizione="B100",TariffaId=100};
        CassaRequest Buy(string first="T100",string second="")=>new(){Action=CassaAction.BuyAbbonamento,PostazioneId=100,NumeroTessera=first,NumeroTessera2=second,PersonId1=first=="T100"?100:101,PersonId2=second.Length>0?100:0,TipoAbbonamentoId=100,PrezzoConfermato=0,IngressiConfermati=2,DurataConfermata=30,Pagato=true,AcquistoId=Guid.NewGuid()};
        CassaRequest Activate()=>new(){Action=CassaAction.ActivateFidelity,PostazioneId=100,NumeroTessera="T100",PersonId1=100,TipoFidelityId=100};
        Check((await cassa.Execute(100,Find())).Socio?.SocioBloccato==false,"Identificazione precedente al blocco");
        await Reject(()=>soci.Execute(101,Block(true)),"permesso");
        await Reject(()=>soci.Execute(100,Block(true,reason:"")),"motivazione");
        await Reject(()=>soci.Execute(100,Block(true,reason:new string('x',1001))),"1000");
        await Reject(()=>soci.Execute(100,Block(true,DateTime.Today.AddDays(-1))),"passato");
        var reason=new string('x',1000);await soci.Execute(100,Block(true,reason:reason));
        var records=(await soci.Execute(100,new(){Action="load",Search=new(){PersonId=100}})).Records;
        Check(records.Length==3&&records.All(x=>x.BloccoSocio&&x.BloccoSocioFino is null&&x.BloccoSocioMotivo.Length==1000),"Blocco salvato sull'anagrafica per tutte le tessere e codici socio");
        foreach(var card in new[]{"T100","ALT100","ALT200"})
        {
            var found=await cassa.Execute(100,Find(card));
            Check(found.Socio is {SocioBloccato:true}&&found.Socio.AvvisoBlocco.Contains("indeterminato")&&found.TipiFidelity.Length==0&&found.AbbonamentiIngresso.Length==0,"Identificazione bloccata con motivazione per "+card);
            await Reject(()=>cassa.Execute(100,Entry(card)),"SOCIO BLOCCATO");
        }
        var reentry=Entry();reentry.Rientro=true;reentry.TariffaId=0;await Reject(()=>cassa.Execute(100,reentry),"SOCIO BLOCCATO");
        var subscription=Entry();subscription.AbbonamentoIngressoId=123;subscription.TariffaId=0;await Reject(()=>cassa.Execute(100,subscription),"SOCIO BLOCCATO");
        var fidelity=Entry();fidelity.IngressoFidelityId=123;fidelity.TariffaId=0;await Reject(()=>cassa.Execute(100,fidelity),"SOCIO BLOCCATO");
        await Reject(()=>cassa.Execute(100,Buy()),"SOCIO BLOCCATO");
        await Reject(()=>cassa.Execute(100,Buy("T101","T100")),"SOCIO BLOCCATO");
        await Reject(()=>cassa.Execute(100,Activate()),"SOCIO BLOCCATO");
        await using(var db=new AppDbContext(options))Check(!await db.Schede.AnyAsync()&&!await db.Abbonamenti.AnyAsync()&&!await db.Fidelities.AnyAsync()&&!await db.ContabilitaMovimenti.AnyAsync(),"Rifiuti non creano ingressi, acquisti, fidelity o movimenti");
        Check((await cassa.Execute(100,Find("T101"))).Socio?.SocioBloccato==false,"Altri soci restano abilitati");
        await new StrisciateDatabaseSink(()=>new AppDbContext(options),100,100).InsertAsync(new("100","T100","Rossi","Mario",19900101,20991231),CancellationToken.None);
        Check((await cassa.Execute(100,Find())).Socio?.SocioBloccato==true,"Nuova importazione tessera non rimuove il blocco");
        await soci.Execute(100,new(){Action="person-update",Record=new(){Id=100,Cognome="Rossi",Nome="Mario",Natoil=19900101}});
        Check((await cassa.Execute(100,Find())).Socio?.SocioBloccato==true,"Modifica anagrafica non rimuove il blocco");
        await soci.Execute(100,Block(true,DateTime.Today));
        Check((await cassa.Execute(100,Find())).Socio?.SocioBloccato==true,"Scadenza odierna inclusa");
        await soci.Execute(100,Block(false));
        Check((await cassa.Execute(100,Find())).Socio?.SocioBloccato==false,"Sblocco esplicito immediato");
        Check((await cassa.Execute(100,Buy())).AcquistoAbbonamento is not null,"Acquisto consentito dopo sblocco");
        Check((await cassa.Execute(100,Activate())).Fidelity?.Attiva==true,"Attivazione Fidelity consentita dopo sblocco");
        await using(var db=new AppDbContext(options)){var p=await db.People.SingleAsync(x=>x.Id==100);p.BloccoSocio=true;p.BloccoSocioFino=DateTime.Today.AddDays(-1);await db.SaveChangesAsync();}
        Check((await cassa.Execute(100,Find())).Socio?.SocioBloccato==false,"Blocco scaduto si disattiva automaticamente");
        Check((await cassa.Execute(100,Entry())).Scheda is not null,"Ingresso consentito dopo scadenza");
        Check(SociValidation.BloccoAttivo(true,null,DateTime.Today.AddYears(20))&&!SociValidation.BloccoAttivo(false,null,DateTime.Today),"Durata indeterminata e sblocco");
        var json=JsonSerializer.Serialize(new DatabaseHostResponse{Success=true,Soci=new(){Records=records},Cassa=new(){Socio=new(){SocioBloccato=true,BloccoSocioMotivo=reason}}},DatabaseProtocolJsonContext.Default.DatabaseHostResponse);
        var round=JsonSerializer.Deserialize(json,DatabaseProtocolJsonContext.Default.DatabaseHostResponse);
        Check(round?.Soci?.Records[0].BloccoSocio==true&&round.Cassa?.Socio?.BloccoSocioMotivo.Length==1000,"Protocollo Native AOT conserva blocco e motivazione");
    }
    private static void Pump(Func<bool> done){var end=DateTime.UtcNow.AddSeconds(20);while(!done()){Dispatcher.UIThread.RunJobs();Thread.Sleep(1);if(DateTime.UtcNow>end)throw new TimeoutException("UI blocchi soci");}}
    private static void Command(ReactiveCommand<RxVoid,RxVoid> command){var done=false;Exception? error=null;using var sub=command.Execute().SubscribePrimitives(_=>{},e=>{error=e;done=true;},()=>done=true);Pump(()=>done);if(error is not null)throw error;Dispatcher.UIThread.RunJobs();}
    public static void UI()
    {
        var fake=new FakeSoci();var vm=new SociViewModel(fake,new Screen());var view=new SociView{ViewModel=vm};var window=new Window{Width=1400,Height=1000,Content=view};window.Show();Pump(()=>vm.DataSource.Count>0&&vm.GroupEnabled);
        Command(vm.BlocchiCommand);Check(vm.Editor is BloccoSocioViewModel,"Sezione Soci apre editor Blocchi");
        var editor=vm.Editor!;editor.BloccoSocio=true;editor.BloccoIndeterminato=false;
        try{editor.Validate();throw new Exception("Scadenza mancante accettata");}catch(SociValidationException){Count++;}
        editor.BloccoFinoOffset=DateTimeOffset.Now.AddDays(7);editor.BloccoMotivo=new string('M',1000);
        Check(view.GetVisualDescendants().OfType<TextBox>().Single(x=>x.Name=="BloccoMotivoInput").MaxLength==1000&&editor.CaratteriBlocco=="1000/1000","Motivazione multilinea limitata e conteggio caratteri");
        Command(vm.SaveCommand);Check(fake.Record.BloccoSocio&&fake.Record.BloccoSocioFino==DateTime.Today.AddDays(7)&&fake.Record.BloccoSocioMotivo.Length==1000,"Editor salva blocco temporaneo");
        Check(vm.GroupBindingT?.Bloccato==true&&vm.GroupedDataSource[0].Label.Contains("BLOCCATO"),"Stato bloccato visibile nell'elenco Soci");
        Command(vm.BlocchiCommand);vm.Editor!.BloccoSocio=false;Command(vm.SaveCommand);
        Check(!fake.Record.BloccoSocio&&vm.GroupBindingT?.Bloccato==false,"Editor permette sblocco e aggiorna elenco");
        window.Close();Dispatcher.UIThread.RunJobs();
        var cvm=new CassaViewModel(new Screen(),new FakeCassa(),100);var cview=new CassaView{ViewModel=cvm};var cw=new Window{Width=1400,Height=1000,Content=cview};cw.Show();Pump(()=>cvm.IsUiReady);
        Command(cvm.EntraSocioCommand);cvm.NumeroTessera="T100";Command(cvm.FindCardCommand);
        Check(cvm.Member?.SocioBloccato==true&&!cvm.CanSelectIngresso&&!cvm.ShowIngressi&&cvm.EntryWarning.Contains("Motivo grave"),"EntraSocio mostra motivo e impedisce ingressi");
        Check(!cvm.CanActivateFidelity&&!cvm.ShowActivateFidelity&&!((System.Windows.Input.ICommand)cvm.RegisterEntryCommand).CanExecute(null),"Ingresso e Fidelity disabilitati nell'interfaccia");
        cw.Close();Dispatcher.UIThread.RunJobs();
    }
    private sealed class Screen:ReactiveObject,IScreen{public RoutingState Router{get;}=new();}
    private sealed class FakeSoci:ISociPersonRepository
    {
        public SociRecord Record=new(){Id=100,Cognome="Rossi",Nome="Mario",Natoil=19900101,CodiceSocio=100,NumeroSocio="100",CodiceTessera=100,NumeroTessera="T100",Scadenza=20991231};
        public Task<SociResult> Execute(SociRequest r,CancellationToken ct=default)
        {
            if(r.Action=="person-block"){Record.BloccoSocio=r.Record.BloccoSocio;Record.BloccoSocioFino=r.Record.BloccoSocioFino;Record.BloccoSocioMotivo=r.Record.BloccoSocioMotivo;return Task.FromResult(new SociResult{PersonId=100});}
            return Task.FromResult(new SociResult{Records=[Record]});
        }
    }
    private sealed class FakeCassa:ICassaRepository
    {
        public Task<CassaResult> Execute(CassaRequest r,CancellationToken ct=default)=>Task.FromResult<CassaResult>(r.Action switch
        {
            CassaAction.PrepareEntry=>new(){Ingressi=[new(){Id=100,Etichetta="Intero",Prezzo=10}]},
            CassaAction.FindCard=>new(){Socio=new(){PersonId=100,NumeroTessera="T100",ScadenzaTessera=20991231,SocioBloccato=true,BloccoSocioMotivo="Motivo grave"},TipiFidelity=[new(){Id=100,Nome="Fidelity"}]},
            _=>new(){PostazioneName="Cassa"}
        });
    }
}