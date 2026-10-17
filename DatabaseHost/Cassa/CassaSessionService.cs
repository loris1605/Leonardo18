using AppServices.Database;
using Leonardo18.DatabaseHost.Strisciate;
using Models.Context;

namespace Leonardo18.DatabaseHost.Cassa;

/// <summary>Starts reception only after the station/day permissions have been checked.</summary>
public sealed class CassaSessionService(StrisciateReceiver receiver, Func<string, AppDbContext> contextFactory)
{
    public async Task<CassaResult> ExecuteAsync(DatabaseHostRequest request, CancellationToken ct = default)
    {
        var payload = request.Cassa ?? throw new InvalidOperationException("Richiesta cassa assente.");
        if (!Guid.TryParse(payload.StrisciateSessionId, out _))
            throw new InvalidOperationException("Sessione di cassa non valida.");
        if (payload.Action == CassaAction.StopStrisciate)
        {
            await receiver.StopAsync(payload.StrisciateSessionId, ct);
            return new CassaResult();
        }
        var repository = new CassaDatabaseRepository(() => contextFactory(request.ConnectionString));
        var result = await repository.Execute(request.OperatorId, payload, ct);
        if (payload.Action == CassaAction.Load)
        {
            var sink = new StrisciateDatabaseSink(() => contextFactory(request.ConnectionString), request.OperatorId, payload.PostazioneId);
            result.StrisciateEndpoint = await receiver.StartAsync(payload.StrisciateSessionId, sink, ct);
        }
        return result;
    }
}
