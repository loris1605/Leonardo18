using AppServices;
using Microsoft.EntityFrameworkCore;

namespace Models.Context;

public class BaseContext : DbContext
{
#if DEBUG
    ~BaseContext() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public BaseContext() { }
    protected BaseContext(DbContextOptions options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (options.IsConfigured) return;
        string connectionString = Connection.CurrentConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("La connessione al database non è configurata.");
        options.UseSqlServer(connectionString);
    }
}