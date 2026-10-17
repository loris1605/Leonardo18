using Avalonia.Threading;
using Connection.Contracts;
using Connection.Core.Services;
using System.Data.Common;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
using System.Collections.ObjectModel;

namespace ViewModels;

public class ConnectionViewModel : ViewModelBase, IConnectionViewModel
{
    private readonly IConnectionService _service;
    private readonly Func<string> _readConnection;
    private readonly Action<string> _saveConnection;
    private readonly bool _autoConnect;
    private readonly Signal<RxVoid> _connectionToLogin = new();
    private readonly StateSignal<bool> _busyState = new(false);
    private readonly StateSignal<bool> _inputValidState = new(false);
    private readonly StateSignal<bool> _schemaActionState = new(false);
    private readonly StateSignal<bool> _readyState = new(false);
    private readonly List<IDisposable> _subscriptions = [];
    private readonly SemaphoreSlim _operation = new(1, 1);
    private string _selectedInstance = "", _userIdText = "", _passwordText = "", _databaseText = "", _errorText = "";
    private bool _isDataLoaded, _isBusy, _avviaVisibile;
    private string? _verifiedConnection;
    private ConnectionStatus? _checkedDatabaseStatus;

    public ObservableCollection<string> SqlInstances { get; } = [];
    public IObservable<RxVoid> ConnectionToLogin => _connectionToLogin;
    public Interaction<RxVoid, RxVoid> UserIdFocus { get; } = new();
    public ReactiveCommand<RxVoid, RxVoid> CheckCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> AvviaCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> F5Command { get; }

    public string SelectedInstance { get => _selectedInstance; set { if (_selectedInstance != value && SetField(ref _selectedInstance, value)) InvalidateConnection(); } }
    public string UserIdText { get => _userIdText; set { if (_userIdText != value && SetField(ref _userIdText, value)) InvalidateConnection(); } }
    public string PasswordText { get => _passwordText; set { if (_passwordText != value && SetField(ref _passwordText, value)) InvalidateConnection(); } }
    public string DatabaseText { get => _databaseText; set { if (_databaseText != value && SetField(ref _databaseText, value)) InvalidateConnection(); } }
    public string ErrorText { get => _errorText; private set => this.RaiseAndSetIfChanged(ref _errorText, value); }
    public bool IsDataLoaded { get => _isDataLoaded; private set { if (_isDataLoaded != value && SetField(ref _isDataLoaded, value)) this.RaisePropertyChanged(nameof(IsUiReady)); } }
    public bool IsBusy { get => _isBusy; private set { if (_isBusy != value && SetField(ref _isBusy, value)) { _busyState.Value = value; this.RaisePropertyChanged(nameof(IsUiReady)); } } }
    public bool IsUiReady => IsDataLoaded && !IsBusy;
    public bool AvviaVisibile { get => _avviaVisibile; private set { if (_avviaVisibile != value && SetField(ref _avviaVisibile, value)) _readyState.Value = value; } }

    protected override IObservable<bool> IsAnythingExecuting => Signal.Defer(() =>
        base.IsAnythingExecuting
            .SyncLatest(CheckCommand.IsExecuting.Prepend(false), static (a, b) => a || b)
            .SyncLatest(AvviaCommand.IsExecuting.Prepend(false), static (a, b) => a || b)
            .SyncLatest(F5Command.IsExecuting.Prepend(false), static (a, b) => a || b).Unique());

