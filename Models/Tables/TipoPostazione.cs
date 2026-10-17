namespace Models.Tables
{
    public class TipoPostazione
    {
#if DEBUG
    ~TipoPostazione() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;

        public List<Postazione> Postazioni { get; set; } = [];

    }
}
