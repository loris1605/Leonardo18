using ReactiveUI;
using ReactiveUI.Primitives;

namespace Connection.Contracts;

public interface IConnectionViewModel : IRoutableViewModel
{
    IObservable<RxVoid> ConnectionToLogin { get; }
}