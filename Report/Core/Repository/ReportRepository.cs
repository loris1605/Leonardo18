using AppServices.Database;
namespace Report.Core.Repository;
public interface IReportRepository { Task<ReportResult> Execute(ReportRequest request,CancellationToken ct=default); }
public sealed class ReportRepository(IDatabaseHostClient client,int operatorId):IReportRepository
{
    public async Task<ReportResult> Execute(ReportRequest request,CancellationToken ct=default)
    {
        var response=await client.SendAsync(new(){Operation="report",ConnectionString=AppServices.Connection.CurrentConnectionString,OperatorId=operatorId,Report=request},ct);
        return response.Report??throw new DatabaseHostException("Risposta report assente.");
    }
}
