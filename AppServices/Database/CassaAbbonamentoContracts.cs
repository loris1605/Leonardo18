namespace AppServices.Database;

public sealed class CassaTipoAbbonamento
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public int NumeroIngressi { get; set; }
    public int DurataGiorni { get; set; }
    public decimal Prezzo { get; set; }
    public string Label => $"{Nome} · {Prezzo:0.00} €";
}

public sealed class CassaAcquistoAbbonamento
{
    public int AbbonamentoId { get; set; }
    public string Nome { get; set; } = "";
    public string Socio1 { get; set; } = "";
    public string Socio2 { get; set; } = "";
    public string NumeroTessera1 { get; set; } = "";
    public string NumeroTessera2 { get; set; } = "";
    public int Ingressi { get; set; }
    public DateTime DataInizio { get; set; }
    public DateTime DataFine { get; set; }
    public decimal Prezzo { get; set; }
    public bool Pagato { get; set; }
    public long? SospesoId { get; set; }
    public bool HasSecondoSocio => !string.IsNullOrEmpty(NumeroTessera2);
    public string Riepilogo => $"Abbonamento n. {AbbonamentoId} · {Nome}\n{Ingressi} ingressi · dal {DataInizio:dd/MM/yyyy} al {DataFine:dd/MM/yyyy}\n{Prezzo:0.00} € · {(Prezzo==0?"Gratuito":Pagato?"Pagato":"In sospeso n. "+SospesoId)}";
}