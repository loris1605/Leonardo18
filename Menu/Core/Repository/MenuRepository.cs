using AppServices.Database;
namespace Menu.Core.Repository;
public interface IMenuRepository
{
    Task<MenuState> Execute(int operatorId, string operation, CancellationToken ct = default);
}
public sealed class MenuRepository(IDatabaseHostClient? client = null) : IMenuRepository
{
#if DEBUG
    ~MenuRepository() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    private readonly IDatabaseHostClient _client = client ?? new DatabaseHostClient();
    public async Task<MenuState> Execute(int operatorId, string operation, CancellationToken ct = default)
    {
        var response = await _client.SendAsync(new DatabaseHostRequest {
            Operation = operation, OperatorId = operatorId,
            ConnectionString = AppServices.Connection.CurrentConnectionString }, ct);
        if (!response.Success || response.Menu is null)
            throw new DatabaseHostException(string.IsNullOrEmpty(response.Message) ? "Caricamento menu non riuscito." : response.Message);
        return response.Menu;
    }
}