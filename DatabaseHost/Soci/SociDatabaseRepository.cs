using System.Data;
using AppServices.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Models.Context;
using Models.Tables;
namespace Leonardo18.DatabaseHost.Soci;

/// <summary>Port of Leonardo17 Soci queries and CRUD; EF executes only in this managed process.</summary>
public sealed class SociDatabaseRepository(Func<AppDbContext> contextFactory)
{
#if DEBUG
    ~SociDatabaseRepository() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public async Task<SociResult> Execute(int operatorId, SociRequest request, CancellationToken ct = default)
    {
        await using var db = contextFactory();
        if (!await db.Operatori.AnyAsync(x => x.Id == operatorId && x.Abilitato, ct)
            || !await db.Permessi.AnyAsync(x => x.OperatoreId == operatorId && x.Postazione!.TipoPostazioneId == 1, ct))
            throw new SociValidationException("Non hai il permesso di gestire i soci.");
        if (request.Action is "load" or "search")
            return await Load(db, request.Search, ct);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var data = request.Record;
        int personId = data.Id;
        try
        {
            switch (request.Action)
            {
                case "person-add":
                    SociValidation.Person(data);
                    await CheckPersonUnique(db, data, 0, ct);
                    data.NumeroSocio = await CheckSocioNumber(db, data.NumeroSocio, 0, ct);
                    data.NumeroTessera = await CheckCardNumber(db, data.NumeroTessera, 0, ct);
                    SociValidation.Expiry(data.Scadenza);
                    var person = new Person { FirstName = data.Nome, SurName = data.Cognome,
                        Natoil = data.Natoil, UniqueParam = data.CodiceUnivoco,
                        Soci = [new Socio { NumeroSocio = data.NumeroSocio, Abilitato = true,
                            Tessere = [new Tessera { NumeroTessera = data.NumeroTessera, Scadenza = data.Scadenza, Abilitato = true }] }] };
                    db.People.Add(person);
                    await db.SaveChangesAsync(ct);
                    personId = person.Id;
                    break;
                case "person-update":
                    var existingPerson = await FindPerson(db, data.Id, ct);
                    SociValidation.Person(data);
                    await CheckPersonUnique(db, data, existingPerson.Id, ct);
                    existingPerson.SurName = data.Cognome; existingPerson.FirstName = data.Nome;
                    existingPerson.Natoil = data.Natoil; existingPerson.UniqueParam = data.CodiceUnivoco;
                    break;
                case "person-block":
                    var blockedPerson=await FindPerson(db,data.Id,ct);
                    SociValidation.Blocco(data);
                    blockedPerson.BloccoSocio=data.BloccoSocio;
                    blockedPerson.BloccoSocioFino=data.BloccoSocioFino?.Date;
                    blockedPerson.BloccoSocioMotivo=data.BloccoSocioMotivo;
                    break;
                case "person-delete":
                    var personToDelete = await FindPerson(db, data.Id, ct);
                    if (await db.Soci.AnyAsync(x => x.PersonId == data.Id, ct))
                        throw new SociValidationException("Rimuovi prima i codici socio associati all'anagrafica.");
                    db.People.Remove(personToDelete);
                    personId = 0;
                    break;
                case "socio-add":
                    await FindPerson(db, data.Id, ct);
                    data.NumeroSocio = await CheckSocioNumber(db, data.NumeroSocio, 0, ct);
                    data.NumeroTessera = await CheckCardNumber(db, data.NumeroTessera, 0, ct);
                    SociValidation.Expiry(data.Scadenza);
                    db.Soci.Add(new Socio { PersonId = data.Id, NumeroSocio = data.NumeroSocio, Abilitato = true,
                        Tessere = [new Tessera { NumeroTessera = data.NumeroTessera, Scadenza = data.Scadenza, Abilitato = true }] });
                    break;
                case "socio-update":
                    var socio = await FindSocio(db, data, ct);
                    socio.NumeroSocio = await CheckSocioNumber(db, data.NumeroSocio, socio.Id, ct);
                    break;
                case "socio-delete":
                    var socioToDelete = await FindSocio(db, data, ct);
                    if (await db.Tessere.AnyAsync(x => x.SocioId == socioToDelete.Id, ct))
                        throw new SociValidationException("Rimuovi prima le tessere associate al codice socio.");
                    db.Soci.Remove(socioToDelete);
                    break;
                case "tessera-add":
                    var parent = await FindSocio(db, data, ct);
                    var number = await CheckCardNumber(db, data.NumeroTessera, 0, ct);
                    SociValidation.Expiry(data.Scadenza);
                    db.Tessere.Add(new Tessera { SocioId = parent.Id, NumeroTessera = number, Scadenza = data.Scadenza, Abilitato = true });
                    break;
                case "tessera-update":
                case "tessera-delete":
                    await FindSocio(db, data, ct);
                    var card = await db.Tessere.SingleOrDefaultAsync(x => x.Id == data.CodiceTessera && x.SocioId == data.CodiceSocio, ct)
                        ?? throw new SociValidationException("Tessera non trovata. Aggiorna l'elenco.");
                    if (request.Action == "tessera-delete") db.Tessere.Remove(card);
                    else
                    {
                        card.NumeroTessera = await CheckCardNumber(db, data.NumeroTessera, card.Id, ct);
                        SociValidation.Expiry(data.Scadenza); card.Scadenza = data.Scadenza;
                    }
                    break;
                default: throw new SociValidationException("Operazione soci non supportata.");
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new SociResult { PersonId = personId };
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && sql.Number is 2601 or 2627)
        { throw new SociValidationException("Anagrafica, codice socio o tessera già in uso. Aggiorna l'elenco e verifica i dati."); }
        catch (DbUpdateException)
        { throw new SociValidationException("Operazione non consentita: controlla i dati e gli eventuali collegamenti con altre registrazioni."); }
    }

    private static async Task<Person> FindPerson(AppDbContext db, int id, CancellationToken ct)
    {
        if (id <= 0) throw new SociValidationException("Seleziona un'anagrafica valida.");
        return await db.People.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new SociValidationException("Anagrafica non trovata. Aggiorna l'elenco.");
    }
    private static async Task<Socio> FindSocio(AppDbContext db, SociRecord data, CancellationToken ct)
    {
        await FindPerson(db, data.Id, ct);
        if (data.CodiceSocio <= 0) throw new SociValidationException("Seleziona un codice socio valido.");
        return await db.Soci.SingleOrDefaultAsync(x => x.Id == data.CodiceSocio && x.PersonId == data.Id, ct)
            ?? throw new SociValidationException("Codice socio non trovato. Aggiorna l'elenco.");
    }
    private static async Task CheckPersonUnique(AppDbContext db, SociRecord data, int except, CancellationToken ct)
    {
        if (await db.People.AnyAsync(x => x.Id != except && x.UniqueParam.ToUpper() == data.CodiceUnivoco, ct))
            throw new SociValidationException("Socio già registrato.");
    }
    private static async Task<string> CheckSocioNumber(AppDbContext db, string value, int except, CancellationToken ct)
    {
        value = SociValidation.Number(value, "Il codice socio");
        if (await db.Soci.AnyAsync(x => x.Id != except && x.NumeroSocio == value, ct))
            throw new SociValidationException("Codice socio già in uso.");
        return value;
    }
    private static async Task<string> CheckCardNumber(AppDbContext db, string value, int except, CancellationToken ct)
    {
        value = SociValidation.Number(value, "Il numero tessera");
        if (await db.Tessere.AnyAsync(x => x.Id != except && x.NumeroTessera == value, ct))
            throw new SociValidationException("Numero tessera già in uso.");
        return value;
    }
    private static async Task<SociResult> Load(AppDbContext db, SociSearch search, CancellationToken ct)
    {
        var people = db.People.AsNoTracking().Where(x => x.Id > 0);
        if (search.PersonId > 0) people = people.Where(x => x.Id == search.PersonId);
        string surname = search.Cognome.Trim(), name = search.Nome.Trim();
        people = search.CognomeMode switch
        {
            1 => people.Where(x => x.SurName == surname),
            2 => people.Where(x => x.SurName.StartsWith(surname)),
            3 => people.Where(x => x.SurName.Contains(surname)), _ => people
        };
        people = search.NomeMode switch
        {
            1 => people.Where(x => x.FirstName == name),
            2 => people.Where(x => x.FirstName.StartsWith(name)),
            3 => people.Where(x => x.FirstName.Contains(name)), _ => people
        };
        if (search.NatoilMode > 0 && SociValidation.ToDate(search.Natoil) is null)
            throw new SociValidationException("Inserisci una data di nascita valida per la ricerca.");
        people = search.NatoilMode switch
        {
            1 => people.Where(x => x.Natoil == search.Natoil),
            2 => people.Where(x => x.Natoil <= search.Natoil),
            3 => people.Where(x => x.Natoil >= search.Natoil), _ => people
        };
        if (!string.IsNullOrWhiteSpace(search.NumeroSocio))
            people = people.Where(x => x.Soci.Any(s => s.NumeroSocio == search.NumeroSocio.Trim()));
        if (!string.IsNullOrWhiteSpace(search.NumeroTessera))
            people = people.Where(x => x.Soci.Any(s => s.Tessere.Any(t => t.NumeroTessera == search.NumeroTessera.Trim())));
        var rows = await people
            .SelectMany(p => p.Soci.DefaultIfEmpty(), (p,s) => new { p,s })
            .SelectMany(x => x.s!.Tessere.DefaultIfEmpty(), (x,t) => new SociRecord {
                BloccoSocio=x.p.BloccoSocio, BloccoSocioFino=x.p.BloccoSocioFino, BloccoSocioMotivo=x.p.BloccoSocioMotivo,
                Id=x.p.Id, Cognome=x.p.SurName, Nome=x.p.FirstName, Natoil=x.p.Natoil, CodiceUnivoco=x.p.UniqueParam,
                CodiceSocio=x.s!=null?x.s.Id:0, NumeroSocio=x.s!=null?x.s.NumeroSocio:"",
                CodiceTessera=t!=null?t.Id:0, NumeroTessera=t!=null?t.NumeroTessera:"", Scadenza=t!=null?t.Scadenza:0 })
            .OrderBy(x => x.Cognome).ThenBy(x => x.Nome).ThenBy(x => x.Id)
            .ThenBy(x => x.CodiceSocio).ThenBy(x => x.CodiceTessera).Take(101).ToArrayAsync(ct);
        return new SociResult { Records = rows.Take(100).ToArray(), LimitReached = rows.Length > 100 };
    }
}