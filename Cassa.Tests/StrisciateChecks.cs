using System.Net;
using System.Net.Sockets;
using AppServices.Database;
using Cassa.Core.Repository;
using Leonardo18.DatabaseHost.Cassa;
using Leonardo18.DatabaseHost.Strisciate;
using Microsoft.EntityFrameworkCore;
using Models.Context;

internal static class StrisciateChecks
{
    public static async Task<int> Run(DbContextOptions<AppDbContext> options, CassaDatabaseRepository cassa)
    {
        int checks=0;
        void Check(bool ok,string description) { if (!ok) throw new Exception(description); checks++; }
        await using var receiver=new StrisciateReceiver(0);
        var service=new CassaSessionService(receiver, _ => new AppDbContext(options));
        var session=Guid.NewGuid().ToString("N");
        DatabaseHostRequest Request(CassaAction action,string? id=null,int op=100) => new() {
            OperatorId=op,Cassa=new() { Action=action,PostazioneId=100,StrisciateSessionId=id ?? session }
        };
        try { await service.ExecuteAsync(Request(CassaAction.Load,op:999)); throw new Exception("Ricevitore avviato senza permesso."); }
        catch (InvalidOperationException) { Check(receiver.Endpoint is null,"Nessun ascolto se la Cassa non è autorizzata."); }
        var result=await service.ExecuteAsync(Request(CassaAction.Load));
        var endpoint=result.StrisciateEndpoint;
        Check(new Uri(endpoint).Host == "127.0.0.1","Ricevitore limitato a loopback.");
        Check((await service.ExecuteAsync(Request(CassaAction.Load))).StrisciateEndpoint == endpoint,"Avvio ripetuto della stessa sessione non duplica il server.");
        using var client=new HttpClient { Timeout=TimeSpan.FromSeconds(5) };
        var fields=new Dictionary<string,string> {
            ["codice_socio"]="000054",["numero_tessera"]="000077",["nome"]="ANNA MARIA",
            ["cognome"]="D'ANGELO'); DROP TABLE People;--",["born_date"]="19950302",["data_scadenza"]="20991231",
            ["stato"]="attivo",["tipo_tessera_id"]="1",["circolo_id"]="19",["data_rilascio"]="20150812"
        };
        string Url(Dictionary<string,string> values) => endpoint + "/update?" + string.Join("&",values.Select(x=>Uri.EscapeDataString(x.Key)+"="+Uri.EscapeDataString(x.Value))).Replace("%20","+");
        using (var response=await client.GetAsync(Url(fields)))
        { Check(response.StatusCode == HttpStatusCode.OK && await response.Content.ReadAsStringAsync() == "OK","/update risponde OK dopo il salvataggio."); }
        await using (var db=new AppDbContext(options))
        {
            var card=await db.Tessere.Include(t=>t.Socio).ThenInclude(s=>s!.Person).SingleAsync(t=>t.NumeroTessera == "000077");
            Check(card.Socio!.NumeroSocio == "000054" && card.NumeroTessera == "000077","Zeri iniziali preservati nei numeri.");
            Check(card.Socio.Person!.FirstName == "ANNA MARIA" && card.Socio.Person.SurName == fields["cognome"].ToUpperInvariant(),"Decodifica + e apostrofi preservati come dati.");
            Check(card.Socio.Person.Natoil == 19950302 && card.Scadenza == 20991231 && await db.Strisciate.CountAsync() == 0,"Smistamento immediato di anagrafica e scadenza, senza record rimasti in coda.");
            Check(await db.People.AnyAsync(),"Testo simile a SQL non viene eseguito.");
        }
        using (var response=await client.GetAsync(endpoint+"/altro")) Check(response.StatusCode == HttpStatusCode.NotFound,"Percorso sconosciuto: 404.");
        using (var response=await client.PostAsync(Url(fields),new StringContent(""))) Check(response.StatusCode == HttpStatusCode.MethodNotAllowed,"Metodo non supportato: 405.");
        var invalid=new Dictionary<string,string>(fields); invalid.Remove("numero_tessera");
        using (var response=await client.GetAsync(Url(invalid))) Check(response.StatusCode == HttpStatusCode.BadRequest,"Tessera mancante: 400.");
        invalid=new(fields) { ["born_date"]="20260230" };
        using (var response=await client.GetAsync(Url(invalid))) Check(response.StatusCode == HttpStatusCode.BadRequest,"Data impossibile: 400.");
        invalid=new(fields) { ["data_scadenza"]="20261301" };
        using (var response=await client.GetAsync(Url(invalid))) Check(response.StatusCode == HttpStatusCode.BadRequest,"Scadenza impossibile: 400.");
        invalid=new(fields) { ["cognome"]=new string('X',51) };
        using (var response=await client.GetAsync(Url(invalid))) Check(response.StatusCode == HttpStatusCode.BadRequest,"Campo troppo lungo: 400.");
        using (var response=await client.GetAsync(Url(fields)+"&numero_tessera=ALTRA")) Check(response.StatusCode == HttpStatusCode.BadRequest,"Parametri ambigui: 400.");
        await using (var db=new AppDbContext(options)) Check(await db.Strisciate.CountAsync() == 0 && await db.Tessere.CountAsync(t=>t.NumeroTessera == "000077") == 1,"Richieste malformate non scrivono righe o tessere.");
        var concurrent=await Task.WhenAll(Enumerable.Range(0,6).Select(_=>client.GetAsync(Url(fields))));
        try { Check(concurrent.All(r=>r.IsSuccessStatusCode),"Richieste simultanee ricevute senza conflitti tra DbContext."); }
        finally { foreach(var response in concurrent) response.Dispose(); }
        await using (var db=new AppDbContext(options)) Check(await db.Strisciate.CountAsync() == 0 && await db.Tessere.CountAsync(t=>t.NumeroTessera == "000077") == 1,"Richieste ripetute non duplicano i dati.");
        checks += await SmistamentoChecks.Run(options, cassa, client, endpoint);

        await using (var db=new AppDbContext(options)) { var op=await db.Operatori.SingleAsync(x=>x.Id==100); op.Abilitato=false; await db.SaveChangesAsync(); }
        using (var response=await client.GetAsync(Url(fields))) Check(response.StatusCode == HttpStatusCode.Forbidden,"Revoca operatore impedisce nuovi inserimenti.");
        await using (var db=new AppDbContext(options)) { var op=await db.Operatori.SingleAsync(x=>x.Id==100); op.Abilitato=true; await db.SaveChangesAsync(); }
        await using (var db=new AppDbContext(options)) { var day=await db.Giornate.SingleAsync(x=>x.Aperta); day.Aperta=false; await db.SaveChangesAsync(); }
        using (var response=await client.GetAsync(Url(fields))) Check(response.StatusCode == HttpStatusCode.Forbidden,"Giornata chiusa impedisce nuovi inserimenti.");
        await using (var db=new AppDbContext(options)) { var day=await db.Giornate.SingleAsync(); day.Aperta=true; await db.SaveChangesAsync(); }

        var newSession=Guid.NewGuid().ToString("N");
        result=await service.ExecuteAsync(Request(CassaAction.Load,newSession));
        await service.ExecuteAsync(Request(CassaAction.StopStrisciate,session));
        Check(receiver.Endpoint == result.StrisciateEndpoint,"Arresto di una vecchia sessione non interrompe la nuova.");
        var lastPort=new Uri(receiver.Endpoint!).Port;
        await service.ExecuteAsync(Request(CassaAction.StopStrisciate,newSession));
        Check(receiver.Endpoint is null,"Arresto al ritorno al Menu.");
        var listener=new TcpListener(IPAddress.Loopback,lastPort);
        try { listener.Start(); Check(true,"Porta rilasciata alla chiusura."); } finally { listener.Stop(); }
        await cassa.Execute(100,new() { Action=CassaAction.PrepareEntry,PostazioneId=100 });
        await using (var db=new AppDbContext(options))
        {
            Check(await db.Strisciate.CountAsync() == 0,"Lo smistamento HTTP è già completo prima di Entra socio.");
            Check(await db.Tessere.CountAsync(x=>x.NumeroTessera == "000077") == 1,"Strisciate ripetute generano una sola tessera nell'anagrafica.");
        }

        var blocking=new BlockingSink();
        await using (var cancellationReceiver=new StrisciateReceiver(0))
        {
            var address=await cancellationReceiver.StartAsync("pending",blocking);
            var pendingRequest=client.GetAsync(address+new Uri(Url(fields)).PathAndQuery);
            await blocking.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await cancellationReceiver.StopAsync("pending");
            Check(blocking.Cancelled,"La chiusura cancella le scritture ancora in corso.");
            try { using var response=await pendingRequest; Check(!response.IsSuccessStatusCode,"Una scrittura annullata non restituisce OK."); }
            catch (HttpRequestException) { Check(true,"Richiesta interrotta durante la chiusura."); }
            catch (TaskCanceledException) { Check(true,"Richiesta annullata durante la chiusura."); }
        }
        var occupied=new TcpListener(IPAddress.Loopback,0); occupied.Start();
        try
        {
            await using var conflicting=new StrisciateReceiver(((IPEndPoint)occupied.LocalEndpoint).Port);
            try { await conflicting.StartAsync("conflict",blocking); throw new Exception("Avvio su porta occupata accettato."); }
            catch (InvalidOperationException ex) { Check(ex.Message.Contains("porta") && conflicting.Endpoint is null,"Porta occupata segnalata senza fermare il servizio esistente."); }
        }
        finally { occupied.Stop(); }
        await using (var failingReceiver=new StrisciateReceiver(0))
        {
            var address=await failingReceiver.StartAsync("failure",new FailingSink());
            using var response=await client.GetAsync(address+new Uri(Url(fields)).PathAndQuery);
            Check(response.StatusCode == HttpStatusCode.InternalServerError && !(await response.Content.ReadAsStringAsync()).Contains("SEGRETO"),"Errore database: 500 senza dettagli riservati.");
        }
        // The desktop attaches a unique receiver lease to every request and its matching stop.
        var capture=new CaptureClient();
        var repository1=new CassaRepository(capture,100); var repository2=new CassaRepository(capture,100);
        await repository1.Execute(new() { Action=CassaAction.Load,PostazioneId=100 });
        await repository1.Execute(new() { Action=CassaAction.StopStrisciate,PostazioneId=100 });
        await repository2.Execute(new() { Action=CassaAction.Load,PostazioneId=100 });
        Check(capture.Sessions[0] == capture.Sessions[1] && capture.Sessions[0] != capture.Sessions[2],"Sessioni desktop distinte e arresto riferito alla sessione corretta.");
        return checks;
    }
    private sealed class BlockingSink : IStrisciateSink
    {
        public TaskCompletionSource Entered { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Cancelled { get; private set; }
        public async Task InsertAsync(StrisciataInput input,CancellationToken ct=default)
        { Entered.TrySetResult(); try { await Task.Delay(Timeout.InfiniteTimeSpan,ct); } catch (OperationCanceledException) { Cancelled=true; throw; } }
    }
    private sealed class FailingSink : IStrisciateSink
    { public Task InsertAsync(StrisciataInput input,CancellationToken ct=default) => Task.FromException(new Exception("SEGRETO")); }
    private sealed class CaptureClient : IDatabaseHostClient
    {
        public List<string> Sessions { get; }=[];
        public Task<DatabaseHostResponse> SendAsync(DatabaseHostRequest request,CancellationToken ct=default)
        { Sessions.Add(request.Cassa!.StrisciateSessionId); return Task.FromResult(new DatabaseHostResponse { Success=true,Cassa=new() }); }
    }
}
