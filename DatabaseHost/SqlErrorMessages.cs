namespace Leonardo18.DatabaseHost;
public static class SqlErrorMessages
{
    public static string For(int number, bool applyingMigrations) => (number switch
    {
        233 => "SQL Server ha interrotto il collegamento durante l'accesso. Verifica autenticazione e disponibilità del database.",
        18456 => "Accesso SQL rifiutato. Verifica nome utente e password.",
        262 or 15247 => "L'utente SQL non ha i permessi necessari per creare il database.",
        229 => "L'utente SQL non ha i permessi necessari per questa operazione.",
        4060 when applyingMigrations => "Impossibile aprire il database durante la creazione o l'aggiornamento. Verifica che sia disponibile e accessibile all'utente SQL.",
        4060 => "Il database non è accessibile all'utente SQL.",
        1801 => "Un database con questo nome esiste già. Ripeti la verifica della connessione.",
        5120 or 5133 or 5170 => "SQL Server non riesce a creare o aprire i file del database. Verifica il percorso e i permessi del servizio SQL Server.",
        -2 => "Tempo di attesa SQL Server scaduto. Ripeti l'operazione.",
        _ when applyingMigrations => "Creazione o aggiornamento del database non riuscito.",
        _ => "Impossibile accedere al database. Controlla server, rete e autorizzazioni."
    }) + $" (SQL {number})";
}