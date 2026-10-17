using System.Globalization;
namespace AppServices.Database;
public sealed class SociValidationException(string message) : Exception(message){
#if DEBUG
    ~SociValidationException() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif
}
public static class SociValidation
{
    public static int ToDateInt(DateTime date) => date.Year * 10000 + date.Month * 100 + date.Day;
    public static DateTime? ToDate(int value)
    {
        try { return value == 0 ? null : new DateTime(value / 10000, value / 100 % 100, value % 100); }
        catch (ArgumentOutOfRangeException) { return null; }
    }
    public static string DateText(int value) => ToDate(value)?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "";
    public static void Person(SociRecord data)
    {
        data.Cognome = data.Cognome.Trim(); data.Nome = data.Nome.Trim();
        if (data.Cognome.Length is < 2 or > 50 || data.Nome.Length is < 2 or > 50)
            throw new SociValidationException("Nome e cognome devono contenere da 2 a 50 caratteri.");
        var born = ToDate(data.Natoil);
        if (born is null || born.Value.Year < 1900 || born > DateTime.Today.AddYears(-18))
            throw new SociValidationException("Inserisci una data di nascita valida: il socio deve essere maggiorenne.");
        data.CodiceUnivoco = (data.Cognome.PadRight(3)[..3] + data.Nome.PadRight(3)[..3]
            + data.Natoil.ToString(CultureInfo.InvariantCulture)).ToUpperInvariant();
    }
    public static string Number(string value, string label)
    {
        value = value.Trim();
        if (value.Length > 20 || !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int number) || number <= 0)
            throw new SociValidationException($"{label} deve essere un numero maggiore di zero.");
        return value;
    }
    public static void Expiry(int value)
    {
        if (value != 0 && ToDate(value) is null)
            throw new SociValidationException("Data di scadenza non valida.");
    }
}