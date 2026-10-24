using System.Text.Json;
using AppServices.Database;
using Avalonia.Controls;
using Avalonia.Threading;
using Cassa.Core.Repository;
using Cassa.ViewModels;
using Cassa.Views;
using Leonardo18.DatabaseHost.Cassa;
using Leonardo18.DatabaseHost.Strisciate;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Models.Context;
using ReactiveUI;
using ReactiveUI.Primitives;

internal static class StrisciataIngressoChecks
{
    public static int Count;
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);Count++;}
    public static async Task Database()
    {
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        var options=new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using(var db=new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Postazioni.Add(new(){Id=100,Nome="Cassa",TipoPostazioneId=2,TipoRientroId=-1});
            db.Operatori.Add(new(){Id=100,Nome="Operatore",PersonId=-1,Abilitato=true});
            db.Permessi.Add(new(){OperatoreId=100,PostazioneId=100});
            db.Giornate.Add(new(){Aperta=true,DataInizio=DateTime.Today,DataFine=DateTime.MaxValue});
            await db.SaveChangesAsync();
        }
        await using var receiver=new StrisciateReceiver(0);
        var service=new CassaSessionService(receiver,_=>new AppDbContext(options));
        var session=Guid.NewGuid().ToString();
        DatabaseHostRequest Request(CassaAction action)=>new(){OperatorId=100,Cassa=new(){Action=action,PostazioneId=100,StrisciateSessionId=session}};
        var loaded=await service.ExecuteAsync(Request(CassaAction.Load));
        using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(10)};
        var url=loaded.StrisciateEndpoint+"/update?codice_socio=S1&numero_tessera=000077&nome=Mario&cognome=Rossi&born_date=19900101&data_scadenza=20991231";
        using(var reply=await client.GetAsync(url))Check(reply.IsSuccessStatusCode,"Importazione HTTP riuscita");
        await using(var db=new AppDbContext(options))Check(await db.Tessere.AnyAsync(x=>x.NumeroTessera=="000077"),"Tessera salvata prima dell'autocompilazione");
        Check(await receiver.TakeImportedCardAsync(Guid.NewGuid().ToString())=="","Altra sessione non legge né consuma la tessera");
        using(var reply=await client.GetAsync(loaded.StrisciateEndpoint+"/update?numero_tessera=SCARTATA"))Check(!reply.IsSuccessStatusCode,"Importazione non valida rifiutata");
        await using(var db=new AppDbContext(options)){(await db.Operatori.SingleAsync(x=>x.Id==100)).Abilitato=false;await db.SaveChangesAsync();}
        try{await service.ExecuteAsync(Request(CassaAction.PrepareEntry));throw new Exception("Preparazione non autorizzata accettata");}
        catch(InvalidOperationException){Count++;}
        await using(var db=new AppDbContext(options)){(await db.Operatori.SingleAsync(x=>x.Id==100)).Abilitato=true;await db.SaveChangesAsync();}
        await service.ExecuteAsync(Request(CassaAction.ListInside));
        var first=await service.ExecuteAsync(Request(CassaAction.PrepareEntry));
        Check(first.TesseraImportata=="000077","Prima apertura usa tessera importata con zeri iniziali; errori e altre viste non la consumano");
        var json=JsonSerializer.Serialize(new DatabaseHostResponse{Success=true,Cassa=first},DatabaseProtocolJsonContext.Default.DatabaseHostResponse);
        Check(JsonSerializer.Deserialize(json,DatabaseProtocolJsonContext.Default.DatabaseHostResponse)?.Cassa?.TesseraImportata=="000077","Protocollo AOT trasferisce tessera importata");
        Check((await service.ExecuteAsync(Request(CassaAction.PrepareEntry))).TesseraImportata=="","Seconda apertura senza tessera");
        using(var reply=await client.GetAsync(url))Check(reply.IsSuccessStatusCode,"Nuova ricezione riuscita");
        Check((await service.ExecuteAsync(Request(CassaAction.PrepareEntry))).TesseraImportata=="000077","Nuova ricezione riabilita autocompilazione una volta");
        using(var reply=await client.GetAsync(url))Check(reply.IsSuccessStatusCode,"Ricezione prima di cambio sessione");
        await service.ExecuteAsync(Request(CassaAction.StopStrisciate));
        session=Guid.NewGuid().ToString();await service.ExecuteAsync(Request(CassaAction.Load));
        Check((await service.ExecuteAsync(Request(CassaAction.PrepareEntry))).TesseraImportata=="","Nuova sessione non eredita tessere della precedente");
    }
    private static void Pump(Func<bool> done){var end=DateTime.UtcNow.AddSeconds(20);while(!done()){Dispatcher.UIThread.RunJobs();Thread.Sleep(1);if(DateTime.UtcNow>end)throw new TimeoutException("UI tessera importata");}}
    private static void Command(ReactiveCommand<RxVoid,RxVoid> command){var done=false;Exception? error=null;using var sub=command.Execute().SubscribePrimitives(_=>{},e=>{error=e;done=true;},()=>done=true);Pump(()=>done);if(error is not null)throw error;Dispatcher.UIThread.RunJobs();}
    public static void UI()
    {
        var fake=new Fake();var vm=new CassaViewModel(new Screen(),fake,100);var view=new CassaView{ViewModel=vm};
        var window=new Window{Width=1300,Height=950,Content=view};window.Show();Pump(()=>vm.IsUiReady);
        Command(vm.EntraSocioCommand);
        Check(vm.IsEntry&&vm.NumeroTessera=="000077"&&vm.Member?.NumeroTessera=="000077"&&fake.Identified==1,"Prima apertura compila e identifica automaticamente");
        Check(view.FindControl<ControlStyles.UiTextBox>("IngressoPosizioneBox")!.IsFocused,"Identificazione riuscita porta alla posizione");
        Command(vm.BackCommand);Command(vm.EntraSocioCommand);
        Check(vm.NumeroTessera==""&&vm.Member is null&&fake.Identified==1,"Seconda apertura vuota senza identificazione automatica");
        Check(view.FindControl<ControlStyles.UiTextBox>("TesseraBox")!.IsFocused,"Seconda apertura attende inserimento tessera");
        vm.NumeroTessera="MANUALE";Command(vm.FindCardCommand);
        Check(vm.Member?.NumeroTessera=="MANUALE"&&fake.Identified==2,"Identificazione manuale resta disponibile");
        Command(vm.BackCommand);fake.NextCard="000088";Command(vm.EntraSocioCommand);
        Check(vm.NumeroTessera=="000088"&&vm.Member?.NumeroTessera=="000088"&&fake.Identified==3,"Nuova importazione proposta alla successiva apertura");
        window.Close();Dispatcher.UIThread.RunJobs();
    }
    private sealed class Screen:ReactiveObject,IScreen{public RoutingState Router{get;}=new();}
    private sealed class Fake:ICassaRepository
    {
        public string NextCard="000077";public int Identified;
        public Task<CassaResult> Execute(CassaRequest r,CancellationToken ct=default)
        {
            if(r.Action==CassaAction.PrepareEntry){var card=NextCard;NextCard="";return Task.FromResult(new CassaResult{TesseraImportata=card,Ingressi=[new(){Id=100,Etichetta="Intero",Prezzo=10}]});}
            if(r.Action==CassaAction.FindCard){Identified++;return Task.FromResult(new CassaResult{Socio=new(){PersonId=100,NumeroTessera=r.NumeroTessera,ScadenzaTessera=20991231}});}
            return Task.FromResult(new CassaResult{PostazioneName="Cassa"});
        }
    }
}