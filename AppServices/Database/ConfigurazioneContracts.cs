namespace AppServices.Database;

// Fields and relations carried over from Leonardo17's configuration module.
public enum ConfigurazioneSection { Operatori, Postazioni, Settori, Tariffe, Rientri }
public enum ConfigurazioneAction { Load, Read, Add, Update, Delete, LinksRead, LinksSave }
public sealed class ConfigurazioneRecord
{
#if DEBUG
    ~ConfigurazioneRecord() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string Etichetta { get; set; } = "";
    public string Password { get; set; } = "";
    public bool Abilitato { get; set; } = true;
    public int Badge { get; set; }
    public int TipoId { get; set; }
    public int RientroId { get; set; }
    public decimal Prezzo { get; set; }
    public bool FreeDrink { get; set; }
    public int DurataOre { get; set; }
    public string Dettagli { get; set; } = "";
    public string[] Relazioni { get; set; } = [];
}
public sealed class ConfigurazioneOption
{
#if DEBUG
    ~ConfigurazioneOption() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public bool Selezionato { get; set; }
}
public sealed class ConfigurazioneRequest
{
#if DEBUG
    ~ConfigurazioneRequest() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif
    public ConfigurazioneSection Section { get; set; }
    public ConfigurazioneAction Action { get; set; }
    public int RecordId { get; set; }
    public ConfigurazioneRecord? Record { get; set; }
    public int[] SelectedIds { get; set; } = [];
}
public sealed class ConfigurazioneResult
{
#if DEBUG
    ~ConfigurazioneResult() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif
    public int RecordId { get; set; }
    public ConfigurazioneRecord[] Records { get; set; } = [];
    public ConfigurazioneOption[] Tipi { get; set; } = [];
    public ConfigurazioneOption[] Rientri { get; set; } = [];
    public ConfigurazioneOption[] Scelte { get; set; } = [];
}
