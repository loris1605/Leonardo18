namespace Models.Tables
{
    public class TipoFidelity : IStandardTable
    {
#if DEBUG
    ~TipoFidelity() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public int Id { get; set; }
        public int Traguardo { get; set; }
        public decimal ValorePremio { get; set; }
        public bool Attivo { get; set; } = true;

        public string Nome { get; set; } = string.Empty;
        public int DurataGG { get; set; }
        public int TipoFidelityInputId { get; set; }
        public int TipoFidelityOutputId { get; set; }

        public TipoFidelityInput? TipoFidelityInput { get; set; }
        public TipoFidelityOutput? TipoFidelityOutput { get; set; } 

        public List<Fidelity> Fidelities { get; set; } = [];
    }
}
