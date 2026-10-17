using ReactiveUI;
using ReactiveUI.Primitives;
namespace Servizi.Contracts;
public interface IServiziViewModel : IRoutableViewModel { IObservable<RxVoid> ServiziToMenu { get; } }