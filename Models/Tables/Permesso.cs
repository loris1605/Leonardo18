using System.ComponentModel.DataAnnotations.Schema;

namespace Models.Tables
{
    public class Permesso : IStandardTable
    {
#if DEBUG
    ~Permesso() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public int Id { get; set; }
        public int OperatoreId { get; set; }
        public int PostazioneId { get; set; }

        public Operatore? Operatore { get; set; }
        public Postazione? Postazione { get; set; }

        [NotMapped]
        public string Nome
        {
            // Restituisce il nome della postazione se caricata, altrimenti una stringa vuota o ID
            get => Postazione?.Nome ?? $"Permesso {Id}";
            set { /* In una tabella di giunzione il setter è spesso vuoto o non usato */ }
        }
    }
}
