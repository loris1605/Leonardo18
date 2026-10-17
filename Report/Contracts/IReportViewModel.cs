using ReactiveUI;
using ReactiveUI.Primitives;
namespace Report.Contracts;
public interface IReportViewModel:IRoutableViewModel { IObservable<RxVoid> ReportToMenu {get;} }
