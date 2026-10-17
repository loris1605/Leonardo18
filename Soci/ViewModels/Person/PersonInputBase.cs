using AppServices.Database;
namespace Soci.ViewModels;
public abstract class PersonInputBase(string action,string title,SociRecord data) : SociInputBase(action,title,data)
{
    public override bool PersonFields => true;
    public override bool SocioFields => Action=="person-add";
    public override bool CardFields => Action=="person-add";
}
public sealed class PersonAddViewModel() : PersonInputBase("person-add","NUOVO SOCIO",new SociRecord());
public sealed class PersonUpdViewModel(SociRecord data) : PersonInputBase("person-update","MODIFICA ANAGRAFICA",data);
public sealed class PersonDelViewModel(SociRecord data) : PersonInputBase("person-delete","ELIMINA ANAGRAFICA",data);