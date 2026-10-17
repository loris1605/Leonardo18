namespace AppServices
{
    public class Flags
    {
#if DEBUG
    ~Flags() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public static bool ServerAttivo { get; set; }

        public static DatabaseAttivo DbStatus { get; set; }


        public enum DatabaseAttivo
        {
            NoDataBase = 0,
            SiDataBase = 1,
            DaAggiornare = 2

        }


    }
}
