using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI.Primitives;
using Servizi.ViewModels;
namespace Servizi.Views;
public sealed partial class ServiziView : global::Views.BaseUserControl<ServiziViewModel>
{
    public ServiziView()
    {
        AvaloniaXamlLoader.Load(this);
        SizeChanged+=(_,_)=>Layout();
        this.FindControl<ListBox>("AbbonamentiList")!.DoubleTapped+=(_,_)=>
        {if(ViewModel is {GroupEnabled:true} vm) vm.UpdCommand.Execute().SubscribePrimitives();};
    }
    protected override IDisposable? OnViewActivated(ServiziViewModel vm)
    {
        void Changed(object? sender,System.ComponentModel.PropertyChangedEventArgs e)
        {
            if(e.PropertyName==nameof(vm.HasEditor)) {Layout();if(vm.HasEditor)Avalonia.Threading.Dispatcher.UIThread.Post(()=>{this.FindControl<TextBox>("NomeInput")!.Focus();});}
        }
        vm.PropertyChanged+=Changed;Layout();return new Cleanup(()=>vm.PropertyChanged-=Changed);
    }
    private void Layout()
    {
        var compact=Bounds.Width<1100;
        this.FindControl<Grid>("MainColumns")!.ColumnDefinitions=new ColumnDefinitions(compact?"*":"3*,2*");
        Grid.SetColumn(this.FindControl<Border>("EditorPanel")!,compact?0:1);
        this.FindControl<Grid>("ListPanel")!.IsVisible=!(compact&&ViewModel?.HasEditor==true);
    }
    private sealed class Cleanup(Action action):IDisposable {public void Dispose()=>action();}
}