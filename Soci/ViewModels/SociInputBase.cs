using AppServices.Database;
using ReactiveUI;
using Soci.Core.DTO;
namespace Soci.ViewModels;
public abstract class SociInputBase : ReactiveObject
{
#if DEBUG
    ~SociInputBase() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    private readonly SociRecord _original;
    private string _cognome, _nome, _numeroSocio, _numeroTessera;
    private DateTimeOffset? _born, _expiry;
    public string Action { get; }
    public string Titolo { get; }
    public string SaveText => IsDelete ? "CONFERMA ELIMINAZIONE" : "SALVA";
    public bool IsDelete => Action.EndsWith("-delete", StringComparison.Ordinal);
    public bool FieldsEnabled => !IsDelete;
    public virtual bool PersonFields => false;
    public virtual bool SocioFields => false;
    public virtual bool CardFields => false;
    public string ContextText => $"{_original.Cognome} {_original.Nome}".Trim();
    public string DeleteText => IsDelete ? "Confermi l'eliminazione di questa registrazione?" : "";
    public string Cognome { get => _cognome; set => this.RaiseAndSetIfChanged(ref _cognome,value); }
    public string Nome { get => _nome; set => this.RaiseAndSetIfChanged(ref _nome,value); }
    public string NumeroSocio { get => _numeroSocio; set => this.RaiseAndSetIfChanged(ref _numeroSocio,value); }
    public string NumeroTessera { get => _numeroTessera; set => this.RaiseAndSetIfChanged(ref _numeroTessera,value); }
    public DateTimeOffset? DataNascitaOffSet { get => _born; set => this.RaiseAndSetIfChanged(ref _born,value); }
    public DateTimeOffset? ScadenzaOffSet { get => _expiry; set => this.RaiseAndSetIfChanged(ref _expiry,value); }
    protected SociInputBase(string action, string title, SociRecord data)
    {
        Action=action; Titolo=title; _original=data;
        _cognome=data.Cognome; _nome=data.Nome; _numeroSocio=data.NumeroSocio; _numeroTessera=data.NumeroTessera;
        _born=ToOffset(data.Natoil); _expiry=ToOffset(data.Scadenza);
        if(action=="socio-add") { _numeroSocio=""; _numeroTessera=""; _expiry=null; }
        if(action=="tessera-add") { _numeroTessera=""; _expiry=null; }
    }
    private static DateTimeOffset? ToOffset(int date) =>
        SociValidation.ToDate(date) is { } value ? new DateTimeOffset(value) : null;
    public SociPersonDTO ToDto() => new() {
        Id=_original.Id, CodiceSocio=_original.CodiceSocio, CodiceTessera=_original.CodiceTessera,
        Cognome=Cognome, Nome=Nome, Natoil=DataNascitaOffSet is { } born ? SociValidation.ToDateInt(born.Date):0,
        CodiceUnivoco=_original.CodiceUnivoco, NumeroSocio=NumeroSocio, NumeroTessera=NumeroTessera,
        Scadenza=ScadenzaOffSet is { } expiry ? SociValidation.ToDateInt(expiry.Date):0 };
    public void Validate()
    {
        if(IsDelete)return;
        var data=ToDto();
        if(PersonFields) SociValidation.Person(data);
        if(SocioFields) SociValidation.Number(data.NumeroSocio,"Il codice socio");
        if(CardFields) { SociValidation.Number(data.NumeroTessera,"Il numero tessera"); SociValidation.Expiry(data.Scadenza); }
    }
    public string FocusName => IsDelete ? "CancelButton" : PersonFields ? "CognomeInput" : SocioFields ? "NumeroSocioInput" : "NumeroTesseraInput";
}