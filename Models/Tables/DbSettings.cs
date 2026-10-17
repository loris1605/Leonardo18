namespace Models.Tables
{
    public class DbSettings
    {
#if DEBUG
    ~DbSettings() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public int Id { get; set; }
        public int Version { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
