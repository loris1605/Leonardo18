using System.Collections.Generic;

namespace Models.Tables
{
    public class Socio
    {
#if DEBUG
    ~Socio() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public int Id { get; set; }
        public string NumeroSocio { get; set; } = string.Empty;
        public int PersonId { get; set; }
        public bool Abilitato { get; set; }

        public Person? Person { get; set; }

        public List<Tessera> Tessere { get; set; } = [];

    }
}
