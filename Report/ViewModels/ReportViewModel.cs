using System.Collections.ObjectModel;
using AppServices.Database;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
using Report.Contracts;
using Report.Printing;
using Report.Core.Repository;
using ViewModels;
namespace Report.ViewModels;
public sealed class ReportViewModel:ViewModelBase,IReportViewModel
{
    private readonly IReportRepository _repository;
    private readonly IReportPrinter _printer;
    private readonly IDisposable _printError;
    private readonly Signal<RxVoid> _menu=new();
    private readonly IDisposable _error;
    private bool _busy;
    private string _info="";
    private ReportResult _data=new();
    private ReportGiornata? _selected;
    public ObservableCollection<ReportGiornata> Giornate {get;}=[];
    public ReportGiornata? SelectedGiornata {get=>_selected;set{if(!_isClosing)this.RaiseAndSetIfChanged(ref _selected,value);}}
    public ReportResult Data=>_data;
    public bool IsBusy=>_busy;
    public bool IsReady=>!_busy&&!_isClosing;
    public bool CanPrint=>IsReady&&HasData;
    public ReactiveCommand<RxVoid,RxVoid> PrintCommand {get;}
    public bool HasData=>Data.GiornataId!=0;
    public string InfoLabel {get=>_info;private set=>this.RaiseAndSetIfChanged(ref _info,value);}
    public string Periodo=>Data.Giornate.FirstOrDefault(x=>x.Id==Data.GiornataId) is {} day?$"Dal {day.Inizio:dd/MM/yyyy HH:mm} al {Data.SituazioneAl:dd/MM/yyyy HH:mm} · Aggiornato alle {Data.AggiornatoAl:HH:mm:ss}":"Nessuna giornata disponibile.";
    public IObservable<RxVoid> ReportToMenu=>_menu;
    public ReactiveCommand<RxVoid,RxVoid> MenuCommand {get;}
    public ReactiveCommand<RxVoid,RxVoid> RefreshCommand {get;}
    public ReportViewModel(IScreen? host,IReportRepository repository,IReportPrinter? printer=null):base(host,"report")
    {
        _repository=repository;_printer=printer??new ReportPrinter();
        PrintCommand=ReactiveCommand.CreateFromTask(Print);
        _printError=PrintCommand.ThrownExceptions.SubscribePrimitives(_=>{if(!_isClosing)InfoLabel="Impossibile aprire la stampa del report.";});
        MenuCommand=ReactiveCommand.Create(()=>{if(IsReady)_menu.OnNext(RxVoid.Default);});
        RefreshCommand=ReactiveCommand.CreateFromTask(()=>Load(SelectedGiornata?.Id??0));
        _error=RefreshCommand.ThrownExceptions.SubscribePrimitives(_=>{if(!_isClosing)InfoLabel="Impossibile aggiornare il report.";});
    }
    protected override Task OnLoading()=>Load(0);
    private async Task Load(int dayId)
    {
        if(!IsReady)return;
        SetBusy(true);InfoLabel="";
        try
        {
            var result=await _repository.Execute(new(){GiornataId=dayId},Token);
            Token.ThrowIfCancellationRequested();if(_isClosing)return;
            _data=result;Giornate.Clear();foreach(var day in result.Giornate)Giornate.Add(day);
            SelectedGiornata=Giornate.FirstOrDefault(x=>x.Id==result.GiornataId);
            foreach(var name in new[]{nameof(Data),nameof(HasData),nameof(Periodo)})this.RaisePropertyChanged(name);
            InfoLabel=HasData?"Pagato/da pagare: stato attuale delle voci. Contabilità: scritture confermate della giornata.":"Apri una giornata dal menu per iniziare.";
        }
        catch(OperationCanceledException){}
        catch(Exception ex){if(!_isClosing)InfoLabel=ex is DatabaseHostException or InvalidOperationException?ex.Message:"Impossibile caricare il report. Verifica la connessione.";}
        finally{if(!_isClosing)SetBusy(false);}
    }
    private async Task Print()
    {
        if(!CanPrint)return;
        var snapshot=Data;SetBusy(true);InfoLabel="Preparazione stampa...";
        try {await _printer.OpenAsync(snapshot,Token);if(!_isClosing)InfoLabel="Report aperto nel browser: premi Stampa / Salva in PDF per scegliere la stampante.";}
        catch(OperationCanceledException){}
        catch(Exception){if(!_isClosing)InfoLabel="Impossibile aprire la stampa. Verifica che sia configurato un browser per i file HTML e riprova.";}
        finally{if(!_isClosing)SetBusy(false);}
    }
    private void SetBusy(bool value){_busy=value;this.RaisePropertyChanged(nameof(IsBusy));this.RaisePropertyChanged(nameof(IsReady));this.RaisePropertyChanged(nameof(CanPrint));}
    protected override Task OnEsc(){if(IsReady)_menu.OnNext(RxVoid.Default);return Task.CompletedTask;}
    protected override void OnFinalDestruction(){_isClosing=true;_error.Dispose();_printError.Dispose();PrintCommand.Dispose();RefreshCommand.Dispose();MenuCommand.Dispose();_menu.Dispose();Giornate.Clear();base.OnFinalDestruction();}
}
