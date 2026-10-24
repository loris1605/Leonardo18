namespace AppServices.Database;

public sealed class ReportRequest { public int GiornataId { get; set; } }
public sealed class ReportGiornata
{
    public int Id { get; set; }
    public DateTime Inizio { get; set; }
    public DateTime Fine { get; set; }
    public bool Aperta { get; set; }
    public string Label=>$"{Inizio:dd/MM/yyyy HH:mm} · {(Aperta?"APERTA":"CHIUSA")} · #{Id}";
}
public sealed class ReportResult
{
    public ReportGiornata[] Giornate { get; set; }=[];
    public int GiornataId { get; set; }
    public DateTime AggiornatoAl { get; set; }
    public DateTime SituazioneAl { get; set; }
    public int Ingressi { get; set; }
    public int Uscite { get; set; }
    public int Presenti { get; set; }
    public int AbbonamentiVenduti { get; set; }
    public decimal ValoreAbbonamenti { get; set; }
    public ReportRiepilogo[] Settori { get; set; }=[];
    public ReportRiepilogo[] Postazioni { get; set; }=[];
    public ReportVoce[] Voci { get; set; }=[];
    public ReportContabilita[] Contabilita { get; set; }=[];
    public decimal CassaIniziale { get; set; }
    public TaglioEuro[] TagliCassaIniziale { get; set; }=[];
    public decimal DisponibilitaMonetaria=>CassaIniziale+SaldoMonetario;
    public decimal Incassi { get; set; }
    public decimal UsciteMonetarie { get; set; }
    public decimal Ricavi { get; set; }
    public decimal Costi { get; set; }
    public decimal SospesiCrediti { get; set; }
    public decimal SospesiDebiti { get; set; }
    public decimal SospesiIncassati { get; set; }
    public int NumeroSospesi { get; set; }
    public decimal TotaleOperativo=>Settori.Sum(x=>x.Netto);
    public decimal PagatoOperativo=>Settori.Sum(x=>x.Pagato);
    public decimal DaPagareOperativo=>Settori.Sum(x=>x.DaPagare);
    public decimal SaldoMonetario=>Incassi-UsciteMonetarie;
}
public sealed class ReportRiepilogo
{
    public string Nome { get; set; }="";
    public int Operazioni { get; set; }
    public decimal Lordo { get; set; }
    public decimal Storni { get; set; }
    public decimal Pagato { get; set; }
    public decimal DaPagare { get; set; }
    public decimal Netto=>Lordo-Storni;
}
public sealed class ReportVoce
{
    public string Settore { get; set; }="";
    public string Descrizione { get; set; }="";
    public int Operazioni { get; set; }
    public decimal Totale { get; set; }
    public decimal Pagato { get; set; }
    public decimal DaPagare { get; set; }
}
public sealed class ReportContabilita
{
    public string Settore { get; set; }="";
    public decimal Ricavi { get; set; }
    public decimal Costi { get; set; }
    public decimal Margine=>Ricavi-Costi;
}
