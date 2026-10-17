using Models.Contabilita;
namespace Models.Sospesi;

public enum TipoSospeso { Credito = 1, Debito = 2 }
public enum OrigineSospeso { Abbonamento = 1, Uscita = 2, Manuale = 3 }
public enum StatoSospeso { Aperto = 0, Saldato = 1 }

public class Sospeso
{
    public long Id { get; set; }
    public Guid IdOperazione { get; set; }
    public TipoSospeso Tipo { get; set; } = TipoSospeso.Credito;
    public OrigineSospeso Origine { get; set; }
    public StatoSospeso Stato { get; set; }
    public DateTime DataOra { get; set; }
    public DateTime DataCompetenza { get; set; }
    public DateTime? DataScadenza { get; set; }
    public decimal Importo { get; set; }
    public string Valuta { get; set; } = "EUR";
    public string Descrizione { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    // Historical operational references deliberately have no operational foreign keys.
    public int? PersonId { get; set; }
    public string Nominativo { get; set; } = string.Empty;
    public string? NumeroSocio { get; set; }
    public string? NumeroTessera { get; set; }
    public int? PersonId2 { get; set; }
    public string? Nominativo2 { get; set; }
    public string? NumeroSocio2 { get; set; }
    public string? NumeroTessera2 { get; set; }
    public int? AbbonamentoId { get; set; }
    public int? SchedaId { get; set; }
    public int? SchedaContoId { get; set; }
    public long? PartitaContabileId { get; set; }
    public PartitaContabile? PartitaContabile { get; set; }
    public string OrigineModulo { get; set; } = "Cassa";
    public string OrigineEntita { get; set; } = string.Empty;
    public string OrigineId { get; set; } = string.Empty;
    public int? OperatoreId { get; set; }
    public string? NomeOperatore { get; set; }
    public int? PostazioneId { get; set; }
    public string? NomePostazione { get; set; }
    public int? GiornataId { get; set; }
    public DateTime? DataSaldo { get; set; }
    public long? MovimentoSaldoId { get; set; }
    public MovimentoContabile? MovimentoSaldo { get; set; }
    public int? OperatoreSaldoId { get; set; }
    public int? PostazioneSaldoId { get; set; }
    public int? GiornataSaldoId { get; set; }
}
