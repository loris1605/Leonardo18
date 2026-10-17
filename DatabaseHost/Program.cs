using Leonardo18.DatabaseHost.Login;
using Microsoft.EntityFrameworkCore;
using Models.Context;
using AppServices.Database;
using Microsoft.Data.SqlClient;
using System.Text.Json;
namespace Leonardo18.DatabaseHost;
internal static class Program
{
    public static async Task<int> Main()
    {
        await using var strisciate = new Strisciate.StrisciateReceiver();
        // Stay alive across navigation. EOF closes the worker when the parent application exits.
        try
        {
            while (await Console.In.ReadLineAsync() is { } input)
            {
                var response = await Execute(input, strisciate);
                await Console.Out.WriteLineAsync(JsonSerializer.Serialize(response, DatabaseProtocolJsonContext.Default.DatabaseHostResponse));
                await Console.Out.FlushAsync();
            }
            return 0;
        }
        catch (IOException) { return 1; }
    }

    private static async Task<DatabaseHostResponse> Execute(string input, Strisciate.StrisciateReceiver strisciate)
    {
        DatabaseHostResponse response;
        bool applyingMigrations = false;
        try
        {
            var request = JsonSerializer.Deserialize(input, DatabaseProtocolJsonContext.Default.DatabaseHostRequest);
            if (request is null) throw new InvalidOperationException("Richiesta assente.");
            applyingMigrations = request.Operation == "migrate";
            var service = new ConnectionService();
            if (request.Operation == "ping")
                response = new() { Success = true, Message = "DatabaseHost pronto." };
            else if (request.Operation == "discover")
                response = new() { Success = true, Instances = (await service.GetInstancesAsync()).ToArray() };
            else if (request.Operation == "cassa")
            {
                var session = new Cassa.CassaSessionService(strisciate, connectionString => new AppDbContext(
                    new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options));
                response = new() { Success = true, Cassa = await session.ExecuteAsync(request) };
            }
            else if (request.Operation == "report")
            {
                var repository=new Report.ReportDatabaseRepository(()=>new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(request.ConnectionString).Options));
                response=new(){Success=true,Report=await repository.Execute(request.OperatorId,request.Report??throw new InvalidOperationException("Richiesta report assente."))};
            }
            else if (request.Operation == "servizi")
            {
                var repository = new Servizi.ServiziDatabaseRepository(() => new AppDbContext(
                    new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(request.ConnectionString).Options));
                response = new() { Success = true, Servizi = await repository.Execute(request.OperatorId,
                    request.Servizi ?? throw new InvalidOperationException("Richiesta servizi assente.")) };
            }
            else if (request.Operation == "configurazione")
            {
                var repository = new Configurazione.ConfigurazioneDatabaseRepository(() => new AppDbContext(
                    new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(request.ConnectionString).Options));
                response = new() { Success = true, Configurazione = await repository.Execute(request.OperatorId,
                    request.Configurazione ?? throw new InvalidOperationException("Richiesta configurazione assente.")) };
            }
            else if (request.Operation == "soci")
            {
                var repository = new Soci.SociDatabaseRepository(() => new AppDbContext(
                    new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(request.ConnectionString).Options));
                response = new() { Success = true, Soci = await repository.Execute(request.OperatorId,
                    request.Soci ?? throw new SociValidationException("Richiesta soci assente.")) };
            }
            else if (request.Operation is "menu-load" or "menu-open" or "menu-close")
            {
                var repository = new Menu.MenuDatabaseRepository(() => new AppDbContext(
                    new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(request.ConnectionString).Options));
                response = new() { Success = true, Menu = await repository.Execute(request.OperatorId, request.Operation) };
            }
            else if (request.Operation is "login-operators" or "login-authenticate")
            {
                var repository = new LoginDatabaseRepository(() => new AppDbContext(
                    new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(request.ConnectionString).Options));
                if (request.Operation == "login-operators")
                    response = new() { Success = true, Operators = await repository.GetOperatoriAbilitati() };
                else
                    response = new() { Success = true, Session = await repository.Authenticate(request.OperatorId, request.Password) };
            }
            else
            {
                var result = request.Operation switch
                {
                    "check" => await service.CheckAsync(request.ConnectionString),
                    "migrate" => await service.ApplyMigrationsAsync(request.ConnectionString),
                    _ => throw new InvalidOperationException("Operazione non supportata.")
                };
                response = new() { Success = true, Status = (int)result.Status, Message = result.Message };
            }
        }
        catch (Strisciate.StrisciateConflictException ex)
        {
            response = new() { Message = ex.Message, ErrorCode = "strisciate_conflict" };
        }
        catch (SociValidationException ex)
        {
            response = new() { Message = ex.Message, ErrorCode = "soci_validation" };
        }
        catch (LoginAuthenticationException ex)
        {
            response = new() { Message = ex.Message, ErrorCode = "invalid_credentials" };
        }
        catch (InvalidOperationException ex)
        {
            response = new() { Message = ex.Message };
        }
        catch (SqlException ex)
        {
            response = new() { Message = SqlErrorMessages.For(ex.Number, applyingMigrations), ErrorCode = $"sql_{ex.Number}" };
        }
        catch (Exception)
        {
            response = new() { Message = "Operazione database non riuscita. Controlla configurazione e migrazioni." };
        }
        return response;
    }
}
