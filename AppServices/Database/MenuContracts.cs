namespace AppServices.Database;
public sealed class MenuState
{
#if DEBUG
    ~MenuState() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public LoginStation[] Stations { get; set; } = [];
    public LoginDay? Day { get; set; }
}