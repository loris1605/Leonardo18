namespace Models.Tables
{
    public class Giornata
    {
#if DEBUG
    ~Giornata() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public int Id { get; set; }
        public Guid? AperturaId { get; set; }
        public decimal CassaIniziale { get; set; }
        public string ConteggioCassaIniziale { get; set; } = "";
        public DateTime DataInizio {  get; set; }
        public DateTime DataFine { get; set; }
        public bool Aperta { get; set; }
    }
}
