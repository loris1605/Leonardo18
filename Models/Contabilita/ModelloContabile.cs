namespace Models.Contabilita;

public enum NaturaConto { Attivita = 1, Passivita = 2, PatrimonioNetto = 3, Costo = 4, Ricavo = 5 }
public enum StatoMovimentoContabile { Bozza = 0, Confermato = 1 }
public enum TipoPartitaContabile { Credito = 1, Debito = 2 }

public class ContoContabile
{
    public int Id { get; set; }
    public string Codice { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public NaturaConto Natura { get; set; }
    public int? ContoPadreId { get; set; }
    public ContoContabile? ContoPadre { get; set; }
    public bool Movimentabile { get; set; } = true;
    public bool Attivo { get; set; } = true;
}

public class MovimentoContabile
{
    public long Id { get; set; }
    // The caller must reuse this identifier when retrying the same accounting event.
    public Guid IdOperazione { get; set; }
    public DateTime DataOra { get; set; }
    public DateTime DataCompetenza { get; set; }
    public StatoMovimentoContabile Stato { get; set; }
    public string Valuta { get; set; } = "EUR";
    public string Causale { get; set; } = string.Empty;
    public string Descrizione { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string OrigineModulo { get; set; } = "Manuale";
    public string? OrigineEntita { get; set; }
    public string? OrigineId { get; set; }
    public string? OrigineEvento { get; set; }
    // Historical references intentionally have no foreign keys to operational tables.
    public int? PersonId { get; set; }
    public string? Nominativo { get; set; }
    public string? NumeroSocio { get; set; }
    public string? NumeroTessera { get; set; }
    public int? OperatoreId { get; set; }
    public string? NomeOperatore { get; set; }
    public int? PostazioneId { get; set; }
    public string? NomePostazione { get; set; }
    public int? GiornataId { get; set; }
    public long? StornoDiId { get; set; }
    public MovimentoContabile? StornoDi { get; set; }
    public List<RigaContabile> Righe { get; set; } = new();

    // Invoke with the complete set of rows before posting; database checks also protect individual rows.
    public void VerificaQuadratura()
    {
        if (Righe.Count < 2 || Righe.Any(x => !x.ImportiValidi) || Righe.Sum(x => x.Dare) != Righe.Sum(x => x.Avere))
            throw new InvalidOperationException("Il movimento deve avere almeno due righe valide e totale Dare uguale al totale Avere.");
    }
}

public class RigaContabile
{
    public long Id { get; set; }
    public long MovimentoId { get; set; }
    public MovimentoContabile Movimento { get; set; } = null!;
    public int NumeroRiga { get; set; }
    public int ContoId { get; set; }
    public ContoContabile Conto { get; set; } = null!;
    public decimal Dare { get; set; }
    public decimal Avere { get; set; }
    public string Descrizione { get; set; } = string.Empty;
    public int? PersonId { get; set; }
    public string? Nominativo { get; set; }
    public int? SettoreId { get; set; }
    public string? NomeSettore { get; set; }
    public int? TariffaId { get; set; }
    public string? EtichettaTariffa { get; set; }
    public string? NumeroTessera { get; set; }
    public int? AbbonamentoId { get; set; }
    public string? NomeAbbonamento { get; set; }
    public bool ImportiValidi => ((Dare > 0 && Avere == 0) || (Avere > 0 && Dare == 0))
        && decimal.Round(Dare, 2) == Dare && decimal.Round(Avere, 2) == Avere;
}

public class PartitaContabile
{
    public long Id { get; set; }
    public long RigaAperturaId { get; set; }
    public RigaContabile RigaApertura { get; set; } = null!;
    public TipoPartitaContabile Tipo { get; set; }
    public int? PersonId { get; set; }
    public string Controparte { get; set; } = string.Empty;
    public string Descrizione { get; set; } = string.Empty;
    public DateTime DataApertura { get; set; }
    public DateTime? DataScadenza { get; set; }
    public decimal ImportoOriginale { get; set; }
    public List<AbbinamentoPartitaContabile> Abbinamenti { get; set; } = new();
}

public class AbbinamentoPartitaContabile
{
    public long Id { get; set; }
    public Guid IdOperazione { get; set; }
    public long PartitaId { get; set; }
    public PartitaContabile Partita { get; set; } = null!;
    public long RigaRegolamentoId { get; set; }
    public RigaContabile RigaRegolamento { get; set; } = null!;
    public DateTime DataOra { get; set; }
    // Positive settles a debt/credit, negative reverses a previous allocation.
    public decimal Importo { get; set; }
    public long? StornoDiId { get; set; }
    public AbbinamentoPartitaContabile? StornoDi { get; set; }
    public string Note { get; set; } = string.Empty;
}
