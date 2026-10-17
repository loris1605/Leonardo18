namespace AppServices.Database;

public sealed class CassaAbbonamentoIngresso
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public DateTime DataFine { get; set; }
    public CassaBollino[] Bollini { get; set; } = [];
    public int Disponibili => Bollini.Count(x=>!x.Usato);
    public string Riepilogo => $"{Disponibili} ingressi disponibili · scadenza {DataFine:dd/MM/yyyy}";
}

public sealed class CassaBollino
{
    public int AbbonamentoId { get; set; }
    public int Numero { get; set; }
    public bool Usato { get; set; }
    public DateTime? DataIngresso { get; set; }
}
