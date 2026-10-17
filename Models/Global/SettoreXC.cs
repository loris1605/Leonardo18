namespace Models.Entity.Global
{
    public class SettoreXC
    {
#if DEBUG
    ~SettoreXC() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public int CODICESETTORE { get; set; }

        private string _mydesc = string.Empty;
        public string DESCSETTORE
        {
            get => _mydesc is null ? string.Empty : _mydesc;
            set => _mydesc = value is null ? string.Empty : value;
        }

        private string _mynome = string.Empty;
        public string NOMESETTORE
        {
            get => _mynome is null ? string.Empty : _mynome;
            set => _mynome = value is null ? string.Empty : value;
        }

        private List<TariffaXC>? _mytar;
        public List<TariffaXC>? TARIFFE
        {
            get => _mytar is null ? null : _mytar;
            set => _mytar = value is null ? null : value;
        }
    }
}