    public ConnectionViewModel(IScreen? host = null, IConnectionService? service = null,
        Func<string>? readConnection = null, Action<string>? saveConnection = null, bool autoConnect = true) : base(host)
    {
        _autoConnect = autoConnect;
        _service = service ?? new ConnectionService();
        _readConnection = readConnection ?? (() => AppServices.Connection.CurrentConnectionString);
        _saveConnection = saveConnection ?? (value => AppServices.Connection.SetConnectionString(value));
        var canCheck = _inputValidState.SyncLatest(_busyState, static (valid, busy) => valid && !busy);
        CheckCommand = ReactiveCommand.CreateFromTask(() => RunOperation(CheckConnectionAsync, "VERIFICA CONNESSIONE IN CORSO..."), canCheck);
        AvviaCommand = ReactiveCommand.CreateFromTask(() => RunOperation(GoToLogin, "APERTURA LOGIN IN CORSO..."), _readyState.SyncLatest(_busyState, static (ready, busy) => ready && !busy));
        F5Command = ReactiveCommand.CreateFromTask(() => RunOperation(ApplyMigrationsAsync, "PREPARAZIONE DATABASE IN CORSO..."), _schemaActionState.SyncLatest(_busyState, static (allowed, busy) => allowed && !busy));
        _subscriptions.Add(CheckCommand.ThrownExceptions.SubscribePrimitives(_ => ErrorText = "Errore durante la verifica della connessione."));
        _subscriptions.Add(AvviaCommand.ThrownExceptions.SubscribePrimitives(_ => ErrorText = "Impossibile proseguire."));
        _subscriptions.Add(F5Command.ThrownExceptions.SubscribePrimitives(_ => ErrorText = "Errore durante la creazione o l'aggiornamento del database."));
    }

    public string BuildConnectionString() => new DbConnectionStringBuilder
    {
        ["Server"] = SelectedInstance.Trim(), ["Database"] = DatabaseText.Trim(),
        ["Authentication"] = "Sql Password", ["Integrated Security"] = false,
        ["User ID"] = UserIdText.Trim(), ["Password"] = PasswordText,
        ["TrustServerCertificate"] = true, ["Connect Timeout"] = 5
    }.ConnectionString;

    private static string ConnectionValue(DbConnectionStringBuilder builder, params string[] names)
    {
        foreach (string name in names)
            if (builder.TryGetValue(name, out object? value))
                return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
        return "";
    }

