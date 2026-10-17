namespace Models.Tables
{
    public class Tessera
    {
#if DEBUG
    ~Tessera() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public int Id { get; set; }
        public string NumeroTessera { get; set; } = string.Empty;
        public int Scadenza { get; set; }
        public int SocioId { get; set; }
        public bool Abilitato { get; set; } = true;

        public Socio? Socio { get; set; }


    }
}
