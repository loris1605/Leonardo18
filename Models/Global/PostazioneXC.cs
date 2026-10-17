using System.ComponentModel.DataAnnotations.Schema;

namespace Models.Entity.Global
{
    public class PostazioneXC
    {
#if DEBUG
    ~PostazioneXC() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        [Column("IdPostazione")]
        public int CODICEPOSTAZIONE { get; set; }

        private string _mydesc = string.Empty;
        [Column("DescPostazione")]
        public string DESCPOSTAZIONE
        {
            get => _mydesc is null ? string.Empty : _mydesc;
            set => _mydesc = value is null ? string.Empty : value;
        }

        [Column("IdTipoPostazione")]
        public int TIPOPOSTAZIONE { get; set; }

        [Column("IdTipoRientro")]
        public int TIPORIENTRO { get; set; }

        [Column("DurataOre")]
        public int ORERIENTRO { get; set; }

        private List<SettoreXC>? _mysett;
        public List<SettoreXC>? SETTORI
        {
            get => _mysett is null ? null : _mysett;
            set => _mysett = value is null ? null : value;
        }


    }
}
