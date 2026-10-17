using System.Data;
using AppServices.Database;
using Microsoft.EntityFrameworkCore;
using Models.Context;
using Models.Tables;
namespace Leonardo18.DatabaseHost.Servizi;

public sealed partial class ServiziDatabaseRepository(Func<AppDbContext> contextFactory)
{
    public async Task<ServiziResult> Execute(int operatorId, ServiziRequest request, CancellationToken ct=default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(request.Action)) throw new InvalidOperationException("Operazione servizi non supportata.");
        await using var db=contextFactory();
        await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        if (!await db.Operatori.AnyAsync(x=>x.Id==operatorId && x.Abilitato,ct)
            || !await db.Permessi.AnyAsync(x=>x.OperatoreId==operatorId && x.Postazione!.TipoPostazioneId==1,ct))
            throw new InvalidOperationException("La gestione servizi richiede un amministratore abilitato.");
        if(!Enum.IsDefined(request.Section))throw new InvalidOperationException("Sezione servizi non valida.");
        if(request.Section==ServiziSection.Fidelity)
        {
            var fidelity=await ExecuteFidelity(db,request,ct);await transaction.CommitAsync(ct);return fidelity;
        }
        TipoAbbonamento? entity=null;
        if (request.Action is ServiziAction.Read or ServiziAction.Update or ServiziAction.Delete)
        {
            if(request.RecordId<=0) throw new InvalidOperationException("Identificativo non valido.");
            entity=await db.TipiAbbonamento.SingleOrDefaultAsync(x=>x.Id==request.RecordId,ct)
                ?? throw new InvalidOperationException("Abbonamento non più disponibile. Aggiorna l'elenco.");
        }
        if(request.Action==ServiziAction.Read) return new() {Records=[Map(entity!)]};
        if(request.Action is ServiziAction.Add or ServiziAction.Update)
        {
            var r=request.Record ?? throw new InvalidOperationException("Dati assenti.");
            if(request.Action==ServiziAction.Add ? r.Id!=0 || request.RecordId!=0 : r.Id!=request.RecordId)
                throw new InvalidOperationException("Identificativo non valido.");
            var name=(r.Nome??"").Trim();
            if(name.Length<2 || name.Length>25) throw new InvalidOperationException("Il nome deve contenere da 2 a 25 caratteri.");
            if(r.NumeroIngressi<0 || r.DurataGiorni<0) throw new InvalidOperationException("Ingressi e durata non possono essere negativi.");
            if(r.Prezzo<0 || r.Prezzo>9999999999999999.99m || decimal.Round(r.Prezzo,2)!=r.Prezzo)
                throw new InvalidOperationException("Prezzo non valido: usa un importo non negativo con massimo due decimali.");
            var upper=name.ToUpperInvariant();
            if(await db.TipiAbbonamento.AnyAsync(x=>x.Id!=r.Id && x.Nome.ToUpper()==upper,ct))
                throw new InvalidOperationException("Esiste già un tipo di abbonamento con questo nome.");
            if(entity is null) { entity=new(); db.TipiAbbonamento.Add(entity); }
            entity.Nome=name; entity.NumeroIngressi=r.NumeroIngressi; entity.DurataGiorni=r.DurataGiorni;
            entity.Prezzo=r.Prezzo; entity.Attivo=r.Attivo;
        }
        if(request.Action==ServiziAction.Delete)
        {
            if(await db.Abbonamenti.AnyAsync(x=>x.TipoAbbonamentoId==request.RecordId,ct))
                throw new InvalidOperationException("Il tipo di abbonamento è già utilizzato e non può essere eliminato. Puoi disattivarlo.");
            db.TipiAbbonamento.Remove(entity!);
        }
        try { await db.SaveChangesAsync(ct); }
        catch(DbUpdateException) { throw new InvalidOperationException("Salvataggio non riuscito: abbonamento già presente o utilizzato da altri dati."); }
        var rows=await db.TipiAbbonamento.AsNoTracking().Where(x=>x.Id>0).OrderBy(x=>x.Nome).ToListAsync(ct);
        await transaction.CommitAsync(ct);
        return new() { Records=rows.Select(Map).ToArray(), SelectedId=request.Action==ServiziAction.Delete?0:entity?.Id??0 };
    }
    private static ServiziRecord Map(TipoAbbonamento x)=>new() {Id=x.Id,Nome=x.Nome,NumeroIngressi=x.NumeroIngressi,DurataGiorni=x.DurataGiorni,Prezzo=x.Prezzo,Attivo=x.Attivo};
}