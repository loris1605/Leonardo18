using ReactiveUI;
using ReactiveUI.Primitives;
namespace Login.Contracts;
public interface ILoginViewModel : IRoutableViewModel
{
    IObservable<RxVoid> LoginSuccesso { get; }
}