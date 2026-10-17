using Models.Interfaces;
namespace Login.Core.DTO;
public sealed class LoginDTO : IMap
{
#if DEBUG
    ~LoginDTO() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public int Id { get; set; }
    public string NomeOperatore { get; set; } = "";
    public string Nome { get => NomeOperatore; set => NomeOperatore = value ?? ""; }
    public string Titolo => $"Login: {NomeOperatore}";
}