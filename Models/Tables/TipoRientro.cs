namespace Models.Tables
{
    public class TipoRientro : IStandardTable
    {
#if DEBUG
    ~TipoRientro() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public int DurataOre { get; set; }

        public List<Postazione> Postazioni { get; set; } = [];

    }
}
