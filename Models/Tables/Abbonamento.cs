using System.ComponentModel.DataAnnotations.Schema;

namespace Models.Tables
{
    public class Abbonamento : IStandardTable
    {
#if DEBUG
    ~Abbonamento() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public int Id { get; set; }
        // Frozen purchase data; accounting remains in its separate journal/Sospesi archive.
        public Guid? AcquistoId { get; set; }
        public decimal? PrezzoAcquisto { get; set; }
        public bool? PagatoAllAcquisto { get; set; }
        public DateTime? DataAcquisto { get; set; }
        public int? OperatoreAcquistoId { get; set; }
        public int? PostazioneAcquistoId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public int TipoAbbonamentoId { get; set; }
        public int? TesseraId1 { get; set; }
        public int? TesseraId2 { get; set; }
        public int? IngressiAcquistati { get; set; }
        public Tessera? Tessera1 { get; set; }
        public Tessera? Tessera2 { get; set; }
        public int PersonId1 { get; set; }
        public int? PersonId2 { get; set; }
        public DateTime DataInizio { get; set; }
        public DateTime DataFine { get; set; }
        public bool Attivo { get; set; }
        public string Note { get; set; } = string.Empty;

        [InverseProperty(nameof(Person.Abbonamenti1))]
        public Person? Person1 { get; set; }

        [InverseProperty(nameof(Person.Abbonamenti2))]
        public Person? Person2 { get; set; }

        public TipoAbbonamento? TipoAbbonamento { get; set; }

        public List<AbbonamentoConto> AbbonamentoConti { get; set; } = new List<AbbonamentoConto>();
    }
    
}
