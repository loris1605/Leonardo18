using AppServices;
using Microsoft.EntityFrameworkCore;

namespace Models.Context
{
    public abstract class ContextBase<TContext> : DbContext
        where TContext : DbContext
    {
#if DEBUG
    ~ContextBase() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        protected ContextBase() { }

        protected ContextBase(DbContextOptions<TContext> options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            // Se le options sono già configurate (es. tramite DI), non sovrascriviamo nulla.
            if (options.IsConfigured) return;

            // Aggiunto controllo e messaggio chiaro se la connessione non è impostata.
            var conn = Connection.CurrentConnectionString;
            if (string.IsNullOrWhiteSpace(conn))
                throw new InvalidOperationException("Connection.CurrentConnectionString non è impostata.");

            // Aggiunto commento: puoi estendere qui le opzioni (retry, command timeout, ecc.).
            options.UseSqlServer(conn);
        }
    }
}
