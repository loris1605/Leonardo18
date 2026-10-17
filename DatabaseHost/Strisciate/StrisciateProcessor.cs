using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Models.Context;
using Models.Tables;

namespace Leonardo18.DatabaseHost.Strisciate;

public sealed class StrisciateConflictException(string message) : Exception(message);

/// <summary>Shared import rules for incoming HTTP records and any pre-existing queued records.</summary>
public static class StrisciateProcessor
{
    public static string UniqueParam(string surname, string name, int birth) =>
        (surname.Trim().PadRight(3)[..3] + name.Trim().PadRight(3)[..3]
            + birth.ToString("D8", CultureInfo.InvariantCulture)).ToUpperInvariant();

    /// <summary>Returns false for an existing card after updating its expiry and the linked person birth date.</summary>
    public static async Task<bool> ApplyAsync(AppDbContext db, Strisciata swipe, CancellationToken ct = default)
    {
        var cardNumber = swipe.NumeroTessera.Trim();
        // Local also covers a card just added by an earlier record in the same batch.
        var existingCard = db.Tessere.Local.FirstOrDefault(t => t.NumeroTessera == cardNumber)
            ?? await db.Tessere.Include(t => t.Socio).ThenInclude(s => s!.Person).SingleOrDefaultAsync(t => t.NumeroTessera == cardNumber, ct);
        if (existingCard is not null)
        {
            if (existingCard.Socio is null)
                await db.Entry(existingCard).Reference(t => t.Socio).LoadAsync(ct);
            var linkedMember = existingCard.Socio
                ?? throw new StrisciateConflictException("La tessera non è collegata a un socio.");
            if (linkedMember.Person is null)
                await db.Entry(linkedMember).Reference(s => s.Person).LoadAsync(ct);
            var linkedPerson = linkedMember.Person
                ?? throw new StrisciateConflictException("Il socio della tessera non è collegato a una persona.");
            if (linkedPerson.Natoil != swipe.Natoil) linkedPerson.Natoil = swipe.Natoil;
            existingCard.Scadenza = swipe.Scadenza;
            return false;
        }

        var memberNumber = swipe.CodiceSocio.Trim();
        if (cardNumber.Length is < 1 or > 20 || memberNumber.Length is < 1 or > 20)
            throw new StrisciateConflictException("Numero socio e numero tessera devono contenere da 1 a 20 caratteri.");

        var key = UniqueParam(swipe.Cognome, swipe.Nome, swipe.Natoil);
        var person = db.People.Local.FirstOrDefault(p => string.Equals(p.UniqueParam, key, StringComparison.OrdinalIgnoreCase))
            ?? await db.People.Include(p => p.Soci).ThenInclude(s => s.Tessere)
                .SingleOrDefaultAsync(p => p.UniqueParam.ToUpper() == key, ct);
        var existingNumber = db.Soci.Local.FirstOrDefault(s => s.NumeroSocio == memberNumber)
            ?? await db.Soci.Include(s => s.Person).SingleOrDefaultAsync(s => s.NumeroSocio == memberNumber, ct);
        if (existingNumber is not null && (person is null
            || (!ReferenceEquals(existingNumber.Person, person)
                && (person.Id == 0 || existingNumber.PersonId != person.Id))))
            throw new StrisciateConflictException("Il numero socio è già associato a una persona con un altro UniqueParam.");

        if (person is null)
        {
            person = new Person { UniqueParam = key, SurName = swipe.Cognome.Trim(), FirstName = swipe.Nome.Trim() };
            db.People.Add(person);
        }
        // Recognition stays strictly based on UniqueParam; never use an existing card to match another person.
        person.Natoil = swipe.Natoil;
        var member = existingNumber ?? person.Soci.FirstOrDefault(s => s.NumeroSocio == memberNumber);
        if (member is null)
        {
            member = new Socio { NumeroSocio = memberNumber, Abilitato = true };
            person.Soci.Add(member);
        }
        member.Tessere.Add(new Tessera { NumeroTessera = cardNumber, Scadenza = swipe.Scadenza, Abilitato = true });
        return true;
    }

    /// <summary>The caller owns the transaction. Remove queued records only after applying them.</summary>
    public static async Task ProcessPendingAsync(AppDbContext db, CancellationToken ct = default)
    {
        var records = await db.Strisciate.OrderBy(s => s.Id).ToListAsync(ct);
        foreach (var record in records)
        {
            await ApplyAsync(db, record, ct);
            db.Strisciate.Remove(record);
        }
        if (records.Count != 0) await db.SaveChangesAsync(ct);
    }
}
