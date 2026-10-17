using System.Data;
using AppServices.Database;
using Microsoft.EntityFrameworkCore;
using Models.Context;
using Models.Tables;
namespace Leonardo18.DatabaseHost.Menu;

/// <summary>Queries and day writes from Leonardo17, isolated from the NativeAOT desktop.</summary>
public sealed class MenuDatabaseRepository(Func<AppDbContext> contextFactory)
{
#if DEBUG
    ~MenuDatabaseRepository() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public async Task<MenuState> Execute(int operatorId, string operation, CancellationToken ct = default)
    {
        await using var db = contextFactory();
        // A serializable transaction prevents two clients from opening a day simultaneously.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await db.Operatori.AnyAsync(x => x.Id == operatorId && x.Abilitato, ct))
            throw new InvalidOperationException("Operatore non abilitato.");
        var stations = await db.Permessi.AsNoTracking().Where(x => x.OperatoreId == operatorId)
            .Select(x => new LoginStation { Id = x.PostazioneId, Name = x.Postazione!.Nome,
                TypeId = x.Postazione.TipoPostazioneId }).Distinct().ToArrayAsync(ct);
        var days = await db.Giornate.Where(x => x.Aperta).Take(2).ToListAsync(ct);
        if (days.Count > 1) throw new InvalidOperationException("Sono presenti più giornate aperte. Verifica la sessione contabile.");
        var day = days.SingleOrDefault();
        if (operation != "menu-load" && !stations.Any(x => x.TypeId is 1 or 2))
            throw new InvalidOperationException("Non hai il permesso di gestire la giornata.");
        switch (operation)
        {
            case "menu-open":
                if (day is null)
                {
                    day = new Giornata { Aperta = true, DataInizio = DateTime.Now, DataFine = DateTime.MaxValue };
                    db.Giornate.Add(day);
                    await db.SaveChangesAsync(ct);
                }
                break;
            case "menu-close":
                if (day is not null)
                {
                    day.Aperta = false; day.DataFine = DateTime.Now;
                    await db.SaveChangesAsync(ct);
                    day = null;
                }
                break;
            case "menu-load": break;
            default: throw new InvalidOperationException("Operazione menu non supportata.");
        }
        await transaction.CommitAsync(ct);
        return new MenuState { Stations = stations,
            Day = day is null ? null : new LoginDay { Id = day.Id, Start = day.DataInizio, End = day.DataFine, Open = day.Aperta } };
    }
}