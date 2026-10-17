using AppServices.Database;
namespace Soci.ViewModels;
public abstract class TesseraInputBase(string action,string title,SociRecord data) : SociInputBase(action,title,data)
{
    public override bool CardFields => true;
}
public sealed class TesseraAddViewModel(SociRecord data) : TesseraInputBase("tessera-add","NUOVA TESSERA",data);
public sealed class TesseraUpdViewModel(SociRecord data) : TesseraInputBase("tessera-update","MODIFICA TESSERA",data);
public sealed class TesseraDelViewModel(SociRecord data) : TesseraInputBase("tessera-delete","ELIMINA TESSERA",data);