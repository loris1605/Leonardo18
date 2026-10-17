using AppServices.Database;
namespace Soci.Core.DTO;
public sealed class SociPersonDTO : SociRecord
{
    public string Titolo => $"{Cognome} {Nome} ({SociValidation.DateText(Natoil)})";
}