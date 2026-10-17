using AppServices.Database;
using Microsoft.EntityFrameworkCore;
using Models.Context;
using Models.Sospesi;
using Models.Tables;

namespace Leonardo18.DatabaseHost.Cassa;

public sealed partial class CassaDatabaseRepository
{
    private static IQueryable<Abbonamento> EntrySubscriptions(AppDbContext db,int personId) => db.Abbonamenti
        .Where(x=>(x.PersonId1==personId||x.PersonId2==personId)&&x.Attivo
            &&x.DataInizio<=DateTime.Today&&x.DataFine>=DateTime.Today
            &&!db.Sospesi.Any(s=>s.AbbonamentoId==x.Id&&s.Stato==StatoSospeso.Aperto)
            &&(x.PagatoAllAcquisto==true||x.PrezzoAcquisto==0
                ||db.Sospesi.Any(s=>s.AbbonamentoId==x.Id&&s.Stato==StatoSospeso.Saldato)));

    private static async Task<CassaAbbonamentoIngresso[]> LoadEntrySubscriptions(AppDbContext db,int personId,CancellationToken ct)
    {
        var subscriptions=await EntrySubscriptions(db,personId).AsNoTracking().Include(x=>x.TipoAbbonamento)
            .Include(x=>x.AbbonamentoConti).OrderBy(x=>x.DataFine).ThenBy(x=>x.Id).ToArrayAsync(ct);
        return subscriptions.Select(x=>new CassaAbbonamentoIngresso
        {
            Id=x.Id,Nome=x.Nome,DataFine=x.DataFine,
            Bollini=Enumerable.Range(1,Math.Max(0,x.IngressiAcquistati??x.TipoAbbonamento?.NumeroIngressi??0))
                .Select(number=>new CassaBollino {AbbonamentoId=x.Id,Numero=number,
                    Usato=x.AbbonamentoConti.Any(r=>r.NumeroIngresso==number&&r.Entrato),
                    DataIngresso=x.AbbonamentoConti.Where(r=>r.NumeroIngresso==number&&r.Entrato)
                        .Select(r=>(DateTime?)r.DataIngresso).FirstOrDefault()}).ToArray()
        }).ToArray();
    }

    // Execute holds a serializable transaction: slot reads and the new sheet commit together.
    private static async Task<AbbonamentoConto> ConsumeEntrySubscription(AppDbContext db,CassaSocio? member,CassaRequest request,CancellationToken ct)
    {
        if(member is null)throw new InvalidOperationException("Identifica il socio tramite la tessera.");
        if(request.TariffaId!=0)throw new InvalidOperationException("Seleziona un solo tipo di ingresso.");
        var subscription=await EntrySubscriptions(db,member.PersonId).Include(x=>x.TipoAbbonamento)
            .Include(x=>x.AbbonamentoConti).SingleOrDefaultAsync(x=>x.Id==request.AbbonamentoIngressoId,ct)
            ??throw new InvalidOperationException("Abbonamento non disponibile, non pagato o non appartenente al socio. Identifica nuovamente il socio.");
        var total=subscription.IngressiAcquistati??subscription.TipoAbbonamento?.NumeroIngressi??0;
        if(request.NumeroIngressoAbbonamento<1||request.NumeroIngressoAbbonamento>total)
            throw new InvalidOperationException("Bollino abbonamento non valido.");
        var rows=subscription.AbbonamentoConti.Where(x=>x.NumeroIngresso==request.NumeroIngressoAbbonamento).ToArray();
        if(rows.Any(x=>x.Entrato))throw new InvalidOperationException("Questo ingresso abbonamento è già stato usato. Identifica nuovamente il socio.");
        var row=rows.FirstOrDefault();
        if(row is null)
        {
            row=new AbbonamentoConto {AbbonamentoId=subscription.Id,NumeroIngresso=request.NumeroIngressoAbbonamento,
                Nome=$"Ingresso {request.NumeroIngressoAbbonamento}"};
            db.AbbonamentoConti.Add(row);
        }
        row.Entrato=true;row.DataIngresso=DateTime.Now;
        row.Note=$"Socio {member.PersonId}; tessera {member.NumeroTessera}; posizione {request.Posizione.Trim()}";
        return row;
    }
}
