using AppServices.Database;
namespace Soci.ViewModels;
public abstract class CodiceSocioInputBase(string action,string title,SociRecord data) : SociInputBase(action,title,data)
{
    public override bool SocioFields => true;
    public override bool CardFields => Action=="socio-add";
}
public sealed class CodiceSocioAddViewModel(SociRecord data) : CodiceSocioInputBase("socio-add","NUOVO CODICE SOCIO",data);
public sealed class CodiceSocioUpdViewModel(SociRecord data) : CodiceSocioInputBase("socio-update","MODIFICA CODICE SOCIO",data);
public sealed class CodiceSocioDelViewModel(SociRecord data) : CodiceSocioInputBase("socio-delete","ELIMINA CODICE SOCIO",data);