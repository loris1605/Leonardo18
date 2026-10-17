namespace AppServices.Database;

public sealed class CassaRientro
{
    public string Tipo { get; set; } = "";
    public DateTime UltimoIngresso { get; set; }
    public DateTime? ValidoFino { get; set; }
    public int GiornataId { get; set; }
    public string Avviso => $"RIENTRO GRATUITO · ingresso del {UltimoIngresso:dd/MM/yyyy HH:mm}"+
        (ValidoFino is {} end ? $" · valido fino al {end:dd/MM/yyyy HH:mm}" : " · fino alla chiusura della giornata");
}

public static class CassaRientroRules
{
    public static bool Disponibile(int tipoId,int durataOre,DateTime ingresso,int? giornataIngresso,
        int giornataAperta,DateTime apertura,DateTime now)
    {
        if(ingresso>now||tipoId==-1)return false;
        if(tipoId==-2)return giornataIngresso is {} id ? id==giornataAperta : ingresso>=apertura;
        return durataOre>0&&(now-ingresso).TotalHours<=durataOre;
    }
}
