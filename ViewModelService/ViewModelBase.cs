using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Signals;
using System.Diagnostics;

namespace ViewModels
{
    /// <summary>
    /// Base comune per i ViewModel della nuova soluzione Leonardo18.
    /// Usa i tipi e gli operatori di ReactiveUI.Primitives, senza System.Reactive.
    /// </summary>
    /// 
    public abstract partial class ViewModelBase : ReactiveObject, IRoutableViewModel, IActivatableViewModel
    {
        private readonly StateSignal<bool> _loadingState = new(false);
        private readonly List<IDisposable> _ownedDisposables = [];
        private readonly object _disposablesLock = new();
        private bool _isLoading;
        private bool _isWaiting;
        private string _waitMessage = string.Empty;
        private int _resourcesDisposed;

        public string UrlPathSegment { get; }
        public IScreen HostScreen { get; }
        public ViewModelActivator Activator { get; } = new();

        // I comandi senza input/output usano RxVoid in ReactiveUI.Primitives.
        public ReactiveCommand<RxVoid, RxVoid> LoadCommand { get; }
        public ReactiveCommand<RxVoid, RxVoid> SaveCommand { get; }
        public ReactiveCommand<RxVoid, RxVoid> EscPressedCommand { get; }
        public ReactiveCommand<RxVoid, RxVoid> AppExitCommand { get; }

        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (_isLoading == value) return;
                this.RaiseAndSetIfChanged(ref _isLoading, value);
                _loadingState.Value = value;
            }
        }

        public bool IsWaiting
        {
            get => _isWaiting;
            protected set => this.RaiseAndSetIfChanged(ref _isWaiting, value);
        }

        public string WaitMessage
        {
            get => _waitMessage;
            protected set => this.RaiseAndSetIfChanged(ref _waitMessage, value);
        }
        protected bool _isClosing;
        protected CancellationTokenSource _cts = new();
        protected CancellationToken Token => _cts.Token;

        protected virtual IObservable<bool> CanSave => Signal.Return(true);
        protected virtual IObservable<bool> CanEsc => Signal.Return(true);

        /// <summary>
        /// La valutazione è differita: i comandi esistono già quando la View si attiva.
        /// </summary>
        protected virtual IObservable<bool> IsAnythingExecuting =>
            Signal.Defer(() =>
                LoadCommand.IsExecuting
                    .Prepend(false)
                    .SyncLatest(SaveCommand.IsExecuting.Prepend(false), static (load, save) => load || save)
                    .SyncLatest(EscPressedCommand.IsExecuting.Prepend(false), static (loadOrSave, esc) => loadOrSave || esc)
                    .Unique());


        protected ViewModelBase(IScreen? hostScreen = null, string? urlPathSegment = null)
        {
            Debug.WriteLine($"***** [VM] {GetType().Name} {GetHashCode()} caricato *****");

            HostScreen = hostScreen!;
            UrlPathSegment = urlPathSegment ?? GetType().Name;

            // Lo stato parte da false e viene aggiornato all'attivazione, dopo la costruzione
            // completa del ViewModel derivato. StateSignal fornisce lo stato iniziale ai comandi.
            var delayedCanExecute = _loadingState
                .Map(static loading => !loading)
                .Calm(TimeSpan.FromMilliseconds(100))
                .WitnessOn(RxSchedulers.MainThreadScheduler);

            var disableImmediately = _loadingState
            .Keep(static loading => loading)
            .Map(static _ => false);

            // Lo stato parte da false e viene aggiornato all'attivazione, dopo la costruzione
            // completa del ViewModel derivato. StateSignal fornisce lo stato iniziale ai comandi.
            var canExecuteGeneral = Signal
            .Blend(delayedCanExecute, disableImmediately)
            .Prepend(true);

            var canSaveEffective = canExecuteGeneral
                .SyncLatest(CanSave, static (general, canSave) => general && canSave);

            var canEscEffective = canExecuteGeneral
                .SyncLatest(CanEsc, static (general, canEsc) => general && canEsc);

            LoadCommand = ReactiveCommand.CreateFromTask(ExecuteLoading, canExecuteGeneral);
            SaveCommand = ReactiveCommand.CreateFromTask(ExecuteSaving, canSaveEffective);
            EscPressedCommand = ReactiveCommand.CreateFromTask(ExecuteEscing, canEscEffective);
            AppExitCommand = ReactiveCommand.Create(OnAppShutDown);

            Track(LoadCommand.ThrownExceptions
                .SubscribePrimitives(ex => Debug.WriteLine($"***** [VM] {GetType().Name} Errore nel caricamento: {ex.Message}"))
                );
            Track(SaveCommand.ThrownExceptions
                .SubscribePrimitives(ex => Debug.WriteLine($"***** [VM] {GetType().Name} Errore nel salvataggio: {ex.Message}"))
                );
            Track(EscPressedCommand.ThrownExceptions
                .SubscribePrimitives(ex => Debug.WriteLine($"***** [VM] {GetType().Name} Errore tasto ESC: {ex.Message}"))
                );

            this.WhenActivated(registerDisposable =>
            {
                registerDisposable(IsAnythingExecuting.SubscribePrimitives(value => IsLoading = value));
                registerDisposable(LoadCommand.Execute().SubscribePrimitives());
                registerDisposable(new DelegateDisposable(() =>
                {
                    try
                    {
                        CancelWork();
                        OnFinalDestruction();
                    }
                    finally
                    {
                        DisposeBaseResources();
                    }
                }));
            });
        }

        private async Task ExecuteLoading()
        {
            if (_isClosing) return;
            try { await OnLoading(); }
            catch (OperationCanceledException) { Debug.WriteLine("[VM] Caricamento annullato."); }
            catch (Exception ex) { Debug.WriteLine($"[VM] Errore caricamento: {ex.Message}"); }
        }

        private async Task ExecuteSaving()
        {
            if (_isClosing) return;
            try { await OnSaving(); }
            catch (OperationCanceledException) { Debug.WriteLine("[VM] Salvataggio annullato."); }
            catch (Exception ex) { Debug.WriteLine($"[VM] Errore salvataggio: {ex.Message}"); }
        }

        private async Task ExecuteEscing()
        {
            if (_isClosing) return;
            try
            {
                await Task.Delay(50, Token);
                await OnEsc();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Debug.WriteLine($"[VM] Errore ESC: {ex.Message}"); }
        }

        protected virtual Task OnLoading() => Task.CompletedTask;
        protected virtual Task OnSaving() => Task.CompletedTask;
        protected virtual Task OnEsc() => Task.CompletedTask;

        protected virtual void OnFinalDestruction()
        {
            Debug.WriteLine($"***** [VM] {GetType().Name} {GetHashCode()} rimosso dallo stack *****");
        }

        protected void OnAppShutDown()
        {
            _isClosing = true;
            if (Avalonia.Application.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lifetime)
            {
                lifetime.Shutdown();
            }
        }

        protected Task SetFocus(Interaction<RxVoid, RxVoid> focusInteraction, int delay = 100) =>
                                TriggerInteraction(focusInteraction, RxVoid.Default, delay);

        protected async Task TriggerInteraction<TInput, TOutput>(Interaction<TInput, TOutput> interaction,
                                                                 TInput input, int delayMs = 200)
        {
            try
            {
                await Task.Delay(delayMs, Token).ConfigureAwait(false);
                var resultSource = new TaskCompletionSource<Task<TOutput>>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                RxSchedulers.MainThreadScheduler.Schedule(new DelegateWorkItem(() =>
                {
                    try
                    {
                        resultSource.TrySetResult(interaction.Handle(input));
                    }
                    catch (Exception ex)
                    {
                        resultSource.TrySetException(ex);
                    }
                }));

                var resultTask = await resultSource.Task.ConfigureAwait(false);
                await resultTask.ConfigureAwait(false);
            }
            catch (UnhandledInteractionException<TInput, TOutput>)
            {
                Debug.WriteLine($">>> [WARN] Interaction {typeof(TInput).Name}->{typeof(TOutput).Name} non gestita (View disattivata).");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($">>> [ERROR] Errore imprevisto nell'Interaction: {ex.Message}");
            }
        }

        private void CancelWork()
        {
            try
            {
                if (!_cts.IsCancellationRequested)
                    _cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // La View può essere disattivata più di una volta.
            }
        }

        private void DisposeBaseResources()
        {
            if (Interlocked.Exchange(ref _resourcesDisposed, 1) != 0)
                return;

            _isClosing = true;
            CancelWork();
            LoadCommand.Dispose(); SaveCommand.Dispose(); EscPressedCommand.Dispose(); AppExitCommand.Dispose();
            _loadingState.Dispose();
            _cts.Dispose();

            lock (_disposablesLock)
            {
                foreach (var disposable in _ownedDisposables)
                    disposable.Dispose();
                _ownedDisposables.Clear();
            }
        }

        private void Track(IDisposable disposable)
        {
            lock (_disposablesLock)
            {
                if (Volatile.Read(ref _resourcesDisposed) != 0)
                    disposable.Dispose();
                else
                    _ownedDisposables.Add(disposable);
            }
        }

        private sealed class DelegateDisposable(Action dispose) : IDisposable
        {
#if DEBUG
    ~DelegateDisposable() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

            private Action? _dispose = dispose;
            public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
        }

        private sealed class DelegateWorkItem(Action action) : IWorkItem
        {
#if DEBUG
    ~DelegateWorkItem() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

            public void Execute() => action();
        }

#if DEBUG
        ~ViewModelBase() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    }
}
