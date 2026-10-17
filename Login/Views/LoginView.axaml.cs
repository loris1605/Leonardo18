using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Login.ViewModels;
using ReactiveUI.Primitives;
using System.Windows.Input;
using Views;
namespace Login.Views;

public partial class LoginView : BaseUserControl<LoginViewModel>
{
    public LoginView()
    {
        InitializeComponent();
        OperatoreCombo.DropDownClosed += (_, _) => Dispatcher.UIThread.Post(FocusPassword, DispatcherPriority.Background);
        PasswordBox.AddHandler(InputElement.KeyDownEvent, OnPasswordKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
    }
    protected override IDisposable OnViewActivated(LoginViewModel viewModel) =>
        viewModel.PasswordFocus.RegisterHandler(async interaction =>
        {
            await Dispatcher.UIThread.InvokeAsync(FocusPassword, DispatcherPriority.Background);
            interaction.SetOutput(RxVoid.Default);
        });
    private void FocusPassword()
    {
        if (ViewModel is { IsUiReady: true }) { PasswordBox.Focus(); PasswordBox.SelectAll(); }
    }
    private void OnPasswordKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ViewModel is { } vm)
        {
            e.Handled = true;
            if (((ICommand)vm.SaveCommand).CanExecute(null)) ((ICommand)vm.SaveCommand).Execute(null);
        }
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && ViewModel is { } vm)
        {
            e.Handled = true;
            if (((ICommand)vm.EscPressedCommand).CanExecute(null)) ((ICommand)vm.EscPressedCommand).Execute(null);
        }
    }
}