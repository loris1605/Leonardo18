namespace AppServices.Database;
public interface IMirrorClient
{
 Task<MirrorStatus> ExecuteAsync(string operation,int operatorId,CancellationToken ct=default);
}
public sealed class MirrorClient(IDatabaseHostClient? client=null):IMirrorClient
{
 private readonly IDatabaseHostClient _client=client??new DatabaseHostClient();
 public async Task<MirrorStatus> ExecuteAsync(string operation,int operatorId,CancellationToken ct=default)
 {
  if(operation is not ("mirror-status" or "mirror-connect" or "mirror-sync"))throw new ArgumentException("Operazione mirror non valida.",nameof(operation));
  return (await _client.SendAsync(new(){Operation=operation,ConnectionString=AppServices.Connection.CurrentConnectionString,OperatorId=operatorId},ct)).Mirror??throw new DatabaseHostException("Stato mirror non disponibile.");
 }
}
