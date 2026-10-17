namespace Models.Tables
{
    public class TipoSettore
    {
#if DEBUG
    ~TipoSettore() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;

        public List<Settore> Settori { get; set; } = [];
    }
}
