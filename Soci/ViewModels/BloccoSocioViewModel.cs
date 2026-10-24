using AppServices.Database;
namespace Soci.ViewModels;
public sealed class BloccoSocioViewModel(SociRecord data):SociInputBase("person-block","BLOCCHI DEL SOCIO",data)
{
    public override bool BloccoFields=>true;
}
