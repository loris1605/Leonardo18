using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace Leonardo18.DatabaseHost.Strisciate;

/// <summary>Decoded /update query fields from app.js. Card/member numbers remain strings.</summary>
public sealed record StrisciataInput(string CodiceSocio, string NumeroTessera, string Cognome, string Nome, int Natoil, int Scadenza)
{
    public static bool TryParse(IQueryCollection query, out StrisciataInput? input, out string error)
    {
        input = null;
        foreach (var field in new[] { "codice_socio", "numero_tessera", "cognome", "nome", "born_date", "data_scadenza" })
        {
            if (!query.TryGetValue(field, out var values) || values.Count != 1 || string.IsNullOrWhiteSpace(values[0]))
            { error = "Parametro mancante o non valido: " + field + "."; return false; }
        }
        var member = query["codice_socio"].ToString().Trim();
        var card = query["numero_tessera"].ToString().Trim();
        var surname = query["cognome"].ToString().Trim();
        var name = query["nome"].ToString().Trim();
        if (member.Length > 20 || card.Length > 20 || surname.Length > 50 || name.Length > 50)
        { error = "Uno dei campi supera la lunghezza consentita."; return false; }
        if (new[] { member, card, surname, name }.Any(value => value.Any(char.IsControl)))
        { error = "I campi non possono contenere caratteri di controllo."; return false; }
        if (!TryDate(query["born_date"].ToString(), out var birth))
        { error = "born_date deve essere una data valida nel formato yyyyMMdd."; return false; }
        if (!TryDate(query["data_scadenza"].ToString(), out var expiry))
        { error = "data_scadenza deve essere una data valida nel formato yyyyMMdd."; return false; }
        input = new(member, card, surname, name, birth, expiry);
        error = "";
        return true;
    }

    private static bool TryDate(string text, out int value)
    {
        value = 0;
        if (text.Length != 8 || !DateTime.TryParseExact(text, "yyyyMMdd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)) return false;
        value = date.Year * 10000 + date.Month * 100 + date.Day;
        return true;
    }
}
