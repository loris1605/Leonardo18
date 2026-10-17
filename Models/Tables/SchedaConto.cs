using System.ComponentModel.DataAnnotations.Schema;
    
namespace Models.Tables
{
    public class SchedaConto : IStandardTable
    {
#if DEBUG
    ~SchedaConto() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public int Id { get; set; }
        public bool IsIngresso { get; set; }
        public int SchedaId { get; set; }
        public string DescSettore { get; set; } = string.Empty;
        public string DescPostazione { get; set; } = string.Empty;
        public string VoiceDesc { get; set; } = string.Empty;
        public decimal VoicePrice { get; set; }
        public bool Pagato { get; set; }
        public string Note { get; set; } = string.Empty;
        public DateTime DataOra { get; set; }

        public Scheda? Scheda { get; set; }

        [NotMapped]
        public string Nome
        {
            // Restituisce il nome della postazione se caricata, altrimenti una stringa vuota o ID
            get => Scheda?.Nome ?? $"Scheda {Id}";
            set { /* In una tabella di giunzione il setter è spesso vuoto o non usato */ }
        }

    }
}
