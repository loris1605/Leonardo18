using System.Text.Json.Serialization;
namespace AppServices.Database;
public sealed class DatabaseHostRequest
{
#if DEBUG
    ~DatabaseHostRequest() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public string Operation { get; set; } = "";
    public string ConnectionString { get; set; } = "";
    public int OperatorId { get; set; }
    public string Password { get; set; } = "";
    public SociRequest? Soci { get; set; }
    public ConfigurazioneRequest? Configurazione { get; set; }
    public CassaRequest? Cassa { get; set; }
    public ServiziRequest? Servizi { get; set; }
    public ReportRequest? Report { get; set; }
    public MenuAperturaRequest? AperturaGiornata { get; set; }
}
public sealed class DatabaseHostResponse
{
#if DEBUG
    ~DatabaseHostResponse() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public bool Success { get; set; }
    public int Status { get; set; }
    public string Message { get; set; } = "";
    public string[] Instances { get; set; } = [];
    public string ErrorCode { get; set; } = "";
    public LoginOperator[] Operators { get; set; } = [];
    public LoginSession? Session { get; set; }
    public MenuState? Menu { get; set; }
    public MirrorStatus? Mirror { get; set; }
    public SociResult? Soci { get; set; }
    public ConfigurazioneResult? Configurazione { get; set; }
    public CassaResult? Cassa { get; set; }
    public ServiziResult? Servizi { get; set; }
    public ReportResult? Report { get; set; }
}
[JsonSerializable(typeof(TaglioEuro[]))]
[JsonSerializable(typeof(DatabaseHostRequest))]
[JsonSerializable(typeof(DatabaseHostResponse))]
public partial class DatabaseProtocolJsonContext : JsonSerializerContext {
#if DEBUG
    ~DatabaseProtocolJsonContext() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif
 }
