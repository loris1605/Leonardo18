using AppServices.Database;
using Microsoft.EntityFrameworkCore;
using Models.Context;
using Models.Tables;
namespace Leonardo18.DatabaseHost.Servizi;
public sealed partial class ServiziDatabaseRepository
{
    private static async Task<ServiziResult> ExecuteFidelity(AppDbContext db,ServiziRequest request,CancellationToken ct)
    {
        TipoFidelity? entity=null;
        if(request.Action is ServiziAction.Read or ServiziAction.Update or ServiziAction.Delete)
            entity=await db.TipiFidelity.SingleOrDefaultAsync(x=>x.Id==request.RecordId&&x.Id>0,ct)
                ??throw new InvalidOperationException("Fidelity non più disponibile. Aggiorna l'elenco.");
        ServiziRecord Map(TipoFidelity x)=>new(){Id=x.Id,Nome=x.Nome,NumeroIngressi=x.Traguardo,DurataGiorni=x.DurataGG,Prezzo=x.ValorePremio,TipoPremioId=x.TipoFidelityOutputId,Attivo=x.Attivo};
        if(request.Action==ServiziAction.Read)return new(){Records=[Map(entity!)]};
        if(request.Action is ServiziAction.Add or ServiziAction.Update)
        {
            var r=request.Record??throw new InvalidOperationException("Dati Fidelity assenti.");
            if(request.Action==ServiziAction.Add?r.Id!=0||request.RecordId!=0:r.Id!=request.RecordId)throw new InvalidOperationException("Identificativo non valido.");
            var name=r.Nome.Trim();
            if(name.Length is <2 or >25||r.NumeroIngressi<1||r.DurataGiorni<0||r.TipoPremioId is not (1 or 2)
                ||r.Prezzo<0||r.Prezzo>9999999999999999.99m||decimal.Round(r.Prezzo,2)!=r.Prezzo||r.TipoPremioId==2&&r.Prezzo<=0)
                throw new InvalidOperationException("Verifica nome, traguardo positivo, durata e importo del premio in denaro.");
            if(await db.TipiFidelity.AnyAsync(x=>x.Id!=r.Id&&x.Nome.ToUpper()==name.ToUpper(),ct))throw new InvalidOperationException("Esiste già una Fidelity con questo nome.");
            if(entity is null){entity=new();db.TipiFidelity.Add(entity);}
            entity.Nome=name;entity.Traguardo=r.NumeroIngressi;entity.DurataGG=r.DurataGiorni;entity.TipoFidelityInputId=1;
            entity.TipoFidelityOutputId=r.TipoPremioId;entity.ValorePremio=r.TipoPremioId==2?r.Prezzo:0;entity.Attivo=r.Attivo;
        }
        if(request.Action==ServiziAction.Delete)
        {
            if(await db.Fidelities.AnyAsync(x=>x.TipoFidelityId==request.RecordId,ct))throw new InvalidOperationException("Fidelity già utilizzata: puoi disattivarla, senza eliminare le raccolte dei soci.");
            db.TipiFidelity.Remove(entity!);
        }
        await db.SaveChangesAsync(ct);
        var types=await db.TipiFidelity.AsNoTracking().Where(x=>x.Id>0).OrderBy(x=>x.Nome).ToArrayAsync(ct);
        return new(){Records=types.Select(Map).ToArray(),SelectedId=request.Action==ServiziAction.Delete?0:entity?.Id??0};
    }
}
