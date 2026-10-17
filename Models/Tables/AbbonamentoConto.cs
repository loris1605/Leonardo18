namespace Models.Tables
{
    public class AbbonamentoConto : IStandardTable
    {
#if DEBUG
    ~AbbonamentoConto() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public int AbbonamentoId { get; set; }
        public int? SchedaIngressoId { get; set; }
        public int NumeroIngresso { get; set; }
        public DateTime DataIngresso { get; set; }
        public bool Entrato { get; set; }
        public string Note { get; set; } = string.Empty;

        public Abbonamento? Abbonamento { get; set; }
    }
}
