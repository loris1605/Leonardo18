namespace Models.Tables
{
    public class Operatore : IStandardTable
    {
#if DEBUG
    ~Operatore() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool Abilitato { get; set; }
        public int Pass {  get; set; }

        public int PersonId { get; set; }
        

        public List<Permesso> Permessi { get; set; } = [];

    }
}
