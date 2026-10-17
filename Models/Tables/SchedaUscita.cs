namespace Models.Tables;

/// <summary>Immutable identity and entry/exit snapshot; payment flags may be settled later.</summary>
public class SchedaUscita
{
    public int Id { get; set; } // Original Scheda ID, never reused.
    public Guid IdOperazione { get; set; }
    public string VersioneConto { get; set; }="";
    public int PersonId { get; set; }
    public string Posizione { get; set; }="";
    public string NumeroTessera { get; set; }="";
    public string Cognome { get; set; }="";
    public string Nome { get; set; }="";
    public int Natoil { get; set; }
    public bool IngressoAnnullato { get; set; }
        public bool IsRientro { get; set; }
        public int? GiornataIngressoId { get; set; }
        public DateTime CheckinTime { get; set; }
    public DateTime CheckoutTime { get; set; }
    public int Grb1 { get; set; }
    public int Grb2 { get; set; }
    public int Grb3 { get; set; }
    public int Grb4 { get; set; }
    public decimal Consumazione { get; set; }
    public bool Blocco { get; set; }
    public string Note { get; set; }="";
    public int OperatoreId { get; set; }
    public string NomeOperatore { get; set; }="";
    public int PostazioneId { get; set; }
    public string NomePostazione { get; set; }="";
    public int GiornataId { get; set; }
    public bool Incassa { get; set; }
    public decimal ImportoIncassato { get; set; }
    public decimal ImportoSospeso { get; set; }
    public List<SchedaUscitaConto> Conto { get; set; }=[];
}
public class SchedaUscitaConto
{
    public bool IsIngresso { get; set; }
    public int Id { get; set; } // Original SchedaConto ID.
    public int SchedaId { get; set; }
    public SchedaUscita Scheda { get; set; }=null!;
    public string DescSettore { get; set; }="";
    public string DescPostazione { get; set; }="";
    public string VoiceDesc { get; set; }="";
    public decimal VoicePrice { get; set; }
    public bool Pagato { get; set; }
    public string Note { get; set; }="";
    public DateTime DataOra { get; set; }
}
