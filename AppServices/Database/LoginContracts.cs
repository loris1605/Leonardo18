namespace AppServices.Database;

public sealed class LoginOperator
{
#if DEBUG
    ~LoginOperator() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public int Id { get; set; }
    public string Name { get; set; } = "";
}
public sealed class LoginSession
{
#if DEBUG
    ~LoginSession() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public int OperatorId { get; set; }
    public string OperatorName { get; set; } = "";
    public List<LoginStation> Stations { get; set; } = [];
    public LoginDay? Day { get; set; }
}
public sealed class LoginStation
{
#if DEBUG
    ~LoginStation() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int TypeId { get; set; }
    public int ReturnTypeId { get; set; }
    public int ReturnHours { get; set; }
    public List<LoginSector> Sectors { get; set; } = [];
}
public sealed class LoginSector
{
#if DEBUG
    ~LoginSector() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public List<LoginRate> Rates { get; set; } = [];
}
public sealed class LoginRate
{
#if DEBUG
    ~LoginRate() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public int Id { get; set; }
    public string Label { get; set; } = "";
    public decimal Price { get; set; }
}
public sealed class LoginDay
{
#if DEBUG
    ~LoginDay() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public int Id { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public bool Open { get; set; }
}