using AppServices.Database;
using Models.Entity.Global;
namespace Soci.Core.Repository;
public interface ISociPersonRepository
{
    Task<SociResult> Execute(SociRequest request, CancellationToken ct = default);
}
public sealed class SociPersonRepository(IDatabaseHostClient? client = null,
    Func<string>? readConnection = null, Func<int>? readOperator = null) : ISociPersonRepository
{
#if DEBUG
    ~SociPersonRepository() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    private readonly IDatabaseHostClient _client = client ?? new DatabaseHostClient();
    public async Task<SociResult> Execute(SociRequest request, CancellationToken ct = default)
    {
        var response = await _client.SendAsync(new DatabaseHostRequest {
            Operation = "soci", ConnectionString = readConnection?.Invoke() ?? AppServices.Connection.CurrentConnectionString,
            OperatorId = readOperator?.Invoke() ?? GlobalValuesC.MySetting?.IDOPERATORE ?? 0, Soci = request }, ct);
        if (!response.Success || response.Soci is null)
            throw new DatabaseHostException(string.IsNullOrWhiteSpace(response.Message) ? "Operazione soci non riuscita." : response.Message, response.ErrorCode);
        return response.Soci;
    }
}