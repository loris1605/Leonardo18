namespace Models.Tables
{
    public class Settore : IStandardTable
    {
#if DEBUG
    ~Settore() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int TipoSettoreId { get; set; }

        public TipoSettore? TipoSettore { get; set; }

        public List<Reparto> Reparti { get; set; } = [];
        public List<Listino> Listini { get; set; } = [];
    }
}
