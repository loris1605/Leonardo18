namespace AppServices.Database;

public sealed class TaglioEuro
{
    public decimal Taglio { get; set; }
    public int Quantita { get; set; }
    public decimal Totale => Taglio * Quantita;
}
public sealed class MenuAperturaRequest
{
    public Guid IdOperazione { get; set; }
    public TaglioEuro[] Tagli { get; set; } = [];
}
public static class CassaInizialeRules
{
    public static decimal[] TagliConsentiti => [500m,200m,100m,50m,20m,10m,5m,2m,1m,0.50m,0.20m,0.10m];
    public static TaglioEuro[] Verifica(MenuAperturaRequest? request)
    {
        if(request is null || request.IdOperazione==Guid.Empty)
            throw new InvalidOperationException("Compila il conteggio della cassa iniziale prima di aprire la giornata.");
        var tagli=request.Tagli;
        var allowed=TagliConsentiti;
        if(tagli is null || tagli.Length!=allowed.Length || tagli.Any(x=>x is null || !allowed.Contains(x.Taglio) || x.Quantita<0 || x.Quantita>1000000)
            || tagli.Select(x=>x.Taglio).Distinct().Count()!=allowed.Length)
            throw new InvalidOperationException("Conteggio non valido. Inserisci quantità intere non negative per tutti i tagli da 500 euro a 10 centesimi.");
        return allowed.Select(t=>new TaglioEuro{Taglio=t,Quantita=tagli.Single(x=>x.Taglio==t).Quantita}).ToArray();
    }
}
