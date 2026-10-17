using Avalonia.Threading;
using AppServices.Database;
using Login.Contracts;
using Login.Core.Repository;
using Login.ViewModels.Map;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
using System.Collections.ObjectModel;
using ViewModels;
namespace Login.ViewModels;

public sealed class LoginViewModel : ViewModelBase, ILoginViewModel
{
    private readonly ILoginRepository _repository;
    private readonly Signal<RxVoid> _loginSuccesso = new();
    // CanSave is read by the base constructor: lazily create the state before that subscription.
    private StateSignal<bool>? _canLogin;
    private StateSignal<bool> CanLoginState => _canLogin ??= new(false);
    private string _passwordText = "", _infoError = "";
    private LoginMap? _bindingT;
    private bool _isBusy, _isDataLoaded;
    public ObservableCollection<LoginMap> DataSource { get; } = [];
    public IObservable<RxVoid> LoginSuccesso => _loginSuccesso;
    public Interaction<RxVoid, RxVoid> PasswordFocus { get; } = new();
    protected override IObservable<bool> CanSave => CanLoginState;
    public string PasswordText
    {
        get => _passwordText;
        set { this.RaiseAndSetIfChanged(ref _passwordText, value); InfoError = ""; UpdateCanLogin(); }
    }
    public LoginMap? BindingT
    {
        get => _bindingT;
        set { this.RaiseAndSetIfChanged(ref _bindingT, value); InfoError = ""; UpdateCanLogin(); }
    }
    public string InfoError { get => _infoError; private set => this.RaiseAndSetIfChanged(ref _infoError, value); }
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value) return;
            this.RaiseAndSetIfChanged(ref _isBusy, value);
            this.RaisePropertyChanged(nameof(IsUiReady)); UpdateCanLogin();
        }
    }
    public bool IsDataLoaded
    {
        get => _isDataLoaded;
        private set { this.RaiseAndSetIfChanged(ref _isDataLoaded, value); this.RaisePropertyChanged(nameof(IsUiReady)); }
    }
    public bool IsUiReady => IsDataLoaded && !IsBusy;
    public LoginViewModel(ILoginRepository? repository = null, IScreen? host = null) : base(host)
    { _repository = repository ?? new LoginRepository(); UpdateCanLogin(); }
    private void UpdateCanLogin() => CanLoginState.Value =
        !_isClosing && !IsBusy && BindingT is not null && !string.IsNullOrEmpty(PasswordText);

    protected override Task OnLoading() => RunOperation(async () =>
    {
        IsDataLoaded = false;
        var operators = await _repository.GetOperatoriAbilitati(Token);
        Token.ThrowIfCancellationRequested();
        DataSource.Clear();
        foreach (var dto in operators) DataSource.Add(new LoginMap(dto));
        BindingT = DataSource.FirstOrDefault();
        IsDataLoaded = true;
        if (DataSource.Count == 0) InfoError = "Nessun operatore abilitato.";
    }, "CARICAMENTO OPERATORI IN CORSO...");

    protected override Task OnSaving() => RunOperation(async () =>
    {
        if (BindingT is null || string.IsNullOrEmpty(PasswordText)) return;
        await _repository.SaveSettings(BindingT.ToDto(), PasswordText, Token);
        Token.ThrowIfCancellationRequested();
        WaitMessage = "APERTURA SESSIONE IN CORSO...";
        PasswordText = "";
        _isClosing = true; UpdateCanLogin();
        await Dispatcher.UIThread.InvokeAsync(() => _loginSuccesso.OnNext(RxVoid.Default));
    }, "VERIFICA CREDENZIALI IN CORSO...");

    private async Task RunOperation(Func<Task> action, string message)
    {
        if (_isClosing || IsBusy) return;
        WaitMessage = message;
        IsBusy = true; IsWaiting = true; InfoError = "";
        try { await action(); }
        catch (OperationCanceledException) { }
        catch (InvalidOperationException ex) { InfoError = ex.Message; }
        catch (DatabaseHostException ex) { InfoError = ex.Message; }
        catch (Exception) { InfoError = "Accesso non riuscito. Verifica la connessione al database."; }
        finally
        {
            IsBusy = false; IsWaiting = false; WaitMessage = "";
            if (!_isClosing && IsDataLoaded) await SetFocus(PasswordFocus, 100);
        }
    }
    protected override Task OnEsc() { OnAppShutDown(); return Task.CompletedTask; }
    protected override void OnFinalDestruction()
    {
        _loginSuccesso.Dispose();
        PasswordText = "";
        base.OnFinalDestruction();
    }
}