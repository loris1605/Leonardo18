using ReactiveUI;
namespace Menu.Contracts;

public interface IMenuViewModel : IRoutableViewModel
{
    IObservable<string> Navigation { get; }
}
