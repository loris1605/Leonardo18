using Login.Core.DTO;
namespace Login.ViewModels.Map;
public sealed class LoginMap
{
#if DEBUG
    ~LoginMap() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public int Id { get; }
    public string NomeOperatore { get; }
    public string Titolo => $"Login: {NomeOperatore}";
    public LoginMap(LoginDTO dto) { ArgumentNullException.ThrowIfNull(dto); Id = dto.Id; NomeOperatore = dto.NomeOperatore; }
    public LoginDTO ToDto() => new() { Id = Id, NomeOperatore = NomeOperatore };
}