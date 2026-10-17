using AppServices.Database;
using ReactiveUI;
using ReactiveUI.Primitives;
namespace Configurazione.Contracts;
public interface IConfigurazioneViewModel : IRoutableViewModel
{
    IObservable<RxVoid> ConfigurazioneToMenu { get; }
}
public interface IConfigurazioneGroupViewModel : IRoutableViewModel
{
    ConfigurazioneSection Section { get; }
    IObservable<ConfigurazioneNavigation> Navigation { get; }
}
public interface IConfigurazioneInputViewModel : IRoutableViewModel
{
    IObservable<int> Closed { get; }
    void Cancel();
}
public sealed record ConfigurazioneNavigation(ConfigurazioneAction Action,int RecordId,string Nome)
{
#if DEBUG
    ~ConfigurazioneNavigation() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif
}

