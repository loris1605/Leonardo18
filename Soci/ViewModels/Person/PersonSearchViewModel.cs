using AppServices.Database;
using ReactiveUI;
namespace Soci.ViewModels;
public sealed class PersonSearchViewModel : ReactiveObject
{
#if DEBUG
    ~PersonSearchViewModel() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    private string _kind="Anagrafica", _cognome="", _nome="", _socio="", _tessera="";
    private int _cognomeMode, _nomeMode, _bornMode;
    private DateTimeOffset? _born;
    public string[] SearchKinds { get; } = ["Anagrafica","Codice socio","Tessera"];
    public string[] TextModes { get; } = ["Ignora","Uguale a","Inizia con","Contiene"];
    public string[] DateModes { get; } = ["Ignora","Uguale a","Prima del / uguale","Dopo il / uguale"];
    public string SearchKind {
        get=>_kind;
        set { this.RaiseAndSetIfChanged(ref _kind,value); this.RaisePropertyChanged(nameof(AnagraficaChecked));
            this.RaisePropertyChanged(nameof(SocioChecked)); this.RaisePropertyChanged(nameof(TesseraChecked)); }
    }
    public bool AnagraficaChecked => SearchKind=="Anagrafica";
    public bool SocioChecked => SearchKind=="Codice socio";
    public bool TesseraChecked => SearchKind=="Tessera";
    public string Cognome { get=>_cognome; set=>this.RaiseAndSetIfChanged(ref _cognome,value); }
    public string Nome { get=>_nome; set=>this.RaiseAndSetIfChanged(ref _nome,value); }
    public string NumeroSocio { get=>_socio; set=>this.RaiseAndSetIfChanged(ref _socio,value); }
    public string NumeroTessera { get=>_tessera; set=>this.RaiseAndSetIfChanged(ref _tessera,value); }
    public int CognomeSelectedValue { get=>_cognomeMode; set=>this.RaiseAndSetIfChanged(ref _cognomeMode,value); }
    public int NomeSelectedValue { get=>_nomeMode; set=>this.RaiseAndSetIfChanged(ref _nomeMode,value); }
    public int NatoilSelectedValue { get=>_bornMode; set=>this.RaiseAndSetIfChanged(ref _bornMode,value); }
    public DateTimeOffset? DataNascitaOffSet { get=>_born; set=>this.RaiseAndSetIfChanged(ref _born,value); }
    public SociSearch ToSearch()
    {
        if(SocioChecked) return new() { NumeroSocio=SociValidation.Number(NumeroSocio,"Il codice socio") };
        if(TesseraChecked) return new() { NumeroTessera=SociValidation.Number(NumeroTessera,"Il numero tessera") };
        if(CognomeSelectedValue>0&&string.IsNullOrWhiteSpace(Cognome))
            throw new SociValidationException("Inserisci il cognome da cercare.");
        if(NomeSelectedValue>0&&string.IsNullOrWhiteSpace(Nome))
            throw new SociValidationException("Inserisci il nome da cercare.");
        if(NatoilSelectedValue>0&&DataNascitaOffSet is null)
            throw new SociValidationException("Inserisci la data di nascita da cercare.");
        return new() { Cognome=Cognome, Nome=Nome, CognomeMode=CognomeSelectedValue, NomeMode=NomeSelectedValue,
            Natoil=DataNascitaOffSet is { } date ? SociValidation.ToDateInt(date.Date):0, NatoilMode=NatoilSelectedValue };
    }
}