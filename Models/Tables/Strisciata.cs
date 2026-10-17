namespace Models.Tables
{
    public class Strisciata : IStandardTable
    {
#if DEBUG
    ~Strisciata() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public int Id { get; set; }
        public string CodiceSocio { get; set; } = string.Empty;
        public int DataOra { get; set; }
        public string NumeroTessera { get; set; } = string.Empty;
        public string Cognome { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public int Natoil { get; set; } 
        public int Scadenza { get; set; }
        public int CodMotivo { get; set; }

    }
}
