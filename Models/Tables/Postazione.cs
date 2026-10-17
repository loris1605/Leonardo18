namespace Models.Tables
{
    public class Postazione : IStandardTable
    {
#if DEBUG
    ~Postazione() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public int TipoPostazioneId { get; set; }
        public int TipoRientroId { get; set; }

        public TipoPostazione? TipoPostazione { get; set; }
        public TipoRientro? TipoRientro { get; set; }

        public List<Permesso> Permessi {  get; set; } = [];
        public List<Reparto> Reparti { get; set; } = [];


    }
}
