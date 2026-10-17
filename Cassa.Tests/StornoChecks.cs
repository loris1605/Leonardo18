using AppServices.Database;
using Leonardo18.DatabaseHost.Cassa;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Models.Context;
using Models.Tables;

internal static class StornoChecks
{
    public static async Task<int> Run(DbContextOptions<AppDbContext> options, CassaDatabaseRepository repository)
    {
        var checks=0;
        void Check(bool ok,string message) { if (!ok) throw new Exception(message); checks++; }
        async Task Reject(CassaRequest request,string message)
        {
            try { await repository.Execute(100,request); }
            catch (InvalidOperationException ex) when (ex.Message.Contains(message,StringComparison.OrdinalIgnoreCase)) { checks++; return; }
            throw new Exception("Storno non rifiutato: " + message);
        }
        int sourceId,zeroId,sheetId,paidId;
        var description=new string('A',30);
        await using(var db=new AppDbContext(options))
        {
            var sheet=await db.Schede.SingleAsync(); sheetId=sheet.Id;
            paidId=await db.SchedeConto.Select(x=>x.Id).SingleAsync();
            var source=new SchedaConto { SchedaId=sheetId,VoiceDesc=description,VoicePrice=18.75m,
                DescSettore="Bar",DescPostazione="Origine",Note="Nota originale",DataOra=DateTime.Now };
            var zero=new SchedaConto { SchedaId=sheetId,VoiceDesc="Omaggio",VoicePrice=0m,DataOra=DateTime.Now };
            db.SchedeConto.AddRange(source,zero); await db.SaveChangesAsync(); sourceId=source.Id; zeroId=zero.Id;
        }
        var request=new CassaRequest { Action=CassaAction.StornaConto,PostazioneId=100,Posizione="A01",SchedaId=sheetId,ContoId=sourceId };
        var result=await repository.Execute(100,request);
        var reversal=result.Conto.Single(x=>x.VoicePrice<0);
        Check(reversal.VoicePrice==-18.75m && reversal.VoiceDesc=="STORNO - " + description,
            "Storno conserva tutta la descrizione di 30 caratteri e nega il prezzo.");
        Check(!reversal.Pagato && reversal.DescSettore=="Bar" && reversal.DescPostazione=="Origine" && reversal.Note=="Nota originale",
            "Storno conserva stato di pagamento e dati della voce originale.");
        Check(result.Conto.Single(x=>x.Id==sourceId).VoicePrice==18.75m && result.Conto.Length==4,
            "Storno aggiunge una riga senza modificare quella originale.");
        Check(result.Conto.Where(x=>!x.Pagato).Sum(x=>x.VoicePrice)==0m && result.VociSpesa.Length==0,
            "Storno non pagato annulla il residuo senza ricaricare i listini.");
        request.ContoId=reversal.Id; result=await repository.Execute(100,request);
        Check(result.Conto.Length==4,"Importo negativo non genera operazioni.");
        request.ContoId=zeroId; result=await repository.Execute(100,request);
        Check(result.Conto.Length==4,"Importo zero non genera operazioni.");
        request.ContoId=paidId; await Reject(request,"contabilità");
        request.ContoId=int.MaxValue; await Reject(request,"voce di spesa"); request.ContoId=sourceId;
        request.SchedaId++; await Reject(request,"cambiata"); request.SchedaId=sheetId;
        request.PostazioneId=101; await Reject(request,"permesso"); request.PostazioneId=100;
        await using(var db=new AppDbContext(options))
        {
            db.Schede.Add(new() { PersonId=101,Posizione="STORNO_ALTRO",SchedeConto=[new() { VoiceDesc="Altra scheda",VoicePrice=25m }] });
            await db.SaveChangesAsync(); request.ContoId=await db.SchedeConto.Where(x=>x.VoiceDesc=="Altra scheda").Select(x=>x.Id).SingleAsync();
        }
        await Reject(request,"voce di spesa");
        await using(var db=new AppDbContext(options))
        {
            db.SchedeConto.Remove(await db.SchedeConto.SingleAsync(x=>x.Id==request.ContoId));
            db.Schede.Remove(await db.Schede.SingleAsync(x=>x.Posizione=="STORNO_ALTRO"));
            (await db.Schede.SingleAsync(x=>x.Id==sheetId)).Blocco=true; await db.SaveChangesAsync();
        }
        request.ContoId=sourceId; await Reject(request,"bloccata");
        await using(var db=new AppDbContext(options))
        {
            var sheet=await db.Schede.SingleAsync(); sheet.Blocco=false; sheet.CheckoutTime=DateTime.Now; await db.SaveChangesAsync();
        }
        await Reject(request,"chiusa");
        await using(var db=new AppDbContext(options))
        {
            (await db.Schede.SingleAsync()).CheckoutTime=DateTime.MaxValue;
            (await db.Giornate.SingleAsync()).Aperta=false; await db.SaveChangesAsync();
        }
        await Reject(request,"giornata");
        await using(var db=new AppDbContext(options))
        {
            Check(await db.SchedeConto.CountAsync()==4,"Storni rifiutati non aggiungono righe.");
            (await db.Giornate.SingleAsync()).Aperta=true;
            db.SchedeConto.RemoveRange(db.SchedeConto.Where(x=>x.Id!=paidId)); await db.SaveChangesAsync();
        }
        // Generate SQL locally: no connection is opened and the configured database is never used.
        using(var sql=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=.;Database=StornoSchemaTest;Integrated Security=true;TrustServerCertificate=true").Options))
        {
            var script=sql.GetService<IMigrator>().GenerateScript();
            Check(script.Contains("CREATE TABLE [SchedeConto]") && script.Contains("[VoiceDesc] nvarchar(50)")
                && !script.Contains("DROP TABLE"),"Start crea la descrizione completa per gli storni.");
            Check(!sql.Database.HasPendingModelChanges(),"Snapshot migrazioni allineato al modello SQL Server.");
        }
        return checks;
    }
}
