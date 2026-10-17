using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using ReactiveUI.Primitives;
using ViewModels;
using System.Windows.Input;

namespace Views;

public partial class ConnectionView : BaseUserControl<ConnectionViewModel>
{
    private bool _focusUserNameAfterSelection;

    public ConnectionView()
    {
        InitializeComponent();
        SqlCombo.DropDownClosed += OnServerDropDownClosed;
    }

    protected override IDisposable OnViewActivated(ConnectionViewModel viewModel) =>
        viewModel.UserIdFocus.RegisterHandler(async interaction =>
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (viewModel.IsUiReady)
                {
                    UserNameText.Focus();
                    UserNameText.SelectAll();
                }
            }, DispatcherPriority.Background);
            interaction.SetOutput(RxVoid.Default);
        });

    private void OnServerSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (SqlCombo.SelectedItem is not string server || ViewModel is not { } vm) return;
        vm.SelectedInstance = server;
        _focusUserNameAfterSelection = true;
        if (!SqlCombo.IsDropDownOpen) FocusUserNameAfterSelection();
    }

    private void OnServerDropDownClosed(object? sender, EventArgs e)
    {
        if (_focusUserNameAfterSelection) FocusUserNameAfterSelection();
    }

    private void FocusUserNameAfterSelection()
    {
        _focusUserNameAfterSelection = false;
        Dispatcher.UIThread.Post(() =>
        {
            if (ViewModel is { IsUiReady: true })
            {
                UserNameText.Focus();
                UserNameText.SelectAll();
            }
        }, DispatcherPriority.Background);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.F5 && ViewModel is { } vm)
        {
            e.Handled = true;
            if (((ICommand)vm.F5Command).CanExecute(null))
                ((ICommand)vm.F5Command).Execute(null);
        }
    }
}