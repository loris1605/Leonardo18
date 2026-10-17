using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models.Tables
{
    public class TipoAbbonamento : IStandardTable
    {
#if DEBUG
    ~TipoAbbonamento() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public int NumeroIngressi { get; set; }
        public int DurataGiorni { get; set; }
        public decimal Prezzo { get; set; }
        public bool Attivo { get; set; }

        public List<Abbonamento> Abbonamenti { get; set; } = new List<Abbonamento>();

    }
    
}
