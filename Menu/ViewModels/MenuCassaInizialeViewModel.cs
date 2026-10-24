using AppServices.Database;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
using System.Collections.ObjectModel;
namespace Menu.ViewModels;

public sealed class TaglioCassaViewModel(decimal taglio, Action changed):ReactiveObject
{
    private decimal? _quantita=0m;
    public decimal Taglio => taglio;
    public decimal? Quantita
    {
        get=>_quantita;
        set { this.RaiseAndSetIfChanged(ref _quantita,value);this.RaisePropertyChanged(nameof(Totale));changed(); }
    }
    public decimal Totale => (_quantita??0m)*taglio;
    public bool IsValid => _quantita is >=0 and <=1000000 && decimal.Truncate(_quantita.Value)==_quantita.Value;
}
public sealed partial class MenuViewModel
{
    private readonly StateSignal<bool> _openingReady=new(false);
    private bool _openingCash;
    private Guid _openingId;
    public bool IsOpeningCash => _openingCash;
    public bool IsOpeningReady => IsOpeningCash&&!_busy&&!_isClosing;
    public ObservableCollection<TaglioCassaViewModel> TagliCassa { get; }=[];
    public decimal TotaleCassaIniziale => TagliCassa.Sum(x=>x.Totale);
    public ReactiveCommand<RxVoid,RxVoid> ConfermaAperturaCommand { get; private set; }=null!;
    public ReactiveCommand<RxVoid,RxVoid> AnnullaAperturaCommand { get; private set; }=null!;
    private void InitializeCassaIniziale()
    {
        foreach(var taglio in CassaInizialeRules.TagliConsentiti)
            TagliCassa.Add(new(taglio,()=>this.RaisePropertyChanged(nameof(TotaleCassaIniziale))));
        ConfermaAperturaCommand=ReactiveCommand.CreateFromTask(ConfirmOpening,_openingReady);
        AnnullaAperturaCommand=ReactiveCommand.Create(CancelOpening,_openingReady);
        _subscriptions.Add(ConfermaAperturaCommand.ThrownExceptions.SubscribePrimitives(_=>InfoError="Apertura giornata non riuscita."));
        _subscriptions.Add(AnnullaAperturaCommand.ThrownExceptions.SubscribePrimitives(_=>InfoError="Impossibile tornare al menu."));
    }
    private void BeginOpening()
    {
        if(!_loaded||_busy||_dayOpen||_isClosing||IsOpeningCash||!(_admin||_cash))return;
        foreach(var row in TagliCassa)row.Quantita=0m;
        _openingId=Guid.NewGuid();_openingCash=true;InfoError="";Refresh();
    }
    private void CancelOpening()
    {
        if(_busy||_isClosing)return;
        _openingCash=false;InfoError="";Refresh();
    }
    private Task ConfirmOpening()
    {
        if(!IsOpeningReady)return Task.CompletedTask;
        if(TagliCassa.Any(x=>!x.IsValid))
        { InfoError="Inserisci quantità intere da 0 a 1.000.000 per ogni taglio.";return Task.CompletedTask; }
        var request=new MenuAperturaRequest{IdOperazione=_openingId,
            Tagli=TagliCassa.Select(x=>new TaglioEuro{Taglio=x.Taglio,Quantita=(int)x.Quantita!.Value}).ToArray()};
        return RunOperation("menu-open","APERTURA GIORNATA E REGISTRAZIONE CASSA INIZIALE...",request);
    }
}
