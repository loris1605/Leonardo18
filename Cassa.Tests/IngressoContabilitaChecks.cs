using AppServices.Database;
using Leonardo18.DatabaseHost.Cassa;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Models.Context;
using Models.Contabilita;
using Models.Tables;

internal static class IngressoContabilitaChecks
{
    public static int Count;
    private static void Check(bool condition,string message)
    { if(!condition)throw new Exception(message);Count++; }
    private static async Task Reject(Func<Task> action,string message)
    {
        try { await action(); }
        catch(InvalidOperationException ex) when(ex.Message.Contains(message,StringComparison.OrdinalIgnoreCase)) { Count++; return; }
        throw new Exception("Operazione non rifiutata: "+message);
    }
    private static CassaRequest Entry(string card,string position,int tariffa=100) => new()
    { Action=CassaAction.RegisterEntry,PostazioneId=100,NumeroTessera=card,Posizione=position,TariffaId=tariffa,Note="Nota ingresso" };

    public static async Task Database()
    {
        await using var connection=new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var options=new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using(var db=new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Postazioni.Add(new(){Id=100,Nome="Cassa ingresso",TipoPostazioneId=2,TipoRientroId=-1});
            db.Operatori.Add(new(){Id=100,Nome="Operatore ingresso",PersonId=-1,Abilitato=true});
            db.Permessi.Add(new(){OperatoreId=100,PostazioneId=100});
            db.Giornate.Add(new(){Id=100,Aperta=true,DataInizio=DateTime.Today.AddDays(-1).AddHours(20),DataFine=DateTime.MaxValue});
            db.Settori.Add(new(){Id=100,Nome="Porta ingresso",TipoSettoreId=-1});
            db.Reparti.Add(new(){PostazioneId=100,SettoreId=100});
            db.Tariffe.AddRange(new Tariffa{Id=100,Nome="Nome interno tariffa",Label="Ridotto",Prezzo=12.50m},
                new Tariffa{Id=101,Nome="Omaggio",Label="Ospite",Prezzo=0});
            db.Listini.AddRange(new Listino{TariffaId=100,SettoreId=100},new Listino{TariffaId=101,SettoreId=100});
            for(var i=100;i<105;i++)
                db.People.Add(new(){Id=i,FirstName="Mario",SurName="Rossi",UniqueParam="ENTRY"+i,Natoil=19900101,
                    Soci=new(){new(){NumeroSocio="S"+i,Tessere=new(){new(){NumeroTessera="T"+i,Scadenza=20991231,Abilitato=true}}}}});
            await db.SaveChangesAsync();
        }
        var repository=new CassaDatabaseRepository(()=>new AppDbContext(options));
        var first=await repository.Execute(100,Entry("T100","A01"));
        int sourceId;
        await using(var db=new AppDbContext(options))
        {
            var source=await db.SchedeConto.SingleAsync();sourceId=source.Id;
            var movement=await db.ContabilitaMovimenti.Include(x=>x.Righe).SingleAsync();
            Check(source.Pagato&&source.VoicePrice==12.50m,"Ingresso operativo pagato");
            Check(movement.Causale=="INGRESSO_PAGATO"&&movement.Stato==StatoMovimentoContabile.Confermato,"Movimento ingresso confermato");
            Check(movement.OrigineModulo=="Cassa"&&movement.OrigineEntita=="SchedaConto"&&movement.OrigineId==source.Id.ToString()&&movement.OrigineEvento=="IngressoPagato","Origine operativa tracciata");
            Check(movement.PersonId==100&&movement.NumeroSocio=="S100"&&movement.NumeroTessera=="T100"&&movement.Nominativo=="Rossi Mario","Dati del socio conservati");
            Check(movement.OperatoreId==100&&movement.NomeOperatore=="Operatore ingresso"&&movement.PostazioneId==100&&movement.NomePostazione=="Cassa ingresso"&&movement.GiornataId==100,"Contesto operatore postazione giornata");
            Check(movement.DataOra==source.DataOra&&movement.DataCompetenza==DateTime.Today.AddDays(-1),"Data evento e competenza della giornata aperta");
            Check(movement.Note.Contains("A01")&&movement.Note.Contains("Nota ingresso")&&movement.IdOperazione!=Guid.Empty,"Note posizione e chiave evento");
            Check(movement.Righe.Count==2&&movement.Righe.Sum(x=>x.Dare)==12.50m&&movement.Righe.Sum(x=>x.Avere)==12.50m,"Scrittura quadrata di 12,50 euro");
            var cash=await db.ContabilitaPianoConti.SingleAsync(x=>x.Codice=="100");
            var revenue=await db.ContabilitaPianoConti.SingleAsync(x=>x.Codice=="400");
            Check(movement.Righe.Single(x=>x.ContoId==cash.Id).Dare==12.50m&&movement.Righe.Single(x=>x.ContoId==revenue.Id).Avere==12.50m,"Cassa in Dare e ricavo in Avere");
            Check(movement.Righe.All(x=>x.SettoreId==100&&x.NomeSettore=="Porta ingresso"&&x.TariffaId==100&&x.EtichettaTariffa=="Ridotto"&&x.Descrizione=="Ridotto"&&x.PersonId==100&&x.NumeroTessera=="T100"),"Dettagli storici settore tariffa e tessera");
            Check(!await db.ContabilitaPartite.AnyAsync()&&!await db.ContabilitaAbbinamentiPartite.AnyAsync(),"Ingresso già pagato non apre debiti");
            movement.VerificaQuadratura();Count++;
            (await db.Tariffe.SingleAsync(x=>x.Id==100)).Label="Etichetta modificata";
            (await db.People.SingleAsync(x=>x.Id==100)).SurName="Cognome modificato";
            await db.SaveChangesAsync();
        }
        await Reject(()=>repository.Execute(100,Entry("T100","A02")),"già all'interno");
        await using(var db=new AppDbContext(options))
        {
            var movement=await db.ContabilitaMovimenti.Include(x=>x.Righe).SingleAsync();
            Check(movement.Nominativo=="Rossi Mario"&&movement.Righe.All(x=>x.EtichettaTariffa=="Ridotto"),"Modifiche anagrafiche non riscrivono lo storico");
            Check(await db.Schede.CountAsync()==1&&await db.ContabilitaMovimenti.CountAsync()==1,"Richiesta ripetuta non duplica operativo o contabilità");
        }
        var payment=new CassaRequest{Action=CassaAction.SetPagamento,PostazioneId=100,Posizione="A01",SchedaId=first.Scheda!.Id,ContoId=sourceId,Pagato=false};
        await Reject(()=>repository.Execute(100,payment),"rettifica contabile");
        payment.Pagato=true;await repository.Execute(100,payment);
        await using(var db=new AppDbContext(options))
        {
            Check((await db.SchedeConto.SingleAsync()).Pagato&&await db.ContabilitaMovimenti.CountAsync()==1,"Rettifiche non disponibili non disallineano gli archivi");
            (await db.ContabilitaPianoConti.SingleAsync(x=>x.Codice=="400")).Attivo=false;await db.SaveChangesAsync();
        }
        await Reject(()=>repository.Execute(100,Entry("T101","B01")),"contabilizzare");
        await using(var db=new AppDbContext(options))
        {
            Check(await db.Schede.CountAsync()==1&&await db.SchedeConto.CountAsync()==1&&await db.ContabilitaMovimenti.CountAsync()==1,"Conto disattivato: rollback anche della scheda già salvata");
            var revenue=await db.ContabilitaPianoConti.SingleAsync(x=>x.Codice=="400");revenue.Attivo=true;revenue.Movimentabile=false;await db.SaveChangesAsync();
        }
        await Reject(()=>repository.Execute(100,Entry("T101","B01")),"contabilizzare");
        await using(var db=new AppDbContext(options))
        {
            var revenue=await db.ContabilitaPianoConti.SingleAsync(x=>x.Codice=="400");revenue.Movimentabile=true;revenue.Natura=NaturaConto.Costo;await db.SaveChangesAsync();
        }
        await Reject(()=>repository.Execute(100,Entry("T101","B01")),"contabilizzare");
        await using(var db=new AppDbContext(options))
        {
            (await db.ContabilitaPianoConti.SingleAsync(x=>x.Codice=="400")).Natura=NaturaConto.Ricavo;await db.SaveChangesAsync();
        }
        var failureOptions=new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(new FailJournal()).Options;
        var failing=new CassaDatabaseRepository(()=>new AppDbContext(failureOptions));
        await Reject(()=>failing.Execute(100,Entry("T101","B01")),"errore contabile simulato");
        await using(var db=new AppDbContext(options))
            Check(await db.Schede.CountAsync()==1&&await db.SchedeConto.CountAsync()==1&&await db.ContabilitaMovimenti.CountAsync()==1,"Errore nel salvataggio contabile: rollback completo");
        await repository.Execute(100,Entry("T101","B01"));
        await using(var db=new AppDbContext(options))
        {
            Check(await db.ContabilitaMovimenti.CountAsync()==2&&await db.ContabilitaRighe.CountAsync()==4,"Riprova dopo errore registra una sola scrittura");
            (await db.Tariffe.SingleAsync(x=>x.Id==101)).Prezzo=-1;await db.SaveChangesAsync();
        }
        await Reject(()=>repository.Execute(100,Entry("T102","C01",101)),"importo");
        await using(var db=new AppDbContext(options))
        {
            Check(!await db.Schede.AnyAsync(x=>x.PersonId==102),"Prezzo negativo non inserisce scheda");
            (await db.Tariffe.SingleAsync(x=>x.Id==101)).Prezzo=0;await db.SaveChangesAsync();
        }
        var free=await repository.Execute(100,Entry("T102","C01",101));
        await using(var db=new AppDbContext(options))
        {
            Check(await db.SchedeConto.AnyAsync(x=>x.SchedaId==free.Scheda!.Id&&x.Pagato&&x.VoicePrice==0),"Ingresso omaggio conservato nell'operativo");
            Check(await db.ContabilitaMovimenti.CountAsync()==2&&await db.ContabilitaRighe.CountAsync()==4,"Omaggio non genera movimenti monetari fittizi");
            db.SchedeConto.Add(new(){SchedaId=free.Scheda!.Id,VoiceDesc="Voce preesistente",VoicePrice=5,Pagato=true});await db.SaveChangesAsync();
            sourceId=await db.SchedeConto.Where(x=>x.VoiceDesc=="Voce preesistente").Select(x=>x.Id).SingleAsync();
        }
        var reversal=await repository.Execute(100,new(){Action=CassaAction.StornaConto,PostazioneId=100,Posizione="C01",SchedaId=free.Scheda!.Id,ContoId=sourceId});
        var entryReversal=new CassaRequest{Action=CassaAction.StornaConto,PostazioneId=100,Posizione="A01",SchedaId=first.Scheda!.Id,ContoId=payment.ContoId};
        await Reject(()=>failing.Execute(100,entryReversal),"errore contabile simulato");
        await using(var db=new AppDbContext(options))Check(await db.SchedeConto.CountAsync(x=>x.SchedaId==first.Scheda.Id)==1,"Errore storno contabile annulla anche la riga negativa");
        var reversedEntry=await repository.Execute(100,entryReversal);
        Check(reversedEntry.Conto.Sum(x=>x.VoicePrice)==0&&reversedEntry.Conto.Any(x=>x.VoicePrice==-12.50m&&x.Pagato),"Storno ingresso pagato azzera il conto operativo");
        await using(var db=new AppDbContext(options))
        {
            var journal=await db.ContabilitaMovimenti.Include(x=>x.Righe).SingleAsync(x=>x.OrigineEvento=="StornoIngresso");
            var original=await db.ContabilitaMovimenti.Include(x=>x.Righe).SingleAsync(x=>x.Id==journal.StornoDiId);
            Check(journal.Causale=="STORNO_INGRESSO"&&journal.GiornataId==100,"Rettifica nella giornata aperta collegata all'originale");
            Check(journal.Righe.All(row=>original.Righe.Any(x=>x.ContoId==row.ContoId&&x.Dare==row.Avere&&x.Avere==row.Dare&&x.EtichettaTariffa==row.EtichettaTariffa)),"Storno inverte Dare e Avere e conserva la tariffa storica");
            journal.VerificaQuadratura();Count++;
            Check(await db.Schede.AnyAsync(x=>x.Id==first.Scheda.Id),"Storno importo conserva la presenza del socio");
        }
        await Reject(()=>repository.Execute(100,new(){Action=CassaAction.SetPagamento,PostazioneId=100,Posizione="A01",SchedaId=first.Scheda.Id,ContoId=reversedEntry.Conto.Single(x=>x.VoicePrice<0).Id,Pagato=false}),"non è modificabile");        await Reject(()=>repository.Execute(100,entryReversal),"già annullato");
        await using(var db=new AppDbContext(options))Check(await db.SchedeConto.CountAsync(x=>x.SchedaId==first.Scheda.Id)==2,"Richiesta ripetuta non duplica lo storno");
        int priorJournals;
        await using(var db=new AppDbContext(options))
        {
            (await db.Schede.SingleAsync(x=>x.Id==first.Scheda.Id)).IngressoAnnullato=false;
            (await db.SchedeConto.SingleAsync(x=>x.SchedaId==first.Scheda.Id&&x.VoicePrice<0)).VoiceDesc="STORNO - Ridotto";
            await db.SaveChangesAsync();priorJournals=await db.ContabilitaMovimenti.CountAsync();
        }
        var completed=await repository.Execute(100,entryReversal);
        Check(completed.Scheda?.IngressoAnnullato==true&&completed.Conto.Any(x=>x.VoiceDesc.Contains("NON ENTRA")),"Vecchio storno solo monetario completabile come NON ENTRA");
        await using(var db=new AppDbContext(options))Check(await db.ContabilitaMovimenti.CountAsync()==priorJournals&&await db.SchedeConto.CountAsync(x=>x.SchedaId==first.Scheda.Id)==2,"Completare vecchio storno non duplica il rimborso");
        await EntryPending(repository,options);
        Check(reversal.Conto.Any(x=>x.VoiceDesc=="STORNO - Voce preesistente"&&x.VoicePrice==-5&&x.Pagato),"Storno operativo delle voci non contabilizzate rimane disponibile");
    }
    private static async Task EntryPending(CassaDatabaseRepository repository,DbContextOptions<AppDbContext> options)
    {
        await using(var db=new AppDbContext(options))
        {db.TipiAbbonamento.Add(new(){Id=200,Nome="Abbonamento sospeso",Attivo=true,NumeroIngressi=10,DurataGiorni=30,Prezzo=50});await db.SaveChangesAsync();}
        async Task<long> Purchase()=> (await repository.Execute(100,new(){Action=CassaAction.BuyAbbonamento,PostazioneId=100,AcquistoId=Guid.NewGuid(),
            TipoAbbonamentoId=200,NumeroTessera="T103",PersonId1=103,PrezzoConfermato=50,IngressiConfermati=10,DurataConfermata=30})).AcquistoAbbonamento!.SospesoId!.Value;
        var one=await Purchase();var two=await Purchase();
        await Reject(()=>repository.Execute(100,Entry("T103","D01",101)),"sospesi");
        await repository.Execute(100,new(){Action=CassaAction.SaldaSospeso,PostazioneId=100,SospesoId=one});
        await Reject(()=>repository.Execute(100,Entry("T103","D01",101)),"sospesi");
        await repository.Execute(100,new(){Action=CassaAction.SaldaSospeso,PostazioneId=100,SospesoId=two});
        int journals;
        await using(var db=new AppDbContext(options))journals=await db.ContabilitaMovimenti.CountAsync();
        var result=await repository.Execute(100,Entry("T103","D01",101));
        await using(var db=new AppDbContext(options))
        {
            var rows=await db.SchedeConto.Where(x=>x.SchedaId==result.Scheda!.Id).ToArrayAsync();
            Check(rows.Length==1&&rows[0].VoiceDesc=="Ospite","Dopo il saldo compare soltanto la voce di ingresso in SchedaConto");
            Check(await db.Sospesi.Where(x=>x.Id==one||x.Id==two).AllAsync(x=>x.SchedaContoId==null),"Saldo sospesi separato da SchedaConto");
            Check(await db.ContabilitaMovimenti.CountAsync()==journals,"Nessuna duplicazione contabile al successivo ingresso");
        }
    }

    private sealed class FailJournal:SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        {
            if(data.Context!.ChangeTracker.Entries<MovimentoContabile>().Any(x=>x.State==EntityState.Added))
                throw new InvalidOperationException("Errore contabile simulato");
            return ValueTask.FromResult(result);
        }
    }
}
