using System.Collections.ObjectModel;
using AppServices.Database;
using ReactiveUI;
using ReactiveUI.Primitives;
namespace Cassa.ViewModels;
public sealed partial class CassaViewModel
{
    private CassaFidelity? _fidelity;
    private CassaFidelity? _tesseraFidelitySource;
    private bool _timbroFidelityCorrente;
    public ObservableCollection<TimbroIngressoFidelityViewModel> TimbriTesseraFidelity {get;}=[];
    public bool HasPremioFidelity => !string.IsNullOrEmpty(_fidelity?.Premio);
    public bool HasAltriSpaziFidelity => TimbriAlProssimoPremio>40;
    public ObservableCollection<TimbroIngressoFidelityViewModel> TimbriIngressoFidelity {get;}=[];
    public ObservableCollection<TimbroIngressoFidelityViewModel> TimbriRegistratiFidelity {get;}=[];
    public bool HasTimbriRegistratiFidelity => TimbriRegistratiFidelity.Count>0;
    private int TimbriAlProssimoPremio => _fidelity is {Attiva:true,Traguardo:>0} f ? f.Traguardo-Math.Max(0,f.Punti)%f.Traguardo:0;
    public string ProssimoPremioFidelity => TimbriAlProssimoPremio>0?$"Al prossimo premio: {TimbriAlProssimoPremio} timbri.":"";
    public string AltriSpaziFidelity => TimbriAlProssimoPremio>40?$"Altri {TimbriAlProssimoPremio-40} spazi fino al prossimo premio.":"";
    public string TimbroIngressoFidelityInfo => _fidelity?.Attiva!=true?"La Fidelity deve essere attiva per ricevere nuovi timbri.":
        HasRientro?"RIENTRO: non viene aggiunto un timbro.":MemberInside?"Il socio è già all'interno: nessun nuovo timbro.":
        Member?.SocioBloccato==true?"Socio bloccato: ingresso non consentito.":Member?.HasSospesi==true?"Salda i sospesi prima di registrare l'ingresso.":
        "Scegli l'ingresso e indica la posizione. Il timbro verde registra ingresso e timbro insieme. REGISTRA INGRESSO fa entrare senza timbrare.";
    private void RefreshTesseraFidelity()
    {
        var corrente=IsEntry && _fidelity?.Attiva==true && !HasRientro && !MemberInside
            && Member is {SocioBloccato:false,HasSospesi:false};
        if(!ReferenceEquals(_tesseraFidelitySource,_fidelity)||_timbroFidelityCorrente!=corrente)
        {
            _tesseraFidelitySource=_fidelity;_timbroFidelityCorrente=corrente;
            TimbriIngressoFidelity.Clear();TimbriRegistratiFidelity.Clear();TimbriTesseraFidelity.Clear();
            var history=_fidelity?.Timbri??[];
            for(var i=history.Length-1;i>=0;i--)TimbriRegistratiFidelity.Add(new(i+1,history[i]));
            for(var i=0;i<history.Length;i++)TimbriTesseraFidelity.Add(TimbriRegistratiFidelity[history.Length-1-i]);
            for(var i=1;i<=Math.Min(TimbriAlProssimoPremio,40);i++)
            {
                var next=new TimbroIngressoFidelityViewModel(history.Length+i,corrente:corrente&&i==1);
                TimbriIngressoFidelity.Add(next);TimbriTesseraFidelity.Add(next);
            }
        }
        foreach(var name in new[]{nameof(HasPremioFidelity),nameof(HasAltriSpaziFidelity),nameof(HasTimbriRegistratiFidelity),nameof(ProssimoPremioFidelity),nameof(AltriSpaziFidelity),nameof(TimbroIngressoFidelityInfo)})this.RaisePropertyChanged(name);
    }
    private CassaTipoFidelity? _selectedTipoFidelity;
    private bool _selectedFidelity,_usaCreditoFidelity;
    public CassaFidelity? Fidelity => _fidelity;
    public bool HasFidelity => _fidelity is not null;
    public bool HasCreditoFidelity => _fidelity?.HasCredito==true;
    public string CreditoFidelityLabel => $"Usa credito Fidelity ({_fidelity?.CreditoSconto??0:N2} € disponibili)";
    public bool UsaCreditoFidelity {get=>_usaCreditoFidelity;set=>this.RaiseAndSetIfChanged(ref _usaCreditoFidelity,value);}
    public ObservableCollection<CassaTipoFidelity> TipiFidelity {get;}=[];
    public CassaTipoFidelity? SelectedTipoFidelity {get=>_selectedTipoFidelity;set {this.RaiseAndSetIfChanged(ref _selectedTipoFidelity,value);RefreshFidelity();}}
    public bool CanActivateFidelity => IsUiReady&&Member is {SocioBloccato:false}&&_fidelity?.Attiva!=true&&SelectedTipoFidelity is not null;
    public bool ShowActivateFidelity => Member is {SocioBloccato:false}&&_fidelity?.Attiva!=true&&TipiFidelity.Count>0;
    public bool ShowIngressoFidelity => !HasRientro&&_fidelity?.CanIngresso==true;
    public bool ShowCreditoFidelity => !HasRientro&&HasCreditoFidelity;
    public bool CanIngressoFidelity => CanSelectIngresso&&!HasRientro&&_fidelity?.CanIngresso==true;
    public ReactiveCommand<RxVoid,RxVoid> AttivaFidelityCommand {get;private set;}=null!;
    public ReactiveCommand<RxVoid,RxVoid> IngressoFidelityCommand {get;private set;}=null!;
    public ReactiveCommand<RxVoid,RxVoid> TimbraERegistraIngressoCommand {get;private set;}=null!;
    private void InitializeFidelity()
    {
        TimbraERegistraIngressoCommand=ReactiveCommand.CreateFromTask(
            ()=>IsEntry&&Fidelity?.Attiva==true&&!HasRientro?RegisterEntry(true):Task.CompletedTask,_entryReady);
        _subscriptions.Add(TimbraERegistraIngressoCommand.ThrownExceptions.SubscribePrimitives(e=>{if(!_isClosing)InfoLabel=e.Message;}));
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
        RefreshTesseraFidelity();
        foreach(var name in new[]{nameof(CanActivateFidelity),nameof(ShowActivateFidelity),nameof(CanIngressoFidelity),nameof(ShowIngressoFidelity),nameof(ShowCreditoFidelity)})this.RaisePropertyChanged(name);
    }
}
