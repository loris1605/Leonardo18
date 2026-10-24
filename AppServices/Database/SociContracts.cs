namespace AppServices.Database;
public class SociRecord
{
#if DEBUG
    ~SociRecord() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public bool BloccoSocio { get; set; }
        public DateTime? BloccoSocioFino { get; set; }
        public string BloccoSocioMotivo { get; set; } = "";
    public int Id { get; set; }
    public string Cognome { get; set; } = "";
    public string Nome { get; set; } = "";
    public int Natoil { get; set; }
    public string CodiceUnivoco { get; set; } = "";
    public int CodiceSocio { get; set; }
    public string NumeroSocio { get; set; } = "";
    public int CodiceTessera { get; set; }
    public string NumeroTessera { get; set; } = "";
    public int Scadenza { get; set; }
}
public sealed class SociSearch
{
#if DEBUG
    ~SociSearch() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public int PersonId { get; set; }
    public string Cognome { get; set; } = "";
    public string Nome { get; set; } = "";
    public int CognomeMode { get; set; }
    public int NomeMode { get; set; }
    public int Natoil { get; set; }
    public int NatoilMode { get; set; }
    public string NumeroSocio { get; set; } = "";
    public string NumeroTessera { get; set; } = "";
}
public sealed class SociRequest
{
#if DEBUG
    ~SociRequest() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public string Action { get; set; } = "load";
    public SociRecord Record { get; set; } = new();
    public SociSearch Search { get; set; } = new();
}
public sealed class SociResult
{
#if DEBUG
    ~SociResult() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public SociRecord[] Records { get; set; } = [];
    public int PersonId { get; set; }
    public bool LimitReached { get; set; }
}