using Microsoft.Extensions.DependencyInjection;
using Avalonia.Controls;
using System;
using ViewModels;
namespace Leonardo18;

public partial class MainWindow : Window
{
#if DEBUG
    ~MainWindow() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif
    private IDisposable? _activation;
    public MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;
    public MainWindow() : this(Program.Services.GetRequiredService<MainWindowViewModel>()) { }
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (!Design.IsDesignMode && _activation is null && ViewModel is { } vm)
            _activation = vm.Activator.Activate();
    }
    protected override void OnClosed(EventArgs e)
    {
        _activation?.Dispose(); _activation = null;
        ModuleHost.Router = null;
        DataContext = null;
        base.OnClosed(e);
    }
}

