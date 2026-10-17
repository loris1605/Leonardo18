using AppServices.Database;
using ReactiveUI;
using ReactiveUI.Primitives;
namespace Servizi.ViewModels;
public sealed partial class ServiziViewModel
{
    private bool _fidelitySection;
    private string _premio="Ingresso Fidelity";
    public bool IsFidelity => _fidelitySection;
    public string SectionTitle => IsFidelity?"FIDELITY":"TIPI DI ABBONAMENTO";
    public string NomeLabel => IsFidelity?"NOME FIDELITY":"NOME ABBONAMENTO";
    public string NumeroLabel => IsFidelity?"TRAGUARDO TIMBRI":"NUMERO INGRESSI";
    public string NumeroColumn => IsFidelity?"TIMBRI":"INGRESSI";
    public string PrezzoColumn => IsFidelity?"PREMIO €":"PREZZO €";
    public string PrezzoLabel => IsFidelity?"PREMIO SCONTO €":"PREZZO €";
    public bool ShowPrice => !IsFidelity||SelectedPremio=="Sconto in denaro";
    public string[] PremioOptions { get; }=["Ingresso Fidelity","Sconto in denaro"];
    public string SelectedPremio {get=>_premio;set {this.RaiseAndSetIfChanged(ref _premio,value);this.RaisePropertyChanged(nameof(ShowPrice));}}
    public ReactiveCommand<RxVoid,RxVoid> AbbonamentiSectionCommand {get;private set;}=null!;
    public ReactiveCommand<RxVoid,RxVoid> FidelitySectionCommand {get;private set;}=null!;
    private void InitializeSections()
    {
        AbbonamentiSectionCommand=ReactiveCommand.CreateFromTask(()=>ChangeSection(false),_ready);
        FidelitySectionCommand=ReactiveCommand.CreateFromTask(()=>ChangeSection(true),_ready);
    }
    private async Task ChangeSection(bool fidelity)
    {
        if(!GroupEnabled||IsFidelity==fidelity)return;
        _fidelitySection=fidelity;_all=[];Records.Clear();Selected=null;SearchText="";
        foreach(var name in new[]{nameof(IsFidelity),nameof(SectionTitle),nameof(NomeLabel),nameof(NumeroLabel),nameof(PrezzoLabel),nameof(ShowPrice),nameof(NumeroColumn),nameof(PrezzoColumn)})this.RaisePropertyChanged(name);
        await Run(new(){Action=ServiziAction.Load});
    }
}
