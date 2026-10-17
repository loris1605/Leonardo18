namespace Models.Tables
{
    public class TipoFidelityInput : IStandardTable
    {
#if DEBUG
    ~TipoFidelityInput() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        

        public List<TipoFidelity> TipiFidelity { get; set; } = new List<TipoFidelity>();
    }
}
