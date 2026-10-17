using AppServices.Database;
using Models.Entity.Global;
namespace Configurazione.Core.Repository;
public interface IConfigurazioneRepository
{
    Task<ConfigurazioneResult> Execute(ConfigurazioneRequest request,CancellationToken ct=default);
}
public sealed class ConfigurazioneRepository(IDatabaseHostClient client) : IConfigurazioneRepository
{
#if DEBUG
    ~ConfigurazioneRepository() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif
    public async Task<ConfigurazioneResult> Execute(ConfigurazioneRequest request,CancellationToken ct=default)
    {
        var response=await client.SendAsync(new DatabaseHostRequest {
            Operation="configurazione",ConnectionString=AppServices.Connection.CurrentConnectionString,
            OperatorId=GlobalValuesC.MySetting?.IDOPERATORE??0,Configurazione=request },ct);
        if(!response.Success||response.Configurazione is null)
            throw new DatabaseHostException(string.IsNullOrWhiteSpace(response.Message)?"Operazione configurazione non riuscita.":response.Message,response.ErrorCode);
        return response.Configurazione;
    }
}
