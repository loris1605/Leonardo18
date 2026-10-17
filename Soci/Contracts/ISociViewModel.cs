using ReactiveUI;
using ReactiveUI.Primitives;
namespace Soci.Contracts;

public interface ISociViewModel : IRoutableViewModel
{
    IObservable<RxVoid> SociToMenu { get; }
}
