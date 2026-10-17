using AppServices.Database;
using Login.Core.DTO;
using Models.Entity.Global;
namespace Login.Core.Repository;
public interface ILoginRepository
{
    Task<List<LoginDTO>> GetOperatoriAbilitati(CancellationToken ct = default);
    Task SaveSettings(LoginDTO dto, string password, CancellationToken ct = default);
}
public sealed class LoginRepository : ILoginRepository
{
#if DEBUG
    ~LoginRepository() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    private readonly IDatabaseHostClient _client;
    private readonly Func<string> _readConnection;
    public LoginRepository(IDatabaseHostClient? client = null, Func<string>? readConnection = null)
    {
        _client = client ?? new DatabaseHostClient();
        _readConnection = readConnection ?? (() => AppServices.Connection.CurrentConnectionString);
    }
    public async Task<List<LoginDTO>> GetOperatoriAbilitati(CancellationToken ct = default)
    {
        var response = await _client.SendAsync(new() { Operation = "login-operators", ConnectionString = _readConnection() }, ct).ConfigureAwait(false);
        return response.Operators.Select(x => new LoginDTO { Id = x.Id, NomeOperatore = x.Name }).ToList();
    }
    public async Task SaveSettings(LoginDTO dto, string password, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        DatabaseHostResponse response;
        try
        {
            response = await _client.SendAsync(new()
            {
                Operation = "login-authenticate", ConnectionString = _readConnection(), OperatorId = dto.Id, Password = password
            }, ct).ConfigureAwait(false);
        }
        catch (DatabaseHostException ex) when (ex.ErrorCode == "invalid_credentials")
        { throw new InvalidOperationException(ex.Message); }
        var session = response.Session;
        if (session is null || session.OperatorId != dto.Id) throw new DatabaseHostException("Sessione operatore non valida.");
        ct.ThrowIfCancellationRequested();
        GlobalValuesC.MySetting = new OperatoreXC
        {
            IDOPERATORE = session.OperatorId, NOMEOPERATORE = session.OperatorName,
            POSTAZIONI = session.Stations.Select(station => new PostazioneXC
            {
                CODICEPOSTAZIONE = station.Id, DESCPOSTAZIONE = station.Name, TIPOPOSTAZIONE = station.TypeId,
                TIPORIENTRO = station.ReturnTypeId, ORERIENTRO = station.ReturnHours,
                SETTORI = station.Sectors.Select(sector => new SettoreXC
                {
                    CODICESETTORE = sector.Id, NOMESETTORE = sector.Name, DESCSETTORE = sector.Label,
                    TARIFFE = sector.Rates.Select(rate => new TariffaXC
                    { CODICETARIFFA = rate.Id, DESCTARIFFA = rate.Label, PRICETARIFFA = rate.Price }).ToList()
                }).ToList()
            }).ToList(),
            GIORNATA = session.Day is { } day ? new GiornataXC
            { IDGIORNATA = day.Id, DATAINIZIO = day.Start, DATAFINE = day.End, APERTA = day.Open } : null
        };
    }
}