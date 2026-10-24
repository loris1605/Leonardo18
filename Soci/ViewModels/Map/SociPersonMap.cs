using AppServices.Database;
using Soci.Core.DTO;
namespace Soci.ViewModels.Map;
public sealed class SociPersonMap(SociRecord data)
{
#if DEBUG
    ~SociPersonMap() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public bool Bloccato => SociValidation.BloccoAttivo(data.BloccoSocio,data.BloccoSocioFino,DateTime.Today);
    public string StatoBlocco => Bloccato ? SociValidation.AvvisoBlocco(data.BloccoSocioFino,data.BloccoSocioMotivo)
        : data.BloccoSocio ? "Blocco scaduto" : "Socio non bloccato";
    public int Id => data.Id;
    public int CodiceSocio => data.CodiceSocio;
    public int CodiceTessera => data.CodiceTessera;
    public string Cognome => data.Cognome;
    public string Nome => data.Nome;
    public string NatoilText => SociValidation.DateText(data.Natoil);
    public string NumeroSocio => data.NumeroSocio;
    public string NumeroTessera => data.NumeroTessera;
    public string ScadenzaText => SociValidation.DateText(data.Scadenza);
    public string Titolo => $"{Cognome} {Nome}";
    public SociPersonDTO ToDto() => new() {
        BloccoSocio=data.BloccoSocio,BloccoSocioFino=data.BloccoSocioFino,BloccoSocioMotivo=data.BloccoSocioMotivo,
        Id=data.Id, Cognome=data.Cognome, Nome=data.Nome, Natoil=data.Natoil, CodiceUnivoco=data.CodiceUnivoco,
        CodiceSocio=data.CodiceSocio, NumeroSocio=data.NumeroSocio, CodiceTessera=data.CodiceTessera,
        NumeroTessera=data.NumeroTessera, Scadenza=data.Scadenza };
}