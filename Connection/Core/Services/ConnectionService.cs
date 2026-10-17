using AppServices.Database;
namespace Connection.Core.Services;
public enum ConnectionStatus { Ready, DatabaseMissing, UpdatesRequired, IncompatibleDatabase }
public sealed record ConnectionCheckResult(ConnectionStatus Status, string Message){
#if DEBUG
    ~ConnectionCheckResult() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif
}
public sealed class DatabaseOperationException(string message) : Exception(message){
#if DEBUG
    ~DatabaseOperationException() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
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

    private readonly IDatabaseHostClient _client;
    public ConnectionService(string? hostPath = null, IDatabaseHostClient? client = null) => _client = client ?? new DatabaseHostClient(hostPath);
    public async Task<IReadOnlyList<string>> GetInstancesAsync(CancellationToken ct = default) =>
        (await SendAsync("discover", "", ct).ConfigureAwait(false)).Instances;
    public async Task<ConnectionCheckResult> CheckAsync(string connectionString, CancellationToken ct = default) =>
        ToResult(await SendAsync("check", connectionString, ct).ConfigureAwait(false));
    public async Task<ConnectionCheckResult> ApplyMigrationsAsync(string connectionString, CancellationToken ct = default) =>
        ToResult(await SendAsync("migrate", connectionString, ct).ConfigureAwait(false));
    private static ConnectionCheckResult ToResult(DatabaseHostResponse response)
    {
        if (response.Status < 0 || response.Status > (int)ConnectionStatus.IncompatibleDatabase)
            throw new DatabaseOperationException("Risposta database non valida.");
        return new((ConnectionStatus)response.Status, response.Message);
    }
    private async Task<DatabaseHostResponse> SendAsync(string operation, string connectionString, CancellationToken ct)
    {
        try { return await _client.SendAsync(new() { Operation = operation, ConnectionString = connectionString }, ct).ConfigureAwait(false); }
        catch (DatabaseHostException ex) { throw new DatabaseOperationException(ex.Message); }
    }
}
