using AppServices.Database;
namespace Menu.Core.Repository;
public interface IMenuRepository
{
    Task<MenuState> Execute(int operatorId, string operation, CancellationToken ct = default);
    Task<MenuState> OpenDay(int operatorId, MenuAperturaRequest request, CancellationToken ct = default)
        => throw new NotSupportedException("Apertura con cassa iniziale non disponibile.");
}
public sealed class MenuRepository(IDatabaseHostClient? client = null) : IMenuRepository
{
#if DEBUG
    ~MenuRepository() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    private readonly IDatabaseHostClient _client = client ?? new DatabaseHostClient();
    public Task<MenuState> Execute(int operatorId, string operation, CancellationToken ct = default) => Send(operatorId,operation,null,ct);
    public Task<MenuState> OpenDay(int operatorId, MenuAperturaRequest request, CancellationToken ct = default) => Send(operatorId,"menu-open",request,ct);
    private async Task<MenuState> Send(int operatorId, string operation, MenuAperturaRequest? opening, CancellationToken ct)
    {
        var response = await _client.SendAsync(new DatabaseHostRequest {
            Operation = operation, OperatorId = operatorId, AperturaGiornata=opening,
            ConnectionString = AppServices.Connection.CurrentConnectionString }, ct);
        if (!response.Success || response.Menu is null)
            throw new DatabaseHostException(string.IsNullOrEmpty(response.Message) ? "Caricamento menu non riuscito." : response.Message);
        return response.Menu;
    }
}