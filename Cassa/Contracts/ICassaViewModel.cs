using ReactiveUI;
using ReactiveUI.Primitives;
namespace Cassa.Contracts;

public interface ICassaViewModel : IRoutableViewModel
{
    IObservable<RxVoid> CassaToMenu { get; }
}
