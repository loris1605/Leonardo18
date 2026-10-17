using System.Collections.ObjectModel;
using AppServices.Database;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace Cassa.ViewModels;

public sealed class BollinoIngressoViewModel(CassaBollino data) : ReactiveObject
{
    public CassaBollino Data { get; } = data;
    public int Numero => Data.Numero;
    public bool Disponibile => !Data.Usato;
    public string Stato => Data.Usato ? $"Usato\n{Data.DataIngresso:dd/MM/yyyy}\n{Data.DataIngresso:HH:mm}" : "Disponibile";
    public string Simbolo => Data.Usato ? "✓" : Numero.ToString();
    private bool _selezionato;
    public bool Selezionato { get=>_selezionato; set {this.RaiseAndSetIfChanged(ref _selezionato,value);this.RaisePropertyChanged(nameof(Sfondo));this.RaisePropertyChanged(nameof(Bordo));} }
    public string Sfondo => Data.Usato ? "#E2E8F0" : Selezionato ? "#BBF7D0" : "#FFFFFF";
    public string Bordo => Selezionato ? "#15803D" : Data.Usato ? "#94A3B8" : "#2D6A73";
}

public sealed class TesseraAbbonamentoViewModel(CassaAbbonamentoIngresso data)
{
    public string Nome => data.Nome;
    public string Riepilogo => data.Riepilogo;
    public BollinoIngressoViewModel[] Bollini { get; } = data.Bollini.Select(x=>new BollinoIngressoViewModel(x)).ToArray();
}

public sealed partial class CassaViewModel
{
    public ObservableCollection<TesseraAbbonamentoViewModel> AbbonamentiIngresso { get; } = [];
    private BollinoIngressoViewModel? _bollino;
    public bool HasAbbonamentiIngresso => AbbonamentiIngresso.Count>0;
    public string SceltaIngresso => _selectedFidelity?"Ingresso Fidelity selezionato":_bollino is null ? "" : $"Ingresso abbonamento selezionato · bollino {_bollino.Numero}";
    public ReactiveCommand<BollinoIngressoViewModel,RxVoid> SelezionaBollinoCommand { get; private set; } = null!;
    private void InitializeEntrySubscriptions()
    {
        SelezionaBollinoCommand=ReactiveCommand.Create<BollinoIngressoViewModel>(bollino=>{
            if(!IsEntry||!CanSelectIngresso||!bollino.Disponibile||!AbbonamentiIngresso.Any(x=>x.Bollini.Contains(bollino)))return;
            SelectedIngresso=null;ClearFidelitySelection();ClearBollino();_bollino=bollino;bollino.Selezionato=true;
            this.RaisePropertyChanged(nameof(SceltaIngresso));RefreshEntry();
        },_ready);
    }
    private void ClearBollino()
    {
        if(_bollino is not null)_bollino.Selezionato=false;
        _bollino=null;this.RaisePropertyChanged(nameof(SceltaIngresso));
    }
    private void ApplyEntrySubscriptions(CassaAbbonamentoIngresso[] subscriptions)
    {
        ClearBollino();AbbonamentiIngresso.Clear();
        foreach(var subscription in subscriptions)AbbonamentiIngresso.Add(new(subscription));
        this.RaisePropertyChanged(nameof(HasAbbonamentiIngresso));RefreshEntry();
    }
    private CassaIngresso? EntryValidationRate => _selectedFidelity?new CassaIngresso{Etichetta="INGRESSO FIDELITY",Prezzo=0}:_bollino is null ? SelectedIngresso : new CassaIngresso {Prezzo=0,Etichetta="Ingresso abbonamento"};
}
