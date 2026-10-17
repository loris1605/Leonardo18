using AppServices.Database;
using ReactiveUI;

namespace Cassa.ViewModels;

public sealed partial class CassaViewModel
{
    private CassaIngresso[] _ingressiOrdinari=[];
    private CassaRientro? _rientro;
    public CassaRientro? Rientro => _rientro;
    public bool HasRientro => _rientro is not null;
    private void ApplyMemberEntryOptions(CassaResult result)
    {
        _rientro=result.Rientro;ApplyFidelity(result.Fidelity,result.TipiFidelity,true);
        this.RaisePropertyChanged(nameof(Rientro));this.RaisePropertyChanged(nameof(HasRientro));
        ApplyEntrySubscriptions(HasRientro?[]:result.AbbonamentiIngresso);
        Ingressi.Clear();
        if(HasRientro)Ingressi.Add(new(){Id=0,Nome="Rientro",Etichetta="RIENTRO GRATUITO",Prezzo=0});
        else foreach(var rate in _ingressiOrdinari)Ingressi.Add(rate);
        SelectedIngresso=Ingressi.FirstOrDefault();
    }
}
