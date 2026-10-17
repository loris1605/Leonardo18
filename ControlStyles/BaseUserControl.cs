using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using ReactiveUI.Binding;

namespace Views;

/// <summary>
/// Typed Avalonia view with explicit activation; no reflected ViewModel discovery.
/// </summary>
public abstract class BaseUserControl<TViewModel> : UserControl, IViewFor<TViewModel>
    where TViewModel : class, IActivatableViewModel
{
#if DEBUG
    ~BaseUserControl() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    private IDisposable? _viewResources;
    private IDisposable? _activation;
    private bool _isAttached;

    public TViewModel? ViewModel
    {
        get => DataContext as TViewModel;
        set => DataContext = value;
    }

    object? IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = (TViewModel?)value;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataContextProperty && _isAttached) BeginActivation();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        BeginActivation();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        EndActivation();
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>Register view interactions before the ViewModel starts loading.</summary>
    protected virtual IDisposable? OnViewActivated(TViewModel viewModel) => null;

    private void BeginActivation()
    {
        EndActivation();
        if (ViewModel is not { } viewModel || Design.IsDesignMode) return;
        _viewResources = OnViewActivated(viewModel);
        _activation = viewModel.Activator.Activate();
    }

    private void EndActivation()
    {
        var activation = _activation; _activation = null;
        activation?.Dispose();
        var resources = _viewResources; _viewResources = null;
        resources?.Dispose();
    }
}