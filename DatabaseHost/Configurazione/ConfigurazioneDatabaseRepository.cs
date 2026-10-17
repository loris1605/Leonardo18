using System.Data;
using AppServices.Database;
using AppServices.Security;
using Microsoft.EntityFrameworkCore;
using Models.Context;
using Models.Tables;
namespace Leonardo18.DatabaseHost.Configurazione;

public sealed class ConfigurazioneDatabaseRepository(Func<AppDbContext> contextFactory)
{
#if DEBUG
    ~ConfigurazioneDatabaseRepository() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif
    public async Task<ConfigurazioneResult> Execute(int operatorId, ConfigurazioneRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(request.Section) || !Enum.IsDefined(request.Action))
            throw new InvalidOperationException("Operazione configurazione non supportata.");
        await using var db = contextFactory();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await db.Operatori.AnyAsync(x => x.Id == operatorId && x.Abilitato, ct)
            || !await db.Permessi.AnyAsync(x => x.OperatoreId == operatorId && x.Postazione!.TipoPostazioneId == 1, ct))
            throw new InvalidOperationException("La configurazione richiede un operatore amministratore abilitato.");
        ConfigurazioneResult result;
        switch (request.Action)
        {
            case ConfigurazioneAction.Load:
                result = new() { Records = await Load(db, request.Section, ct) };
                break;
            case ConfigurazioneAction.Read:
                var record = request.RecordId == 0 ? new ConfigurazioneRecord() :
                    (await Load(db, request.Section, ct)).SingleOrDefault(x => x.Id == request.RecordId)
                    ?? throw new InvalidOperationException("Elemento non più disponibile. Aggiorna l'elenco.");
                result = new() { Records = [record],
                    Tipi = request.Section == ConfigurazioneSection.Postazioni
                        ? await db.TipiPostazione.OrderBy(x=>x.Nome).Select(x=>new ConfigurazioneOption{Id=x.Id,Nome=x.Nome}).ToArrayAsync(ct)
                        : request.Section == ConfigurazioneSection.Settori
                        ? await db.TipiSettore.OrderBy(x=>x.Nome).Select(x=>new ConfigurazioneOption{Id=x.Id,Nome=x.Nome}).ToArrayAsync(ct) : [],
                    Rientri = request.Section == ConfigurazioneSection.Postazioni
                        ? await db.TipiRientro.OrderBy(x=>x.Nome).Select(x=>new ConfigurazioneOption{Id=x.Id,Nome=x.Nome}).ToArrayAsync(ct) : [] };
                break;
            case ConfigurazioneAction.LinksRead:
                result = new() { Scelte = await LinksRead(db, request, ct) };
                break;
            case ConfigurazioneAction.LinksSave:
                await LinksSave(db, request, ct);
                result = new() { RecordId = request.RecordId };
                break;
            default:
                result = new() { RecordId = await Write(db, request, ct) };
                break;
        }
        try { await SaveChanges(db, ct); }
        catch (DbUpdateException) { throw new InvalidOperationException("Salvataggio non riuscito: elemento già presente o utilizzato da altri dati."); }
        await transaction.CommitAsync(ct);
        return result;
    }

    private static async Task<ConfigurazioneRecord[]> Load(AppDbContext db, ConfigurazioneSection section, CancellationToken ct)
    {
        // Passwords and password hashes never leave the managed worker.
        return section switch
        {
            ConfigurazioneSection.Operatori => (await db.Operatori.AsNoTracking().Where(x=>x.Id>=-1)
                .Include(x=>x.Permessi).ThenInclude(x=>x.Postazione).ThenInclude(x=>x!.TipoPostazione)
                .OrderBy(x=>x.Nome).ToListAsync(ct)).Select(x=>new ConfigurazioneRecord {
                    Id=x.Id,Nome=x.Nome,Abilitato=x.Abilitato,Badge=x.Pass,
                    Dettagli=$"{(x.Abilitato ? "Abilitato" : "Disabilitato")} · Badge {x.Pass}",
                    Relazioni=x.Permessi.OrderBy(p=>p.Postazione!.Nome).Select(p=>$"{p.Postazione!.Nome} · {p.Postazione.TipoPostazione?.Nome}").ToArray()
                }).ToArray(),
            ConfigurazioneSection.Postazioni => (await db.Postazioni.AsNoTracking().Include(x=>x.TipoPostazione)
                .Include(x=>x.TipoRientro).Include(x=>x.Reparti).ThenInclude(x=>x.Settore)
                .OrderBy(x=>x.Nome).ToListAsync(ct)).Select(x=>new ConfigurazioneRecord {
                    Id=x.Id,Nome=x.Nome,TipoId=x.TipoPostazioneId,RientroId=x.TipoRientroId,
                    Dettagli=$"{x.TipoPostazione?.Nome} · Rientro: {x.TipoRientro?.Nome}",
                    Relazioni=x.Reparti.OrderBy(r=>r.Settore!.Nome).Select(r=>r.Settore!.Nome).ToArray()
                }).ToArray(),
            ConfigurazioneSection.Settori => (await db.Settori.AsNoTracking().Include(x=>x.TipoSettore)
                .Include(x=>x.Listini).ThenInclude(x=>x.Tariffa).OrderBy(x=>x.Nome).ToListAsync(ct)).Select(x=>new ConfigurazioneRecord {
                    Id=x.Id,Nome=x.Nome,Etichetta=x.Label,TipoId=x.TipoSettoreId,Dettagli=$"{x.TipoSettore?.Nome} · {x.Label}",
                    Relazioni=x.Listini.OrderBy(r=>r.Tariffa!.Nome).Select(r=>$"{r.Tariffa!.Nome} · {r.Tariffa.Prezzo:0.00} €").ToArray()
                }).ToArray(),
            ConfigurazioneSection.Tariffe => await db.Tariffe.AsNoTracking().OrderBy(x=>x.Nome)
                .Select(x=>new ConfigurazioneRecord {Id=x.Id,Nome=x.Nome,Etichetta=x.Label,Prezzo=x.Prezzo,FreeDrink=x.IsFreeDrink}).ToArrayAsync(ct),
            ConfigurazioneSection.Rientri => await db.TipiRientro.AsNoTracking().OrderBy(x=>x.Nome)
                .Select(x=>new ConfigurazioneRecord {Id=x.Id,Nome=x.Nome,DurataOre=x.DurataOre}).ToArrayAsync(ct),
            _ => throw new InvalidOperationException("Sezione non disponibile.")
        };
    }

    private static async Task<int> Write(AppDbContext db, ConfigurazioneRequest request, CancellationToken ct)
    {
        var action=request.Action;
        var r=request.Record ?? throw new InvalidOperationException("Dati assenti.");
        if (action == ConfigurazioneAction.Add ? r.Id!=0 || request.RecordId!=0 : r.Id==0 || r.Id!=request.RecordId)
            throw new InvalidOperationException("Identificativo non valido.");
        if (action != ConfigurazioneAction.Delete)
        {
            r.Nome=(r.Nome??"").Trim(); r.Etichetta=(r.Etichetta??"").Trim();
            int max=request.Section==ConfigurazioneSection.Operatori?50:25;
            if(r.Nome.Length<2||r.Nome.Length>max)throw new InvalidOperationException($"Il nome deve contenere da 2 a {max} caratteri.");
            if(request.Section is ConfigurazioneSection.Settori or ConfigurazioneSection.Tariffe && (r.Etichetta.Length<2||r.Etichetta.Length>25))throw new InvalidOperationException("L'etichetta deve contenere da 2 a 25 caratteri.");
            if(r.Badge<0||r.DurataOre<0)throw new InvalidOperationException("Badge e durata non possono essere negativi.");
            if(r.Prezzo<0||r.Prezzo>9999999999999999.99m||decimal.Round(r.Prezzo,2)!=r.Prezzo)
                throw new InvalidOperationException("Prezzo non valido: usa un importo non negativo con massimo due decimali.");
        }
        switch(request.Section)
        {
            case ConfigurazioneSection.Operatori:
            {
                var entity=action==ConfigurazioneAction.Add?new Operatore():
                    await db.Operatori.SingleOrDefaultAsync(x=>x.Id==r.Id,ct)??throw Missing();
                if(action==ConfigurazioneAction.Delete)
                {
                    if(entity.Id<0)throw new InvalidOperationException("L'operatore di sistema non può essere eliminato.");
                    if(await db.Permessi.AnyAsync(x=>x.OperatoreId==entity.Id,ct))throw InUse("permessi");
                    db.Operatori.Remove(entity);break;
                }
                if(entity.Id==-1&&!r.Abilitato)throw new InvalidOperationException("L'amministratore di sistema deve restare abilitato.");
                await Unique(db.Operatori,r,ct);
                if(action==ConfigurazioneAction.Add&&string.IsNullOrEmpty(r.Password))throw new InvalidOperationException("Inserisci la password.");
                if(!string.IsNullOrEmpty(r.Password))
                {
                    if(string.IsNullOrWhiteSpace(r.Password)||r.Password.Length<2||r.Password.Length>128)
                        throw new InvalidOperationException("La password deve contenere da 2 a 128 caratteri.");
                    entity.Password=PasswordHasher.Hash(r.Password);
                }
                entity.Nome=r.Nome;entity.Abilitato=r.Abilitato;entity.Pass=r.Badge;
                if(action==ConfigurazioneAction.Add)db.Operatori.Add(entity);
                break;
            }
            case ConfigurazioneSection.Postazioni:
            {
                var entity=action==ConfigurazioneAction.Add?new Postazione():
                    await db.Postazioni.SingleOrDefaultAsync(x=>x.Id==r.Id,ct)??throw Missing();
                if(action==ConfigurazioneAction.Delete)
                {
                    if(entity.Id<0)throw new InvalidOperationException("La postazione di sistema non può essere eliminata.");
                    if(await db.Permessi.AnyAsync(x=>x.PostazioneId==entity.Id,ct)||await db.Reparti.AnyAsync(x=>x.PostazioneId==entity.Id,ct))throw InUse("permessi o reparti");
                    db.Postazioni.Remove(entity);break;
                }
                await Unique(db.Postazioni,r,ct);
                if(!await db.TipiPostazione.AnyAsync(x=>x.Id==r.TipoId,ct)||!await db.TipiRientro.AnyAsync(x=>x.Id==r.RientroId,ct))
                    throw new InvalidOperationException("Seleziona un tipo di postazione e un tipo di rientro validi.");
                if(entity.Id<0&&r.TipoId!=entity.TipoPostazioneId)throw new InvalidOperationException("Il tipo della postazione di sistema non può essere modificato.");
                entity.Nome=r.Nome;entity.TipoPostazioneId=r.TipoId;entity.TipoRientroId=r.RientroId;
                if(action==ConfigurazioneAction.Add)db.Postazioni.Add(entity);
                break;
            }
            case ConfigurazioneSection.Settori:
            {
                var entity=action==ConfigurazioneAction.Add?new Settore():
                    await db.Settori.SingleOrDefaultAsync(x=>x.Id==r.Id,ct)??throw Missing();
                if(action==ConfigurazioneAction.Delete)
                {
                    if(await db.Reparti.AnyAsync(x=>x.SettoreId==entity.Id,ct)||await db.Listini.AnyAsync(x=>x.SettoreId==entity.Id,ct))throw InUse("reparti o listini");
                    db.Settori.Remove(entity);break;
                }
                await Unique(db.Settori,r,ct);
                if(!await db.TipiSettore.AnyAsync(x=>x.Id==r.TipoId,ct))throw new InvalidOperationException("Seleziona un tipo di settore valido.");
                entity.Nome=r.Nome;entity.Label=r.Etichetta;entity.TipoSettoreId=r.TipoId;
                if(action==ConfigurazioneAction.Add)db.Settori.Add(entity);
                break;
            }
            case ConfigurazioneSection.Tariffe:
            {
                var entity=action==ConfigurazioneAction.Add?new Tariffa():
                    await db.Tariffe.SingleOrDefaultAsync(x=>x.Id==r.Id,ct)??throw Missing();
                if(action==ConfigurazioneAction.Delete)
                {
                    if(await db.Listini.AnyAsync(x=>x.TariffaId==entity.Id,ct))throw InUse("listini");
                    db.Tariffe.Remove(entity);break;
                }
                await Unique(db.Tariffe,r,ct);
                entity.Nome=r.Nome;entity.Label=r.Etichetta;entity.Prezzo=r.Prezzo;entity.IsFreeDrink=r.FreeDrink;
                if(action==ConfigurazioneAction.Add)db.Tariffe.Add(entity);
                break;
            }
            case ConfigurazioneSection.Rientri:
            {
                var entity=action==ConfigurazioneAction.Add?new TipoRientro():
                    await db.TipiRientro.SingleOrDefaultAsync(x=>x.Id==r.Id,ct)??throw Missing();
                if(action==ConfigurazioneAction.Delete)
                {
                    if(entity.Id<0)throw new InvalidOperationException("I rientri di sistema non possono essere eliminati.");
                    if(await db.Postazioni.AnyAsync(x=>x.TipoRientroId==entity.Id,ct))throw InUse("postazioni");
                    db.TipiRientro.Remove(entity);break;
                }
                await Unique(db.TipiRientro,r,ct);
                if(entity.Id<0&&r.DurataOre!=entity.DurataOre)throw new InvalidOperationException("La durata del rientro di sistema non può essere modificata.");
                entity.Nome=r.Nome;entity.DurataOre=r.DurataOre;
                if(action==ConfigurazioneAction.Add)db.TipiRientro.Add(entity);
                break;
            }
        }
        await SaveChanges(db, ct);
        if(!await db.Operatori.AnyAsync(x=>x.Abilitato&&x.Permessi.Any(p=>p.Postazione!.TipoPostazioneId==1),ct))
            throw new InvalidOperationException("Deve rimanere almeno un amministratore abilitato.");
        return action==ConfigurazioneAction.Add ? request.Section switch {
            ConfigurazioneSection.Operatori=>db.ChangeTracker.Entries<Operatore>().Single(x=>x.Entity.Nome==r.Nome).Entity.Id,
            ConfigurazioneSection.Postazioni=>db.ChangeTracker.Entries<Postazione>().Single().Entity.Id,
            ConfigurazioneSection.Settori=>db.ChangeTracker.Entries<Settore>().Single().Entity.Id,
            ConfigurazioneSection.Tariffe=>db.ChangeTracker.Entries<Tariffa>().Single().Entity.Id,
            _=>db.ChangeTracker.Entries<TipoRientro>().Single().Entity.Id
        } : action==ConfigurazioneAction.Delete?0:r.Id;
    }
    private static async Task SaveChanges(AppDbContext db, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException("Salvataggio non riuscito: elemento già presente o utilizzato da altri dati.");
        }
    }
    private static async Task Unique<T>(DbSet<T> set,ConfigurazioneRecord r,CancellationToken ct) where T:class,IStandardTable
    {
        var name=r.Nome.ToUpperInvariant();
        if(await set.AnyAsync(x=>x.Id!=r.Id&&x.Nome.ToUpper()==name,ct))throw new InvalidOperationException("Nome già registrato.");
    }
    private static InvalidOperationException Missing()=>new("Elemento non più disponibile. Aggiorna l'elenco.");
    private static InvalidOperationException InUse(string link)=>new($"Prima di eliminare l'elemento rimuovi i relativi {link}.");

    private static async Task<ConfigurazioneOption[]> LinksRead(AppDbContext db,ConfigurazioneRequest r,CancellationToken ct)
    {
        switch(r.Section)
        {
            case ConfigurazioneSection.Operatori:
                if(!await db.Operatori.AnyAsync(x=>x.Id==r.RecordId,ct))throw Missing();
                return await db.Postazioni.OrderBy(x=>x.Nome).Select(x=>new ConfigurazioneOption{
                    Id=x.Id,Nome=x.Nome+" · "+x.TipoPostazione!.Nome,Selezionato=x.Permessi.Any(p=>p.OperatoreId==r.RecordId)}).ToArrayAsync(ct);
            case ConfigurazioneSection.Postazioni:
                if(!await db.Postazioni.AnyAsync(x=>x.Id==r.RecordId,ct))throw Missing();
                return await db.Settori.OrderBy(x=>x.Nome).Select(x=>new ConfigurazioneOption{
                    Id=x.Id,Nome=x.Nome,Selezionato=x.Reparti.Any(p=>p.PostazioneId==r.RecordId)}).ToArrayAsync(ct);
            case ConfigurazioneSection.Settori:
                if(!await db.Settori.AnyAsync(x=>x.Id==r.RecordId,ct))throw Missing();
                return await db.Tariffe.OrderBy(x=>x.Nome).Select(x=>new ConfigurazioneOption{
                    Id=x.Id,Nome=x.Nome,Selezionato=x.Listini.Any(p=>p.SettoreId==r.RecordId)}).ToArrayAsync(ct);
            default:throw new InvalidOperationException("Questa sezione non gestisce associazioni.");
        }
    }
    private static async Task LinksSave(AppDbContext db,ConfigurazioneRequest r,CancellationToken ct)
    {
        var available=await LinksRead(db,r,ct);
        var ids=(r.SelectedIds??[]).Distinct().ToArray();
        if(ids.Except(available.Select(x=>x.Id)).Any())throw new InvalidOperationException("Una delle associazioni non è più disponibile.");
        switch(r.Section)
        {
            case ConfigurazioneSection.Operatori:
                db.Permessi.RemoveRange(await db.Permessi.Where(x=>x.OperatoreId==r.RecordId&&!ids.Contains(x.PostazioneId)).ToListAsync(ct));
                var old=await db.Permessi.Where(x=>x.OperatoreId==r.RecordId).Select(x=>x.PostazioneId).ToArrayAsync(ct);
                foreach(var id in ids.Except(old))db.Permessi.Add(new Permesso{OperatoreId=r.RecordId,PostazioneId=id});
                break;
            case ConfigurazioneSection.Postazioni:
                db.Reparti.RemoveRange(await db.Reparti.Where(x=>x.PostazioneId==r.RecordId&&!ids.Contains(x.SettoreId)).ToListAsync(ct));
                var sectors=await db.Reparti.Where(x=>x.PostazioneId==r.RecordId).Select(x=>x.SettoreId).ToArrayAsync(ct);
                foreach(var id in ids.Except(sectors))db.Reparti.Add(new Reparto{PostazioneId=r.RecordId,SettoreId=id});
                break;
            case ConfigurazioneSection.Settori:
                db.Listini.RemoveRange(await db.Listini.Where(x=>x.SettoreId==r.RecordId&&!ids.Contains(x.TariffaId)).ToListAsync(ct));
                var prices=await db.Listini.Where(x=>x.SettoreId==r.RecordId).Select(x=>x.TariffaId).ToArrayAsync(ct);
                foreach(var id in ids.Except(prices))db.Listini.Add(new Listino{SettoreId=r.RecordId,TariffaId=id});
                break;
        }
        await SaveChanges(db, ct);
        if(!await db.Operatori.AnyAsync(x=>x.Abilitato&&x.Permessi.Any(p=>p.Postazione!.TipoPostazioneId==1),ct))
            throw new InvalidOperationException("Deve rimanere almeno un amministratore abilitato.");
    }
}


