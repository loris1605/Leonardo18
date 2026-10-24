using System.ComponentModel.DataAnnotations.Schema;

namespace Models.Tables
{
    public class Person : IStandardTable
    {
#if DEBUG
    ~Person() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public bool BloccoSocio { get; set; }
        public DateTime? BloccoSocioFino { get; set; }
        public string BloccoSocioMotivo { get; set; } = "";
        public int Id { get; set; }
        private string _firstName="",_surName="";
        public string FirstName { get=>_firstName; set=>_firstName=(value??"").ToUpperInvariant(); }
        public string SurName { get=>_surName; set=>_surName=(value??"").ToUpperInvariant(); }
        public int Natoil { get; set; }
        public string UniqueParam {  get; set; } = string.Empty;

        public List<Socio> Soci { get; set; } = new List<Socio>();
       
        public Scheda? Scheda { get; set; }

        public Fidelity? Fidelity { get; set; }

        public List<Abbonamento> Abbonamenti1 { get; set; } = new List<Abbonamento>();
        public List<Abbonamento> Abbonamenti2 { get; set; } = new List<Abbonamento>();


        // Proprietà aggregata di sola lettura per compatibilità; EF non la mapperà
        [NotMapped]
        public List<Abbonamento> Abbonamenti => Abbonamenti1.Concat(Abbonamenti2).ToList();

        [NotMapped]
        public string Nome
        {
            get => $"{FirstName} {SurName}";
            set
            {
                // Logica opzionale, ad esempio per lo split del nome
                // o semplicemente per aggiornare la UI
            }
        }

    }
}
