using AppServices.Database;
namespace Servizi.Core.Repository;
public interface IServiziRepository { Task<ServiziResult> Execute(ServiziRequest request,CancellationToken ct=default); }
public sealed class ServiziRepository(IDatabaseHostClient client,int operatorId) : IServiziRepository
{
    public async Task<ServiziResult> Execute(ServiziRequest request,CancellationToken ct=default)
    {
        var response=await client.SendAsync(new() {Operation="servizi",ConnectionString=AppServices.Connection.CurrentConnectionString,OperatorId=operatorId,Servizi=request},ct);
        return response.Servizi ?? throw new DatabaseHostException("Risposta servizi assente.");
    }
}