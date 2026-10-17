using Microsoft.EntityFrameworkCore;
using Models.Context;
using Models.Tables;
namespace Leonardo18.DatabaseHost.Cassa;
public sealed partial class CassaDatabaseRepository
{
    // Complete the former monetary-only reversal without reversing cash a second time.
    private static async Task<bool> CompletePreviousEntryReversal(AppDbContext db,SchedaConto source,CancellationToken ct)
    {
        var key=EntryAccountingKey(source.Id);
        var original=await db.ContabilitaMovimenti.AsNoTracking().SingleOrDefaultAsync(x=>x.IdOperazione==key,ct);
        if(original is null)return false;
        var reversal=await db.ContabilitaMovimenti.AsNoTracking().SingleOrDefaultAsync(x=>x.StornoDiId==original.Id&&x.OrigineEvento=="StornoIngresso",ct);
        if(reversal is null)return false;
        if(!int.TryParse(reversal.OrigineId,out var id))throw new InvalidOperationException("Riga dello storno ingresso non identificabile.");
        var line=await db.SchedeConto.SingleOrDefaultAsync(x=>x.Id==id&&x.SchedaId==source.SchedaId,ct)
            ??throw new InvalidOperationException("Riga dello storno ingresso non disponibile.");
        await AnnullaIngresso(db,source,ct);
        var label="STORNO - NON ENTRA - "+source.VoiceDesc;line.VoiceDesc=label.Length>50?label[..50]:label;
        await db.SaveChangesAsync(ct);return true;
    }

    private static async Task AnnullaIngresso(AppDbContext db,SchedaConto source,CancellationToken ct)
    {
        var sheet=await db.Schede.SingleAsync(x=>x.Id==source.SchedaId,ct);
        if(sheet.IngressoAnnullato)throw new InvalidOperationException("Ingresso già annullato: NON ENTRA.");
        var events=await db.FidelityEntries.Where(x=>x.SchedaId==sheet.Id&&((x.TipoMovimento>=1&&x.TipoMovimento<=3)||x.TipoMovimento==ScontoIngressoFid)).ToArrayAsync(ct);
        foreach(var originalGroup in events.GroupBy(x=>x.FidelityId))
        {
            var f=await db.Fidelities.Include(x=>x.FidelityConti).SingleAsync(x=>x.Id==originalGroup.Key,ct);
            var group=originalGroup.Where(e=>e.TipoMovimento!=PremioDenaroFid||!f.FidelityConti.Any(a=>a.TipoMovimento==9&&a.SchedaId==e.SchedaId)).ToList();
            var points=f.FidelityConti.Where(x=>x.Ciclo==f.Ciclo).Sum(x=>x.Value)-group.Where(x=>x.Ciclo==f.Ciclo).Sum(x=>x.Value);
            if(points<0)
            {
                var reward=f.FidelityConti.Where(x=>x.TipoMovimento==PremioDenaroFid&&x.Ciclo==f.Ciclo&&!group.Any(e=>e.Id==x.Id)
                    &&!f.FidelityConti.Any(a=>a.TipoMovimento==9&&a.SchedaId==x.SchedaId)).OrderByDescending(x=>x.Id).FirstOrDefault();
                if(reward is not null){group.Add(reward);points-=reward.Value;}
            }
            var credit=f.FidelityConti.Sum(x=>x.Importo)-group.Sum(x=>x.Importo);
            if(points<0||credit<0)throw new InvalidOperationException("Il premio Fidelity collegato ai timbri è già stato utilizzato: regolarizza il premio prima di annullare l'ingresso.");
            foreach(var e in group)
            {
                var undo=FidelityRow(f,e.TipoMovimento switch{1=>7,2=>8,3=>9,_=>StornoScontoFid},e.SchedaId,-e.Value,-e.Importo,source.Id);
                undo.Ciclo=e.Ciclo;db.FidelityEntries.Add(undo);
            }
        }
        var subscription=await db.AbbonamentoConti.Where(x=>x.SchedaIngressoId==sheet.Id&&x.Entrato).ToArrayAsync(ct);
        foreach(var entry in subscription){entry.Entrato=false;entry.SchedaIngressoId=null;entry.Note+="; NON ENTRA - ingresso restituito";}
        sheet.IngressoAnnullato=true;await db.SaveChangesAsync(ct);
    }
}
