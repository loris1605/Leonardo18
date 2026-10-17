using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Controls.Presenters;
using System.Linq;
using Login.ViewModels;
using Menu.ViewModels;
using ReactiveUI;
using Soci.ViewModels;
using System;
using System.Collections.Specialized;
using ViewModels;
namespace Leonardo18.Views;

/// <summary>Typed router host: always builds a fresh view, without reflective view location.</summary>
public sealed class MainRouterHost : TransitioningContentControl
{
#if DEBUG
    ~MainRouterHost() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif
    private bool _transitionRunning, _routePending;
    private ContentPresenter? _secondaryPresenter;
    protected override bool RegisterContentPresenter(ContentPresenter presenter)
    {
        var registered = base.RegisterContentPresenter(presenter);
        if (registered && presenter.Name == "PART_ContentPresenter2")
            _secondaryPresenter = presenter;
        return registered;
    }
    public MainRouterHost()
    {
        TransitionCompleted += OnTransitionCompleted;
    }
    private void OnTransitionCompleted(object? sender, TransitionCompletedEventArgs args)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // A cancelled older transition must not clear an ongoing newer transition.
            if (!ReferenceEquals(Content, args.To)) return;
            _transitionRunning = false;
            foreach (var presenter in new[] { Presenter, _secondaryPresenter }.Where(p => p is not null && !ReferenceEquals(p.Content, Content)))
            {
                presenter!.Content = null;
                presenter.IsVisible = false;
                presenter.UpdateChild();
            }
            if (_routePending && _attached) { _routePending = false; DisplayCurrent(); }
        });
    }
    protected override Type StyleKeyOverride => typeof(TransitioningContentControl);
    public static readonly StyledProperty<RoutingState?> RouterProperty =
        AvaloniaProperty.Register<MainRouterHost, RoutingState?>(nameof(Router));
    public static readonly StyledProperty<Control?> DefaultContentProperty =
        AvaloniaProperty.Register<MainRouterHost, Control?>(nameof(DefaultContent));
    public Control? DefaultContent
    {
        get => GetValue(DefaultContentProperty);
        set => SetValue(DefaultContentProperty, value);
    }
    private bool _arranging;
    protected override Size ArrangeOverride(Size finalSize)
    {
        _arranging = true;
        try { return base.ArrangeOverride(finalSize); }
        finally { _arranging = false; }
    }
    private bool _displayPending;
    private RoutingState? _subscribedRouter;
    private IRoutableViewModel? _displayed;
    private bool _attached;
    public RoutingState? Router { get => GetValue(RouterProperty); set => SetValue(RouterProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RouterProperty && _attached) SubscribeRouter();
        if (change.Property == DefaultContentProperty && _attached && _displayed is null)
            Content = DefaultContent;
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        SubscribeRouter();
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        UnsubscribeRouter();
        base.OnDetachedFromVisualTree(e);
    }
    private void SubscribeRouter()
    {
        UnsubscribeRouter();
        _subscribedRouter = Router;
        if (_subscribedRouter is null) { Content = DefaultContent; return; }
        _subscribedRouter.NavigationStack.CollectionChanged += OnStackChanged;
        DisplayCurrent();
    }
    private void UnsubscribeRouter()
    {
        if (_subscribedRouter is { } previous)
            previous.NavigationStack.CollectionChanged -= OnStackChanged;
        _transitionRunning = false; _routePending = false;
        _subscribedRouter = null;
        _displayed = null;
        Content = null; // Release content when the host is detached or its router is replaced.
    }
    private void OnStackChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (_displayPending) return;
        _displayPending = true;
        // NavigateAndReset clears and fills the stack: animate only its final state.
        Dispatcher.UIThread.Post(() =>
        {
            _displayPending = false;
            if (_attached) DisplayCurrent();
        });
    }
    private void DisplayCurrent()
    {
        if (_transitionRunning) { _routePending = true; return; }
        if (_arranging)
        {
            OnStackChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            return;
        }
        var vm = _subscribedRouter?.GetCurrentViewModel();
        if (ReferenceEquals(vm, _displayed) && (vm is not null || ReferenceEquals(Content, DefaultContent))) return;
        if (vm is null)
        {
            _displayed = null;
            Content = DefaultContent;
            return;
        }
        Control view = vm switch
        {
            ConnectionViewModel connection => new global::Views.ConnectionView { ViewModel = connection },
            LoginViewModel login => new global::Login.Views.LoginView { ViewModel = login },
            MenuViewModel menu => new global::Menu.Views.MenuView { ViewModel = menu },
            global::Cassa.ViewModels.CassaViewModel cassa => new global::Cassa.Views.CassaView { ViewModel = cassa },
            SociViewModel soci => new global::Soci.Views.SociView { ViewModel = soci },
            global::Report.ViewModels.ReportViewModel report => new global::Report.Views.ReportView { ViewModel=report },
            global::Servizi.ViewModels.ServiziViewModel servizi => new global::Servizi.Views.ServiziView { ViewModel = servizi },
            global::Configurazione.ViewModels.ConfigurazioneViewModel configurazione => new global::Configurazione.Views.ConfigurazioneView { ViewModel = configurazione },
            _ => throw new InvalidOperationException("Vista non registrata nel contenitore principale.")
        };
        var sameModule = _displayed?.GetType() == vm.GetType();
        _displayed = vm;
        var transition = PageTransition;
        // Replacing the same module must release its previous activation immediately.
        if (sameModule) SetCurrentValue(PageTransitionProperty, null);
        try
        {
            _transitionRunning = PageTransition is not null && Presenter is not null && _secondaryPresenter is not null;
            Content = view;
        }
        finally
        {
            if (sameModule) SetCurrentValue(PageTransitionProperty, transition);
        }
    }
}

