using System.Collections.ObjectModel;
using AppServices.Database;
using ReactiveUI;
using ReactiveUI.Primitives;
namespace Cassa.ViewModels;
public sealed partial class CassaViewModel
{
    private CassaFidelity? _fidelity;
    private CassaTipoFidelity? _selectedTipoFidelity;
    private bool _selectedFidelity,_usaCreditoFidelity;
    public CassaFidelity? Fidelity => _fidelity;
    public bool HasFidelity => _fidelity is not null;
    public bool HasCreditoFidelity => _fidelity?.HasCredito==true;
    public string CreditoFidelityLabel => $"Usa credito Fidelity ({_fidelity?.CreditoSconto??0:N2} € disponibili)";
    public bool UsaCreditoFidelity {get=>_usaCreditoFidelity;set=>this.RaiseAndSetIfChanged(ref _usaCreditoFidelity,value);}
    public ObservableCollection<CassaTipoFidelity> TipiFidelity {get;}=[];
    public CassaTipoFidelity? SelectedTipoFidelity {get=>_selectedTipoFidelity;set {this.RaiseAndSetIfChanged(ref _selectedTipoFidelity,value);RefreshFidelity();}}
    public bool CanActivateFidelity => IsUiReady&&HasMember&&_fidelity?.Attiva!=true&&SelectedTipoFidelity is not null;
    public bool ShowActivateFidelity => HasMember&&_fidelity?.Attiva!=true&&TipiFidelity.Count>0;
    public bool ShowIngressoFidelity => !HasRientro&&_fidelity?.CanIngresso==true;
    public bool ShowCreditoFidelity => !HasRientro&&HasCreditoFidelity;
    public bool CanIngressoFidelity => CanSelectIngresso&&!HasRientro&&_fidelity?.CanIngresso==true;
    public ReactiveCommand<RxVoid,RxVoid> AttivaFidelityCommand {get;private set;}=null!;
    public ReactiveCommand<RxVoid,RxVoid> IngressoFidelityCommand {get;private set;}=null!;
    private void InitializeFidelity()
    {
        AttivaFidelityCommand=ReactiveCommand.CreateFromTask(async()=>{
            if(!CanActivateFidelity||Member is not {} member||SelectedTipoFidelity is not {} type)return;
            await Run(CassaAction.ActivateFidelity,"ATTIVAZIONE FIDELITY...",ApplyMemberEntryOptions,card:member.NumeroTessera,
                tipoFidelityId:type.Id,personFidelityId:member.PersonId);
        },_ready);
        IngressoFidelityCommand=ReactiveCommand.Create(()=>{
            if(!CanIngressoFidelity)return;
            SelectedIngresso=null;ClearBollino();_selectedFidelity=true;
            this.RaisePropertyChanged(nameof(SceltaIngresso));RefreshEntry();
        },_ready);
        _subscriptions.Add(AttivaFidelityCommand.ThrownExceptions.SubscribePrimitives(e=>{if(!_isClosing)InfoLabel=e.Message;}));
    }
    private void ClearFidelitySelection(){_selectedFidelity=false;this.RaisePropertyChanged(nameof(SceltaIngresso));}
    private void ApplyFidelity(CassaFidelity? fidelity,CassaTipoFidelity[]? types=null,bool reset=false)
    {
        _fidelity=fidelity;
        if(reset){ClearFidelitySelection();UsaCreditoFidelity=false;}
        if(types is not null){TipiFidelity.Clear();foreach(var type in types)TipiFidelity.Add(type);SelectedTipoFidelity=TipiFidelity.FirstOrDefault();}
        foreach(var name in new[]{nameof(Fidelity),nameof(HasFidelity),nameof(HasCreditoFidelity),nameof(CreditoFidelityLabel)})this.RaisePropertyChanged(name);
        if(!HasCreditoFidelity)UsaCreditoFidelity=false;
        RefreshFidelity();
    }
    private void RefreshFidelity()
    {
        foreach(var name in new[]{nameof(CanActivateFidelity),nameof(ShowActivateFidelity),nameof(CanIngressoFidelity),nameof(ShowIngressoFidelity),nameof(ShowCreditoFidelity)})this.RaisePropertyChanged(name);
    }
}
