namespace Models.Entity.Global
{
    public class OperatoreXC
    {
#if DEBUG
    ~OperatoreXC() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public int IDOPERATORE { get; set; }

        private string _mynome = string.Empty;
        public string NOMEOPERATORE
        {
            get => _mynome is null ? string.Empty : _mynome;
            set => _mynome = value is null ? string.Empty : value;
        }

        private string _mypass = string.Empty;
        public string PASSWORD
        {
            get => _mypass is null ? string.Empty : _mypass;
            set => _mypass = value is null ? string.Empty : value;
        }

        public decimal FONDOCASSA { get; set; }

        private List<PostazioneXC>? _mypost;
        public List<PostazioneXC>? POSTAZIONI
        {
            get => _mypost is null ? null : _mypost;
            set => _mypost = value is null ? null : value;
        }

        private TurnoXC? _myturno;
        public TurnoXC? TURNO
        {
            get => _myturno is null ? null : _myturno;
            set => _myturno = value is null ? null : value;
        }

        private GiornataXC? _mygior;
        public GiornataXC? GIORNATA
        {
            get => _mygior is null ? null : _mygior;
            set => _mygior = value is null ? null : value;
        }
    }
}
