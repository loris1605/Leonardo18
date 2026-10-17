using System.ComponentModel.DataAnnotations.Schema;

namespace Models.Tables
{
    public class FidelityConto : IStandardTable
    {
#if DEBUG
    ~FidelityConto() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public int Id { get; set; }
        public DateTime? RegistratoIl { get; set; }
        public int TipoMovimento { get; set; } = 1;
        public int Ciclo { get; set; }
        public int? SchedaId { get; set; }
        public int? ContoId { get; set; }
        public decimal Importo { get; set; }

        public int FidelityId { get; set; }
        public int Value { get; set; }
        public int DataOra { get; set; }

        public Fidelity? Fidelity { get; set; }


        [NotMapped]
        public string Nome
        {
            // Restituisce il nome della postazione se caricata, altrimenti una stringa vuota o ID
            get => "";
            set { }
        }

    }
}
