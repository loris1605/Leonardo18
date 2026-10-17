namespace Menu.ViewModels.Map;
public sealed record MenuPostazioneMap(int CodicePostazione, string NomePostazione)
{
#if DEBUG
    ~MenuPostazioneMap() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public string NomeTipoPostazione => "Cassa";
}