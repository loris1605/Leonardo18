namespace Models.Entity.Global
{
    public class TurnoXC
    {
#if DEBUG
    ~TurnoXC() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public int CODICETURNO { get; set; }

        public DateTime DATAINIZIO { get; set; }

        public DateTime DATAFINE { get; set; }

        public bool APERTO { get; set; }

        public int IDGIORNATA { get; set; }

        public int IDOPERATORE { get; set; }

        public decimal CASSAINIZIALE { get; set; }

        public decimal CASSAFINALE { get; set; }

        public decimal DIFFERENZA { get; set; }


    }
}
