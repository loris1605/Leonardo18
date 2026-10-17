using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AppServices.Database;
using Microsoft.EntityFrameworkCore;
using Models.Context;
using Models.Sospesi;
using Models.Tables;
namespace Leonardo18.DatabaseHost.Cassa;

public sealed partial class CassaDatabaseRepository
{
    private static async Task<(int Count,decimal Total)> MemberPending(AppDbContext db,int personId,CancellationToken ct)
    {
        var amounts=await db.Sospesi.AsNoTracking().Where(x=>x.Stato==StatoSospeso.Aperto&&x.Tipo==TipoSospeso.Credito&&(x.PersonId==personId||x.PersonId2==personId)).Select(x=>x.Importo).ToArrayAsync(ct);
        return(amounts.Length,amounts.Sum());
    }
    private static async Task LoadPending(AppDbContext db,CassaRequest request,CassaResult result,CancellationToken ct)
    {
        var query=db.Sospesi.AsNoTracking().Where(x=>x.Stato==StatoSospeso.Aperto);
        if(!string.IsNullOrWhiteSpace(request.NumeroTessera))
        {
            var member=await FindCard(db,request.NumeroTessera,ct)??throw new InvalidOperationException("Tessera non trovata per la ricerca dei sospesi.");
            result.Socio=member;var personId=member.PersonId;
            query=query.Where(x=>x.PersonId==personId||x.PersonId2==personId);
        }
        result.Sospesi=await query.OrderBy(x=>x.DataOra).ThenBy(x=>x.Id).Select(x=>new CassaSospeso
        {
            Id=x.Id,PersonId=x.PersonId,PersonId2=x.PersonId2,Nominativo=x.Nominativo,Nominativo2=x.Nominativo2??"",
            NumeroTessera=x.NumeroTessera??"",NumeroTessera2=x.NumeroTessera2??"",Descrizione=x.Descrizione,
            Tipo=x.Tipo==TipoSospeso.Credito?"Credito":"Debito",Origine=x.Origine==OrigineSospeso.Abbonamento?"Abbonamento":x.Origine==OrigineSospeso.Uscita?"Uscita":"Manuale",
            Importo=x.Importo,DataOra=x.DataOra,DataScadenza=x.DataScadenza,
            CanIncassare=x.Tipo==TipoSospeso.Credito&&((x.Origine==OrigineSospeso.Abbonamento&&x.AbbonamentoId!=null&&x.PartitaContabileId==null)||(x.Origine==OrigineSospeso.Uscita&&x.SchedaId!=null&&db.SchedeUscite.Any(s=>s.Id==x.SchedaId)))
        }).ToArrayAsync(ct);
        var exitIds=result.Sospesi.Where(x=>x.Origine=="Uscita").Select(x=>x.Id).ToArray();
        if(exitIds.Length==0)return;
        var sources=await db.Sospesi.AsNoTracking().Where(x=>exitIds.Contains(x.Id)).ToArrayAsync(ct);
        var sheetIds=sources.Where(x=>x.SchedaId!=null).Select(x=>x.SchedaId!.Value).Distinct().ToArray();
        var sheets=await db.SchedeUscite.AsNoTracking().Include(x=>x.Conto).Where(x=>sheetIds.Contains(x.Id)).ToArrayAsync(ct);
        foreach(var pending in result.Sospesi.Where(x=>x.Origine=="Uscita"))
        {
            var source=sources.Single(x=>x.Id==pending.Id);
            var sheet=sheets.SingleOrDefault(x=>x.Id==source.SchedaId);
            if(sheet is null)continue;
            var rows=new List<Models.Tables.SchedaUscitaConto>();
            foreach(var row in sheet.Conto.Where(x=>!x.Pagato))
                if(source.SchedaContoId is {} rowId ? row.Id==rowId : source.PartitaContabileId==null &&
                    !await db.ContabilitaMovimenti.AnyAsync(x=>x.OrigineEntita=="SchedaConto"&&x.OrigineId==row.Id.ToString()&&(x.OrigineEvento=="VoceSpesa"||x.OrigineEvento=="StornoVoceSpesa"||x.OrigineEvento=="IngressoPagato"),ct))rows.Add(row);
            pending.Dettaglio=string.Join(Environment.NewLine,rows.OrderBy(x=>x.DataOra).ThenBy(x=>x.Id)
                .Select(x=>$"{x.DataOra:dd/MM/yyyy HH:mm} · {x.DescSettore} · {x.VoiceDesc}: {x.VoicePrice:N2} €"));
        }
    }
    private static async Task<Sospeso> SettlePending(AppDbContext db,int operatorId,Postazione station,CassaRequest request,CancellationToken ct)
    {
        var pending=await db.Sospesi.SingleOrDefaultAsync(x=>x.Id==request.SospesoId,ct)
            ??throw new InvalidOperationException("Sospeso non trovato.");
        if(pending.Origine==OrigineSospeso.Uscita)return await SettleExitPending(db,operatorId,station,pending,ct);
        if(pending.Tipo!=TipoSospeso.Credito||pending.Origine!=OrigineSospeso.Abbonamento||pending.AbbonamentoId is null||pending.PartitaContabileId is not null)
            throw new InvalidOperationException("Il saldo di questo tipo di sospeso verrà collegato alla relativa funzione di origine.");
        if(pending.Stato==StatoSospeso.Saldato)return pending; // Lost responses can be retried without a second receipt.
        var subscription=await db.Abbonamenti.SingleOrDefaultAsync(x=>x.Id==pending.AbbonamentoId,ct)
            ??throw new InvalidOperationException("Abbonamento del sospeso non trovato.");
        if(subscription.AcquistoId!=pending.IdOperazione||subscription.PrezzoAcquisto!=pending.Importo||subscription.PagatoAllAcquisto!=false||pending.Valuta!="EUR")
            throw new InvalidOperationException("Il sospeso non coincide con i dati originali dell'acquisto.");
        var key=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"SaldoSospeso:{pending.IdOperazione:D}"))[..16]);
        var first=new SubscriptionHolder(pending.PersonId??subscription.PersonId1,pending.Nominativo,pending.NumeroSocio,pending.NumeroTessera??"");
        var second=pending.PersonId2 is null?null:new SubscriptionHolder(pending.PersonId2.Value,pending.Nominativo2??"",pending.NumeroSocio2,pending.NumeroTessera2??"");
        var movement=await SubscriptionMovement(db,operatorId,station,subscription,key,"Sospeso",pending.Id.ToString(CultureInfo.InvariantCulture),"SaldoSospesoAbbonamento",first,second,ct);
        db.ContabilitaMovimenti.Add(movement);
        pending.MovimentoSaldo=movement;pending.Stato=StatoSospeso.Saldato;pending.DataSaldo=movement.DataOra;
        pending.OperatoreSaldoId=operatorId;pending.PostazioneSaldoId=station.Id;pending.GiornataSaldoId=movement.GiornataId;
        await db.SaveChangesAsync(ct);
        return pending;
    }
}
