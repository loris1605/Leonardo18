using System.Data;
using Microsoft.EntityFrameworkCore;
using Models.Context;
using Models.Tables;

namespace Leonardo18.DatabaseHost.Strisciate;

public interface IStrisciateSink
{
    Task InsertAsync(StrisciataInput input, CancellationToken ct = default);
}

/// <summary>Each HTTP update uses its own DbContext and the current application's configured database.</summary>
public sealed class StrisciateDatabaseSink(Func<AppDbContext> contextFactory, int operatorId, int stationId) : IStrisciateSink
{
    public async Task InsertAsync(StrisciataInput input, CancellationToken ct = default)
    {
        await using var db = contextFactory();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await db.Operatori.AnyAsync(x => x.Id == operatorId && x.Abilitato, ct)
            || !await db.Permessi.AnyAsync(x => x.OperatoreId == operatorId && x.PostazioneId == stationId
                && x.Postazione!.TipoPostazioneId == 2, ct)
            || await db.Giornate.CountAsync(x => x.Aperta, ct) != 1)
            throw new InvalidOperationException("La sessione di cassa non è più autorizzata alla ricezione.");
        var swipe = new Strisciata {
            CodiceSocio = input.CodiceSocio, NumeroTessera = input.NumeroTessera,
            Cognome = input.Cognome, Nome = input.Nome, Natoil = input.Natoil, Scadenza = input.Scadenza,
            DataOra = 0, CodMotivo = 0 // Same mapping as the supplied app.js.
        };
        // Existing cards update expiry and linked person birth date without changing membership or leaving a queued row.
        if (!await StrisciateProcessor.ApplyAsync(db, swipe, ct))
        { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return; }
        db.Strisciate.Add(swipe);
        // Store the incoming record and its resulting graph in one transaction, then consume the queue row.
        await db.SaveChangesAsync(ct);
        db.Strisciate.Remove(swipe);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
