namespace AppServices.Database;
public sealed class CassaUscita
{
    public Guid IdOperazione { get; set; }
    public string VersioneConto { get; set; }="";
    public bool Confermata { get; set; }
    public DateTime? DataUscita { get; set; }
    public decimal DaPagare { get; set; }
    public decimal Incassato { get; set; }
    public decimal Sospeso { get; set; }
}
