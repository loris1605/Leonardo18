using System.Data;
using System.Text.Json;
using AppServices.Database;
using Microsoft.EntityFrameworkCore;
using Models.Context;
using Models.Contabilita;
using Models.Sospesi;

namespace Leonardo18.DatabaseHost.Report;

public sealed class ReportDatabaseRepository(Func<AppDbContext> contextFactory)
{
    public async Task<ReportResult> Execute(int operatorId,ReportRequest request,CancellationToken ct=default)
    {
        await using var db=contextFactory();
        await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        if(!await db.Operatori.AnyAsync(x=>x.Id==operatorId&&x.Abilitato,ct)
            ||!await db.Permessi.AnyAsync(x=>x.OperatoreId==operatorId&&(x.Postazione!.TipoPostazioneId==1||x.Postazione.TipoPostazioneId==2||x.Postazione.TipoPostazioneId==6),ct))
            throw new InvalidOperationException("Non hai il permesso di consultare i report.");
        var now=DateTime.Now;
        var result=new ReportResult{AggiornatoAl=now,Giornate=await db.Giornate.AsNoTracking().OrderByDescending(x=>x.Aperta).ThenByDescending(x=>x.DataInizio).ThenByDescending(x=>x.Id)
            .Select(x=>new ReportGiornata{Id=x.Id,Inizio=x.DataInizio,Fine=x.DataFine,Aperta=x.Aperta}).ToArrayAsync(ct)};
        var day=request.GiornataId==0?result.Giornate.FirstOrDefault():result.Giornate.SingleOrDefault(x=>x.Id==request.GiornataId)
            ??throw new InvalidOperationException("Giornata non trovata. Aggiorna il report.");
        if(day is null){await transaction.CommitAsync(ct);return result;}
        result.GiornataId=day.Id;
        var opening=await db.Giornate.AsNoTracking().Where(x=>x.Id==day.Id).Select(x=>new{x.CassaIniziale,x.ConteggioCassaIniziale}).SingleAsync(ct);
        result.CassaIniziale=opening.CassaIniziale;
        result.TagliCassaIniziale=string.IsNullOrEmpty(opening.ConteggioCassaIniziale)?[]:
            JsonSerializer.Deserialize(opening.ConteggioCassaIniziale,DatabaseProtocolJsonContext.Default.TaglioEuroArray)??[];
        var start=day.Inizio;var end=day.Aperta?now.AddTicks(1):day.Fine;
        if(end<=start)throw new InvalidOperationException("Intervallo della giornata non valido.");
        result.SituazioneAl=day.Aperta?now:day.Fine;
        result.Ingressi=await db.Schede.CountAsync(x=>!x.IngressoAnnullato&&x.CheckinTime>=start&&x.CheckinTime<end,ct);
        result.Uscite=await db.Schede.CountAsync(x=>!x.IngressoAnnullato&&x.CheckoutTime>=start&&x.CheckoutTime<end,ct);
        result.Presenti=await db.Schede.CountAsync(x=>!x.IngressoAnnullato&&x.CheckinTime<end&&x.CheckoutTime>=end,ct);
        result.Ingressi+=await db.SchedeUscite.CountAsync(x=>!x.IngressoAnnullato&&x.CheckinTime>=start&&x.CheckinTime<end,ct);
        result.Uscite+=await db.SchedeUscite.CountAsync(x=>!x.IngressoAnnullato&&x.CheckoutTime>=start&&x.CheckoutTime<end,ct);
        result.Presenti+=await db.SchedeUscite.CountAsync(x=>!x.IngressoAnnullato&&x.CheckinTime<end&&x.CheckoutTime>=end,ct);
        var rows=await db.SchedeConto.AsNoTracking().Where(x=>x.DataOra>=start&&x.DataOra<end)
            .Select(x=>new{Settore=x.DescSettore,Postazione=x.DescPostazione,Descrizione=x.VoiceDesc,Importo=x.VoicePrice,x.Pagato}).ToArrayAsync(ct);
        var historical=await db.SchedeUsciteConto.AsNoTracking().Where(x=>x.DataOra>=start&&x.DataOra<end)
            .Select(x=>new{Settore=x.DescSettore,Postazione=x.DescPostazione,Descrizione=x.VoiceDesc,Importo=x.VoicePrice,x.Pagato}).ToArrayAsync(ct);
        rows=rows.Concat(historical).ToArray();
        static string Name(string? value)=>string.IsNullOrWhiteSpace(value)?"Non indicato":value.Trim();
        var sectors=await db.Settori.AsNoTracking().Where(x=>x.Id>0).Select(x=>x.Nome).ToArrayAsync(ct);
        ReportRiepilogo[] Summarize(IEnumerable<string> names,Func<string,IEnumerable<int>> indices)=>names.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x,StringComparer.CurrentCultureIgnoreCase).Select(name=>{
            var values=indices(name).Select(i=>rows[i]).ToArray();
            return new ReportRiepilogo{Nome=name,Operazioni=values.Length,Lordo=values.Where(x=>x.Importo>0).Sum(x=>x.Importo),Storni=-values.Where(x=>x.Importo<0).Sum(x=>x.Importo),
                Pagato=values.Where(x=>x.Pagato).Sum(x=>x.Importo),DaPagare=values.Where(x=>!x.Pagato).Sum(x=>x.Importo)};
        }).ToArray();
        result.Settori=Summarize(sectors.Select(Name).Concat(rows.Select(x=>Name(x.Settore))),name=>Enumerable.Range(0,rows.Length).Where(i=>string.Equals(Name(rows[i].Settore),name,StringComparison.OrdinalIgnoreCase)));
        result.Postazioni=Summarize(rows.Select(x=>Name(x.Postazione)),name=>Enumerable.Range(0,rows.Length).Where(i=>string.Equals(Name(rows[i].Postazione),name,StringComparison.OrdinalIgnoreCase)));
        result.Voci=rows.GroupBy(x=>(Settore:Name(x.Settore),Descrizione:Name(x.Descrizione))).OrderBy(x=>x.Key.Settore).ThenBy(x=>x.Key.Descrizione)
            .Select(g=>new ReportVoce{Settore=g.Key.Settore,Descrizione=g.Key.Descrizione,Operazioni=g.Count(),Totale=g.Sum(x=>x.Importo),Pagato=g.Where(x=>x.Pagato).Sum(x=>x.Importo),DaPagare=g.Where(x=>!x.Pagato).Sum(x=>x.Importo)}).ToArray();
        var purchases=await db.Abbonamenti.AsNoTracking().Where(x=>x.DataAcquisto>=start&&x.DataAcquisto<end).Select(x=>x.PrezzoAcquisto??0).ToArrayAsync(ct);
        result.AbbonamentiVenduti=purchases.Length;result.ValoreAbbonamenti=purchases.Sum();
        var entries=await db.ContabilitaRighe.AsNoTracking().Where(x=>x.Movimento.GiornataId==day.Id&&x.Movimento.Stato==StatoMovimentoContabile.Confermato)
            .Select(x=>new{x.MovimentoId,x.Movimento.OrigineEvento,x.Dare,x.Avere,x.Conto.Natura,x.Conto.Codice,x.NomeSettore,x.AbbonamentoId}).ToArrayAsync(ct);
        result.Ricavi=entries.Where(x=>x.Natura==NaturaConto.Ricavo).Sum(x=>x.Avere-x.Dare);
        result.Costi=entries.Where(x=>x.Natura==NaturaConto.Costo).Sum(x=>x.Dare-x.Avere);
        var money=entries.Where(x=>x.OrigineEvento!="CassaIniziale"&&(x.Codice=="100"||x.Codice=="110")).ToArray();
        var cashFlows=money.GroupBy(x=>x.MovimentoId).Select(g=>g.Sum(x=>x.Dare-x.Avere)).ToArray();
        result.Incassi=cashFlows.Where(x=>x>0).Sum();result.UsciteMonetarie=-cashFlows.Where(x=>x<0).Sum();
        result.Contabilita=entries.Where(x=>x.Natura==NaturaConto.Ricavo||x.Natura==NaturaConto.Costo)
            .GroupBy(x=>string.IsNullOrWhiteSpace(x.NomeSettore)?x.AbbonamentoId!=null?"Abbonamenti":"Non assegnato":x.NomeSettore.Trim())
            .OrderBy(x=>x.Key).Select(g=>new ReportContabilita{Settore=g.Key,Ricavi=g.Where(x=>x.Natura==NaturaConto.Ricavo).Sum(x=>x.Avere-x.Dare),Costi=g.Where(x=>x.Natura==NaturaConto.Costo).Sum(x=>x.Dare-x.Avere)}).ToArray();
        var outstanding=await db.Sospesi.AsNoTracking().Where(x=>x.DataOra<end&&(x.Stato==StatoSospeso.Aperto||x.DataSaldo>=end)).Select(x=>new{x.Tipo,x.Importo}).ToArrayAsync(ct);
        result.NumeroSospesi=outstanding.Length;
        result.SospesiCrediti=outstanding.Where(x=>x.Tipo==TipoSospeso.Credito).Sum(x=>x.Importo);
        result.SospesiDebiti=outstanding.Where(x=>x.Tipo==TipoSospeso.Debito).Sum(x=>x.Importo);
        result.SospesiIncassati=(await db.Sospesi.AsNoTracking().Where(x=>x.Tipo==TipoSospeso.Credito&&x.Stato==StatoSospeso.Saldato&&x.DataSaldo>=start&&x.DataSaldo<end).Select(x=>x.Importo).ToArrayAsync(ct)).Sum();
        await transaction.CommitAsync(ct);
        return result;
    }
}
