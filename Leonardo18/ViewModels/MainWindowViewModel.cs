using Servizi.Contracts;
using Cassa.Contracts;
using Configurazione.Contracts;
using Connection.Contracts;
using Avalonia.Threading;
using Login.Contracts;
using Menu.Contracts;
using Models.Entity.Global;
using ReactiveUI;
using ReactiveUI.Primitives;
using Soci.Contracts;
using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
namespace ViewModels;

/// <summary>Main screen: each navigation creates a new module and resets the previous stack.</summary>
public sealed class MainWindowViewModel : ViewModelBase, IScreen
{
    private readonly Func<IScreen, bool, IConnectionViewModel> _connectionFactory;
    private readonly Func<IScreen, ILoginViewModel> _loginFactory;
    private readonly Func<IScreen, IMenuViewModel> _menuFactory;
    private readonly Func<IScreen, ISociViewModel> _sociFactory;
    private readonly Func<IScreen, IConfigurazioneViewModel> _configurazioneFactory;
    private readonly Func<IScreen, int, ICassaViewModel> _cassaFactory;
    private readonly Func<IScreen, IServiziViewModel> _serviziFactory;
    private readonly Func<IScreen, global::Report.Contracts.IReportViewModel> _reportFactory;
    private readonly IDisposable _routeErrors;
    private IDisposable? _moduleSubscription, _routingExecution;
    private bool _initialized;
    public RoutingState Router { get; } = new();
    public IRoutableViewModel? CurrentViewModel => Router.GetCurrentViewModel();

    public MainWindowViewModel(
        Func<IScreen, bool, IConnectionViewModel> connectionFactory,
        Func<IScreen, ILoginViewModel> loginFactory,
        Func<IScreen, IMenuViewModel> menuFactory,
        Func<IScreen, ISociViewModel> sociFactory,
        Func<IScreen, IConfigurazioneViewModel> configurazioneFactory,
        Func<IScreen, int, ICassaViewModel> cassaFactory, Func<IScreen, IServiziViewModel> serviziFactory, Func<IScreen, global::Report.Contracts.IReportViewModel> reportFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _loginFactory = loginFactory ?? throw new ArgumentNullException(nameof(loginFactory));
        _menuFactory = menuFactory ?? throw new ArgumentNullException(nameof(menuFactory));
        _sociFactory = sociFactory ?? throw new ArgumentNullException(nameof(sociFactory));
        _configurazioneFactory = configurazioneFactory ?? throw new ArgumentNullException(nameof(configurazioneFactory));
        _cassaFactory = cassaFactory ?? throw new ArgumentNullException(nameof(cassaFactory));
        _serviziFactory = serviziFactory ?? throw new ArgumentNullException(nameof(serviziFactory));
        _reportFactory=reportFactory;
        Router.NavigationStack.CollectionChanged += OnStackChanged;
        _routeErrors = Router.NavigateAndReset.ThrownExceptions.SubscribePrimitives(
            error => System.Diagnostics.Debug.WriteLine($"[MAIN ROUTER] {error.Message}"));
    }
    protected override Task OnLoading()
    {
        if (!_initialized)
        {
            _initialized = true;
            ShowConnection();
        }
        return Task.CompletedTask;
    }
    public void ShowConnection(bool autoConnect = true)
    {
        if (_isClosing) return;
        ClearSession();
        var vm = _connectionFactory(this, autoConnect);
        ResetTo(vm, () => vm.ConnectionToLogin.SubscribePrimitives(_ => RequestNavigation(vm, ShowLogin)));
    }
    public void ShowLogin()
    {
        if (_isClosing) return;
        ClearSession();
        var vm = _loginFactory(this);
        ResetTo(vm, () => vm.LoginSuccesso.SubscribePrimitives(_ => RequestNavigation(vm, ShowMenu)));
    }
    public void ShowMenu()
    {
        if (_isClosing) return;
        var vm = _menuFactory(this);
        ResetTo(vm, () => vm.Navigation.SubscribePrimitives(destination => RequestNavigation(vm, () =>
        {
            switch (destination)
            {
                case "connection": ShowConnection(autoConnect: false); break;
                case "soci": ShowSoci(); break;
                case "servizi": ShowServizi(); break;
                case "report": ShowReport(); break;
                case "cassa": ShowCassa(); break;
                case "configurazione": ShowConfigurazione(); break;
                case "login": ShowLogin(); break;
            }
        })));
    }
    public void ShowReport()
    {
        if(_isClosing)return;
        var vm=_reportFactory(this);
        ResetTo(vm,()=>vm.ReportToMenu.SubscribePrimitives(_=>RequestNavigation(vm,ShowMenu)));
    }
    public void ShowServizi()
    {
        if (_isClosing) return;
        var vm = _serviziFactory(this);
        ResetTo(vm, () => vm.ServiziToMenu.SubscribePrimitives(_ => RequestNavigation(vm, ShowMenu)));
    }
    public void ShowSoci()
    {
        if (_isClosing) return;
        var vm = _sociFactory(this);
        ResetTo(vm, () => vm.SociToMenu.SubscribePrimitives(_ => RequestNavigation(vm, ShowMenu)));
    }
    public void ShowConfigurazione()
    {
        if (_isClosing) return;
        var vm = _configurazioneFactory(this);
        ResetTo(vm, () => vm.ConfigurazioneToMenu.SubscribePrimitives(_ => RequestNavigation(vm, ShowMenu)));
    }
    public void ShowCassa()
    {
        if (_isClosing || GlobalValuesC.MyPostazione is not { } station) return;
        var vm = _cassaFactory(this, station.CODICEPOSTAZIONE);
        ResetTo(vm, () => vm.CassaToMenu.SubscribePrimitives(_ => RequestNavigation(vm, ShowMenu)));
    }
    private void RequestNavigation(IRoutableViewModel source, Action navigate)
    {
        // Defer transitions until the current command completes. Ignore stale/duplicate module events.
        Dispatcher.UIThread.Post(() =>
        {
            if (!_isClosing && ReferenceEquals(CurrentViewModel, source)) navigate();
        });
    }
    private void ResetTo(IRoutableViewModel vm, Func<IDisposable> subscribe)
    {
        Dispatcher.UIThread.VerifyAccess();
        _moduleSubscription?.Dispose(); _moduleSubscription = null;
        _routingExecution?.Dispose(); _routingExecution = null;
        _moduleSubscription = subscribe();
        _routingExecution = Router.NavigateAndReset.Execute(vm).SubscribePrimitives();
    }
    private void OnStackChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        this.RaisePropertyChanged(nameof(CurrentViewModel));
#if DEBUG
        System.Diagnostics.Debug.WriteLine($"[MAIN ROUTER] Stack: {Router.NavigationStack.Count}; current: {CurrentViewModel?.GetType().Name ?? "nessuno"}");
#endif
    }
    private static void ClearSession()
    {
        GlobalValuesC.MySetting = null;
        GlobalValuesC.MyPostazione = null;
    }
    protected override void OnFinalDestruction()
    {
        _isClosing = true;
        _moduleSubscription?.Dispose(); _moduleSubscription = null;
        _routingExecution?.Dispose(); _routingExecution = null;
        Router.NavigationStack.Clear();
        Router.NavigationStack.CollectionChanged -= OnStackChanged;
        _routeErrors.Dispose();
        Router.NavigateAndReset.Dispose(); Router.Navigate.Dispose(); Router.NavigateBack.Dispose();
        base.OnFinalDestruction();
    }
}

