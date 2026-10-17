namespace Models.Entity.Global
{
    public class GiornataXC
    {
#if DEBUG
    ~GiornataXC() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public int IDGIORNATA { get; set; }

        public DateTime? DATAINIZIO { get; set; }

        public DateTime? DATAFINE { get; set; }

        public bool APERTA { get; set; }
    }
}
