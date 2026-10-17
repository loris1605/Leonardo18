using AppServices.Database;
namespace Cassa.Core.Repository;

public interface ICassaRepository
{
    Task<CassaResult> Execute(CassaRequest request, CancellationToken ct = default);
}

/// <summary>The desktop exchanges DTOs with the managed database host, without EF or reflective mapping.</summary>
public sealed class CassaRepository(IDatabaseHostClient client, int operatorId) : ICassaRepository
{
    private readonly string _strisciateSessionId = Guid.NewGuid().ToString("N");
    public async Task<CassaResult> Execute(CassaRequest request, CancellationToken ct = default)
    {
        request.StrisciateSessionId = _strisciateSessionId;
        var response = await client.SendAsync(new DatabaseHostRequest
        {
            Operation = "cassa", ConnectionString = AppServices.Connection.CurrentConnectionString,
            OperatorId = operatorId, Cassa = request
        }, ct);
        return response.Cassa ?? throw new DatabaseHostException("Risposta cassa assente.");
    }
}
