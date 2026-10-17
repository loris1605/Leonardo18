using AppServices.Database;
using Avalonia.Threading;
using Configurazione.Contracts;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
using ViewModels;
namespace Configurazione.ViewModels;

public sealed class ConfigurazioneViewModel : ViewModelBase,IScreen,IConfigurazioneViewModel
{
    private readonly Func<IScreen,ConfigurazioneSection,int,IConfigurazioneGroupViewModel> _groupFactory;
    private readonly Func<IScreen,ConfigurazioneSection,ConfigurazioneAction,int,string,IConfigurazioneInputViewModel> _inputFactory;
    private readonly Signal<RxVoid> _toMenu=new();
    private readonly StateSignal<bool> _canNavigate=new(true);
    private readonly List<IDisposable> _commandErrors=[];
    private IDisposable? _groupSubscription,_inputSubscription,_groupExecution,_inputExecution;
    private ConfigurazioneSection _section;
    private bool _hasEditor,_wide;
    public RoutingState GroupRouter {get;}=new();
    public RoutingState InputRouter {get;}=new();
    public RoutingState Router=>GroupRouter;
    public IObservable<RxVoid> ConfigurazioneToMenu=>_toMenu;
    public bool HasEditor=>_hasEditor;
    public bool GroupEnabled=>!_hasEditor;
    public bool IsGroupVisible=>!_hasEditor||_wide;
    public bool IsWideLayout {
        get=>_wide;
        set {this.RaiseAndSetIfChanged(ref _wide,value);this.RaisePropertyChanged(nameof(IsGroupVisible));}
    }
    public ReactiveCommand<ConfigurazioneSection,RxVoid> SectionCommand {get;}
    public ReactiveCommand<RxVoid,RxVoid> MenuCommand {get;}
    public ConfigurazioneViewModel(IScreen host,
        Func<IScreen,ConfigurazioneSection,int,IConfigurazioneGroupViewModel> groupFactory,
        Func<IScreen,ConfigurazioneSection,ConfigurazioneAction,int,string,IConfigurazioneInputViewModel> inputFactory):base(host)
    {
        _groupFactory=groupFactory??throw new ArgumentNullException(nameof(groupFactory));
        _inputFactory=inputFactory??throw new ArgumentNullException(nameof(inputFactory));
        SectionCommand=ReactiveCommand.Create<ConfigurazioneSection>(s=>ShowGroup(s),_canNavigate);
        MenuCommand=ReactiveCommand.Create(BackToMenu,_canNavigate);
        _commandErrors.Add(SectionCommand.ThrownExceptions.SubscribePrimitives(ex=>System.Diagnostics.Debug.WriteLine(ex.Message)));
        _commandErrors.Add(MenuCommand.ThrownExceptions.SubscribePrimitives(ex=>System.Diagnostics.Debug.WriteLine(ex.Message)));
        _commandErrors.Add(GroupRouter.NavigateAndReset.ThrownExceptions.SubscribePrimitives(ex=>System.Diagnostics.Debug.WriteLine(ex.Message)));
        _commandErrors.Add(InputRouter.NavigateAndReset.ThrownExceptions.SubscribePrimitives(ex=>System.Diagnostics.Debug.WriteLine(ex.Message)));
    }
    protected override Task OnLoading(){ShowGroup(ConfigurazioneSection.Operatori);return Task.CompletedTask;}
    private void ShowGroup(ConfigurazioneSection section,int selection=0)
    {
        if(_isClosing)return;
        Dispatcher.UIThread.VerifyAccess();
        ClearInput();
        _groupSubscription?.Dispose();_groupExecution?.Dispose();_section=section;
        var group=_groupFactory(this,section,selection);
        _groupSubscription=group.Navigation.SubscribePrimitives(request=>Dispatcher.UIThread.Post(()=>{
            if(!_isClosing&&!HasEditor&&ReferenceEquals(GroupRouter.GetCurrentViewModel(),group))ShowInput(request);
        }));
        _groupExecution=GroupRouter.NavigateAndReset.Execute(group).SubscribePrimitives();
    }
    private void ShowInput(ConfigurazioneNavigation request)
    {
        var input=_inputFactory(this,_section,request.Action,request.RecordId,request.Nome);
        _inputSubscription=input.Closed.SubscribePrimitives(id=>Dispatcher.UIThread.Post(()=>{
            if(_isClosing||!ReferenceEquals(InputRouter.GetCurrentViewModel(),input))return;
            ClearInput();
            if(id!=0)ShowGroup(_section,id>0?id:0);
        }));
        SetEditor(true);
        _inputExecution=InputRouter.NavigateAndReset.Execute(input).SubscribePrimitives();
    }
    private void ClearInput()
    {
        _inputSubscription?.Dispose();_inputSubscription=null;
        _inputExecution?.Dispose();_inputExecution=null;
        InputRouter.NavigationStack.Clear();SetEditor(false);
    }
    private void SetEditor(bool value)
    {
        _hasEditor=value;_canNavigate.Value=!value&&!_isClosing;
        foreach(var name in new[]{nameof(HasEditor),nameof(GroupEnabled),nameof(IsGroupVisible)})this.RaisePropertyChanged(name);
    }
    private void BackToMenu(){if(HasEditor||_isClosing)return;_isClosing=true;_canNavigate.Value=false;_toMenu.OnNext(RxVoid.Default);}
    protected override Task OnEsc()
    {
        if(HasEditor&&InputRouter.GetCurrentViewModel() is IConfigurazioneInputViewModel input)input.Cancel();
        else BackToMenu();
        return Task.CompletedTask;
    }
    protected override void OnFinalDestruction()
    {
        _isClosing=true;ClearInput();
        _groupSubscription?.Dispose();_groupSubscription=null;_groupExecution?.Dispose();_groupExecution=null;
        GroupRouter.NavigationStack.Clear();
        foreach(var s in _commandErrors)s.Dispose();_commandErrors.Clear();
        SectionCommand.Dispose();MenuCommand.Dispose();_canNavigate.Dispose();_toMenu.Dispose();
        foreach(var router in new[]{GroupRouter,InputRouter}){router.NavigateAndReset.Dispose();router.Navigate.Dispose();router.NavigateBack.Dispose();}
        base.OnFinalDestruction();
    }
}
