using System.ComponentModel.DataAnnotations.Schema;

namespace Models.Tables
{
    public class Reparto : IStandardTable
    {
#if DEBUG
    ~Reparto() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public int Id { get; set; }
        public int SettoreId { get; set; }
        public int PostazioneId { get; set; }

        public Settore? Settore { get; set; }
        public Postazione? Postazione { get; set; }


        [NotMapped]
        public string Nome
        {
            // Restituisce il nome della postazione se caricata, altrimenti una stringa vuota o ID
            get => Settore?.Nome ?? $"Reparto {Id}";
            set { /* In una tabella di giunzione il setter è spesso vuoto o non usato */ }
        }
    }
}
