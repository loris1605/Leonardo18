using AppServices;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Models.Context;

namespace Leonardo18.DatabaseHost;

public enum ConnectionStatus { Ready, DatabaseMissing, UpdatesRequired, IncompatibleDatabase }
public sealed record ConnectionCheckResult(ConnectionStatus Status, string Message){
#if DEBUG
    ~ConnectionCheckResult() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif
}

public interface IConnectionService
{
    Task<IReadOnlyList<string>> GetInstancesAsync(CancellationToken ct = default);
    Task<ConnectionCheckResult> CheckAsync(string connectionString, CancellationToken ct = default);
    Task<ConnectionCheckResult> ApplyMigrationsAsync(string connectionString, CancellationToken ct = default);
}

public sealed class ConnectionService : IConnectionService
{
#if DEBUG
    ~ConnectionService() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public async Task<IReadOnlyList<string>> GetInstancesAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows()) return [];
        return await Task.Run(() => (IReadOnlyList<string>)SqlInstanceFinder.GetInstances()
            .Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList(), ct).ConfigureAwait(false);
    }

    public async Task<ConnectionCheckResult> CheckAsync(string connectionString, CancellationToken ct = default)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource) || string.IsNullOrWhiteSpace(builder.InitialCatalog))
            throw new ArgumentException("Specificare server e database.", nameof(connectionString));

        await using var sql = new SqlConnection(connectionString);
        try { await sql.OpenAsync(ct).ConfigureAwait(false); }
        catch (SqlException ex) when (ex.Errors.Cast<SqlError>().Any(error => error.Number is 4060 or 233 or 18456))
        {
            // Some local SQL providers return 233 instead of 4060 for a missing catalog.
            // Authenticate separately on master before deciding that creation is allowed.
            builder.InitialCatalog = "master";
            await using var master = new SqlConnection(builder.ConnectionString);
            await master.OpenAsync(ct).ConfigureAwait(false);
            await using var command = new SqlCommand("SELECT COUNT(*) FROM sys.databases WHERE name = @name", master);
            command.Parameters.AddWithValue("@name", new SqlConnectionStringBuilder(connectionString).InitialCatalog);
            if (Convert.ToInt32(await command.ExecuteScalarAsync(ct).ConfigureAwait(false)) == 0)
                return new(ConnectionStatus.DatabaseMissing, "Database non trovato o non visibile. F5: prova a crearlo.");
            throw; // Never offer creation when the catalog exists but its login fails.
        }

        await using var db = CreateContext(connectionString);
        var known = db.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
        if (known.Count == 0)
            return new(ConnectionStatus.IncompatibleDatabase, "Non sono disponibili migrazioni per Leonardo18.");
        var applied = (await db.Database.GetAppliedMigrationsAsync(ct).ConfigureAwait(false)).ToList();
        if (applied.Any(migration => !known.Contains(migration)))
            return new(ConnectionStatus.IncompatibleDatabase, "Il database ha migrazioni non compatibili con questa versione di Leonardo18.");
        var pending = (await db.Database.GetPendingMigrationsAsync(ct).ConfigureAwait(false)).ToList();
        if (pending.Count > 0)
        {
            if (applied.Count == 0)
            {
                await using var command = new SqlCommand("SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0 AND name <> '__EFMigrationsHistory'", sql);
                if (Convert.ToInt32(await command.ExecuteScalarAsync(ct).ConfigureAwait(false)) > 0)
                    return new(ConnectionStatus.IncompatibleDatabase, "Il database contiene tabelle ma non ha le migrazioni di Leonardo18.");
            }
            return new(ConnectionStatus.UpdatesRequired, "Database da aggiornare. F5: applica le migrazioni.");
        }

        await using var versionCommand = new SqlCommand("SELECT [Version] FROM [Settings] WHERE [Id] = -1", sql);
        object? version = await versionCommand.ExecuteScalarAsync(ct).ConfigureAwait(false);
        if (version is null or DBNull || Convert.ToInt32(version) != 1)
            return new(ConnectionStatus.IncompatibleDatabase, "La versione del database non è compatibile con Leonardo18.");
        return new(ConnectionStatus.Ready, "Connessione verificata.");
    }

    public async Task<ConnectionCheckResult> ApplyMigrationsAsync(string connectionString, CancellationToken ct = default)
    {
        var before = await CheckAsync(connectionString, ct).ConfigureAwait(false);
        if (before.Status == ConnectionStatus.Ready) return before;
        if (before.Status == ConnectionStatus.IncompatibleDatabase)
            return before;

        await using var db = CreateContext(connectionString);
        if (before.Status == ConnectionStatus.DatabaseMissing)
        {
            // Create via master, avoiding a second login to an absent catalog in MigrateAsync.
            var creator = db.GetService<IRelationalDatabaseCreator>();
            try { await creator.CreateAsync(ct).ConfigureAwait(false); }
            catch (SqlException ex) when (ex.Number == 1801)
            {
                // Another client may have created it since verification.
                var current = await CheckAsync(connectionString, ct).ConfigureAwait(false);
                if (current.Status != ConnectionStatus.UpdatesRequired) return current;
            }
        }
        await db.Database.MigrateAsync(ct).ConfigureAwait(false);
        return await CheckAsync(connectionString, ct).ConfigureAwait(false);
    }

    private static AppDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString,
            sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name)).Options;
        return new AppDbContext(options);
    }
}