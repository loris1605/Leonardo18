using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Input;
using Avalonia.Interactivity;
using AppServices.Database;
using Cassa.ViewModels;
using ReactiveUI.Primitives;
using System.Windows.Input;
using Views;
namespace Cassa.Views;

public partial class CassaView : BaseUserControl<CassaViewModel>
{
    public CassaView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => UpdateHeader();
        PosizioneBox.AddHandler(InputElement.KeyDownEvent, OnPositionKey, RoutingStrategies.Tunnel);
        TesseraBox.AddHandler(InputElement.KeyDownEvent, OnCardKey, RoutingStrategies.Tunnel);
        IngressoPosizioneBox.AddHandler(InputElement.KeyDownEvent, OnEntryKey, RoutingStrategies.Tunnel);
        SociList.DoubleTapped += (_, _) => Execute(ViewModel?.OpenSelectedCommand);
    }
    private void UpdateHeader()
    {
        var compact=Bounds.Width < 850;
        Grid.SetColumnSpan(TitleControl, compact ? 2 : 1);
        Grid.SetColumn(MenuButton, compact ? 0 : 1);
        Grid.SetRow(MenuButton, compact ? 1 : 0);
        MenuButton.HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Right;

    }
    protected override IDisposable OnViewActivated(CassaViewModel vm) => new FocusHandlers(
        vm.UscitaFocus.RegisterHandler(interaction => {
            var input=this.GetVisualDescendants().OfType<UscitaSocioView>().First().FindControl<TextBox>("ExitPositionBox")!;
            input.Focus();input.SelectAll();interaction.SetOutput(RxVoid.Default);return Task.CompletedTask;
        }),
        vm.PositionFocus.RegisterHandler(interaction => { PosizioneBox.Focus(); PosizioneBox.SelectAll(); interaction.SetOutput(RxVoid.Default); return Task.CompletedTask; }),
        vm.CardFocus.RegisterHandler(interaction => { TesseraBox.Focus(); TesseraBox.SelectAll(); interaction.SetOutput(RxVoid.Default); return Task.CompletedTask; }),
        vm.AbbonamentoCardFocus.RegisterHandler(interaction => {
            this.GetVisualDescendants().OfType<AcquistoAbbonamentoView>().First().FindControl<TextBox>("FirstCardBox")!.Focus();
            interaction.SetOutput(RxVoid.Default); return Task.CompletedTask;
        }),
        vm.EntryPositionFocus.RegisterHandler(interaction => { IngressoPosizioneBox.Focus(); interaction.SetOutput(RxVoid.Default); return Task.CompletedTask; }));

    private void OnPositionKey(object? sender, KeyEventArgs e)
    { if (e.Key == Key.Enter) { e.Handled=true; Execute(ViewModel?.OpenSheetCommand); } }
    private void OnCardKey(object? sender, KeyEventArgs e)
    { if (e.Key == Key.Enter) { e.Handled=true; Execute(ViewModel?.FindCardCommand); } }
    private void OnEntryKey(object? sender, KeyEventArgs e)
    { if (e.Key == Key.Enter) { e.Handled=true; Execute(ViewModel?.RegisterEntryCommand); } }
    private void OnAccountContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not Control { DataContext: CassaConto line } row
            || ViewModel is not { } vm || !vm.Conto.Contains(line)) return;
        row.ContextMenu?.Close();
        var item=new MenuItem {
            Header="Storna voce", IsEnabled=line.CanStornare && !vm.IsPaymentBusy,
            Command=vm.StornaContoCommand, CommandParameter=line
        };
        var menu=new ContextMenu { ItemsSource=new[] { item } };
        menu.Closed+=(_,_)=> { if (ReferenceEquals(row.ContextMenu,menu)) row.ContextMenu=null; };
        row.ContextMenu=menu;
        menu.Open(row);
        e.Handled=true;
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape) { e.Handled=true; Execute(ViewModel?.EscPressedCommand); }
    }
    private static void Execute(ICommand? command)
    { if (command?.CanExecute(null) == true) command.Execute(null); }
    private sealed class FocusHandlers(params IDisposable[] handlers) : IDisposable
    {
        public void Dispose() { foreach (var handler in handlers) handler.Dispose(); }
    }
}
