using AppServices.Database;
using AppServices.Security;
using Microsoft.EntityFrameworkCore;
using Models.Context;
namespace Leonardo18.DatabaseHost.Login;

public sealed class LoginAuthenticationException : Exception
{
#if DEBUG
    ~LoginAuthenticationException() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public LoginAuthenticationException() : base("Credenziali non valide o operatore non abilitato.") { }
}
/// <summary>Port of Leonardo17 login queries and password verification, executing only in the managed host.</summary>
public sealed class LoginDatabaseRepository(Func<AppDbContext> contextFactory)
{
#if DEBUG
    ~LoginDatabaseRepository() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public async Task<LoginOperator[]> GetOperatoriAbilitati(CancellationToken ct = default)
    {
        await using var db = contextFactory();
        return await db.Operatori.AsNoTracking().Where(x => x.Abilitato).OrderBy(x => x.Nome)
            .Select(x => new LoginOperator { Id = x.Id, Name = x.Nome }).ToArrayAsync(ct).ConfigureAwait(false);
    }

    public async Task<LoginSession> Authenticate(int operatorId, string password, CancellationToken ct = default)
    {
        await using var db = contextFactory();
        var operatore = await db.Operatori.SingleOrDefaultAsync(x => x.Id == operatorId && x.Abilitato, ct).ConfigureAwait(false);
        if (operatore is null || !PasswordHasher.Verify(password, operatore.Password))
            throw new LoginAuthenticationException();

        var session = new LoginSession { OperatorId = operatore.Id, OperatorName = operatore.Nome };
        session.Stations = await db.Permessi.AsNoTracking().Where(x => x.OperatoreId == operatorId)
            .Select(x => new LoginStation
            {
                Id = x.PostazioneId, Name = x.Postazione!.Nome, TypeId = x.Postazione.TipoPostazioneId,
                ReturnTypeId = x.Postazione.TipoRientroId, ReturnHours = x.Postazione.TipoRientro!.DurataOre
            }).ToListAsync(ct).ConfigureAwait(false);
        foreach (var station in session.Stations)
        {
            station.Sectors = await db.Reparti.AsNoTracking().Where(x => x.PostazioneId == station.Id)
                .Select(x => new LoginSector { Id = x.SettoreId, Name = x.Settore!.Nome, Label = x.Settore.Label })
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var sector in station.Sectors)
                sector.Rates = await db.Listini.AsNoTracking().Where(x => x.SettoreId == sector.Id)
                    .Select(x => new LoginRate { Id = x.TariffaId, Label = x.Tariffa!.Label, Price = x.Tariffa.Prezzo })
                    .ToListAsync(ct).ConfigureAwait(false);
        }
        var days = await db.Giornate.AsNoTracking().Where(x => x.Aperta)
            .Select(x => new LoginDay { Id = x.Id, Start = x.DataInizio, End = x.DataFine, Open = x.Aperta })
            .Take(2).ToListAsync(ct).ConfigureAwait(false);
        if (days.Count > 1) throw new InvalidOperationException("Sono presenti piu giornate aperte.");
        session.Day = days.SingleOrDefault();

        ct.ThrowIfCancellationRequested();
        if (PasswordHasher.NeedsRehash(operatore.Password))
        {
            operatore.Password = PasswordHasher.Hash(password);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        return session;
    }
}