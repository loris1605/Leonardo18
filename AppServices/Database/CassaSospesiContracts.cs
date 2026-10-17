namespace AppServices.Database;

public sealed class CassaSospeso
{
    public long Id { get; set; }
    public int? PersonId { get; set; }
    public int? PersonId2 { get; set; }
    public string Nominativo { get; set; } = "";
    public string Nominativo2 { get; set; } = "";
    public string NumeroTessera { get; set; } = "";
    public string NumeroTessera2 { get; set; } = "";
    public string Descrizione { get; set; } = "";
    public string Dettaglio { get; set; } = "";
    public string Tipo { get; set; } = "";
    public string Origine { get; set; } = "";
    public decimal Importo { get; set; }
    public DateTime DataOra { get; set; }
    public DateTime? DataScadenza { get; set; }
    public bool CanIncassare { get; set; }
    public string Soci => string.IsNullOrEmpty(Nominativo2)?Nominativo:Nominativo+" / "+Nominativo2;
    public string Tessere => string.IsNullOrEmpty(NumeroTessera2)?NumeroTessera:NumeroTessera+" / "+NumeroTessera2;
}
