namespace AppServices.Database;

public static class CassaAnnullamentoIngresso
{
    public const string AutorizzazioneRichiesta = "Sono trascorsi i 10 minuti consentiti per annullare l'ingresso. Serve l'autorizzazione di un operatore Amministratore; la procedura di autorizzazione non è ancora disponibile.";

    public static bool Consentito(DateTime inseritoIl, DateTime adesso) =>
        adesso >= inseritoIl && adesso - inseritoIl < TimeSpan.FromMinutes(10);
}
