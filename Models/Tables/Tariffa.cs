namespace Models.Tables
{
    public class Tariffa : IStandardTable
    {
#if DEBUG
    ~Tariffa() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public bool IsFreeDrink { get; set; }
        public decimal Prezzo { get; set; }

        public List<Listino> Listini { get; set; } = [];
    }
}