    private bool SetField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        this.RaiseAndSetIfChanged(ref field, value, propertyName);
        return true;
    }

    private void InvalidateConnection()
    {
        _inputValidState.Value = !string.IsNullOrWhiteSpace(SelectedInstance)
            && !string.IsNullOrWhiteSpace(DatabaseText)
            && !string.IsNullOrWhiteSpace(UserIdText)
            && !string.IsNullOrEmpty(PasswordText);
        _verifiedConnection = null;
        _checkedDatabaseStatus = null;
        AvviaVisibile = false;
        _schemaActionState.Value = false;
        AppServices.Flags.ServerAttivo = false;
        AppServices.Flags.DbStatus = AppServices.Flags.DatabaseAttivo.NoDataBase;
    }

    private async Task RunOperation(Func<Task> action, string message)
    {
        if (_isClosing || !await _operation.WaitAsync(0, Token)) return;
        try
        {
            WaitMessage = message;
            IsBusy = true;
            ErrorText = "";
            await action();
        }
        catch (OperationCanceledException) { }
        catch (DatabaseOperationException ex) { ErrorText = ex.Message; }
        catch (Exception) { ErrorText = "Operazione non riuscita. Controlla la configurazione e le migrazioni del database."; }
        finally
        {
            IsBusy = false;
            WaitMessage = "";
            _operation.Release();
            if (!_isClosing && IsDataLoaded)
                await SetFocus(UserIdFocus, 100);
        }
    }

    protected override Task OnLoading() => RunOperation(async () =>
    {
        IsDataLoaded = false;
        string saved = "";
        try { saved = _readConnection(); }
        catch (Exception) { ErrorText = "Impossibile leggere la connessione salvata. Inserisci la configurazione."; }
        if (!string.IsNullOrWhiteSpace(saved))
        {
            try
            {
                var builder = new DbConnectionStringBuilder { ConnectionString = saved };
                SelectedInstance = ConnectionValue(builder, "Server", "Data Source", "Address", "Addr", "Network Address");
                DatabaseText = ConnectionValue(builder, "Database", "Initial Catalog");
                UserIdText = ConnectionValue(builder, "User ID", "UID");
                PasswordText = ConnectionValue(builder, "Password", "PWD");
                string integrated = ConnectionValue(builder, "Integrated Security", "Trusted_Connection");
                string authentication = ConnectionValue(builder, "Authentication");
                if (integrated.Equals("true", StringComparison.OrdinalIgnoreCase)
                    || integrated.Equals("yes", StringComparison.OrdinalIgnoreCase)
                    || integrated.Equals("sspi", StringComparison.OrdinalIgnoreCase)
                    || (authentication.Length > 0 && !authentication.Equals("Sql Password", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Richiesta autenticazione SQL Server con nome utente e password.");
                // Opening from Menu pre-fills the form; verification requires an explicit click.
                if (_autoConnect)
                {
                    string candidate = BuildConnectionString();
                    WaitMessage = "VERIFICA CONNESSIONE SALVATA...";
                    var result = await _service.CheckAsync(candidate, Token);
                    SetVerificationResult(candidate, result);
                    if (result.Status == ConnectionStatus.Ready) { IsDataLoaded = true; await GoToLogin(); return; }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { ErrorText = "La connessione salvata non è disponibile. Verifica la configurazione."; }
        }
        try
        {
            WaitMessage = "RICERCA ISTANZE IN CORSO...";
            var instances = await _service.GetInstancesAsync(Token);
            SqlInstances.Clear();
            foreach (string instance in instances) SqlInstances.Add(instance);
            if (string.IsNullOrWhiteSpace(SelectedInstance) && SqlInstances.Count > 0) SelectedInstance = SqlInstances[0];
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { if (string.IsNullOrEmpty(ErrorText)) ErrorText = "Ricerca server non disponibile: puoi inserire il server manualmente."; }
        IsDataLoaded = true;
    }, "LETTURA CONFIGURAZIONE IN CORSO...");

    private async Task CheckConnectionAsync()
    {
        InvalidateConnection();
        string candidate = BuildConnectionString();
        SetVerificationResult(candidate, await _service.CheckAsync(candidate, Token));
        if (AvviaVisibile) await GoToLogin();
    }

    private async Task ApplyMigrationsAsync()
    {
        if (!_schemaActionState.Value) return;
        _schemaActionState.Value = false;
        WaitMessage = _checkedDatabaseStatus == ConnectionStatus.DatabaseMissing
            ? "CREAZIONE DATABASE IN CORSO..."
            : "AGGIORNAMENTO DATABASE IN CORSO...";
        string candidate = BuildConnectionString();
        try { SetVerificationResult(candidate, await _service.ApplyMigrationsAsync(candidate, Token)); }
        catch { _schemaActionState.Value = true; throw; }
    }

    private void SetVerificationResult(string candidate, ConnectionCheckResult result)
    {
        if (candidate != BuildConnectionString()) { ErrorText = "I dati sono cambiati: ripeti la verifica."; return; }
        Token.ThrowIfCancellationRequested();
        _checkedDatabaseStatus = result.Status;
        ErrorText = result.Message;
        bool ready = result.Status == ConnectionStatus.Ready;
        if (ready) { _saveConnection(candidate); _verifiedConnection = candidate; }
        AvviaVisibile = ready;
        _schemaActionState.Value = result.Status is ConnectionStatus.DatabaseMissing or ConnectionStatus.UpdatesRequired;
        AppServices.Flags.ServerAttivo = true; // Every returned result follows a successful server connection.
        AppServices.Flags.DbStatus = ready ? AppServices.Flags.DatabaseAttivo.SiDataBase
            : result.Status == ConnectionStatus.UpdatesRequired ? AppServices.Flags.DatabaseAttivo.DaAggiornare
            : AppServices.Flags.DatabaseAttivo.NoDataBase;
    }

    private async Task GoToLogin()
    {
        if (_isClosing || _verifiedConnection is null || _verifiedConnection != BuildConnectionString()) return;
        WaitMessage = "APERTURA LOGIN IN CORSO...";
        _isClosing = true;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _connectionToLogin.OnNext(RxVoid.Default);
        });
    }

    protected override void OnFinalDestruction()
    {
        foreach (var subscription in _subscriptions) subscription.Dispose();
        _subscriptions.Clear();
        CheckCommand.Dispose(); AvviaCommand.Dispose(); F5Command.Dispose();
        _connectionToLogin.Dispose();
        // A running operation may still finish after cancellation and release its semaphore.
        base.OnFinalDestruction();
    }
}