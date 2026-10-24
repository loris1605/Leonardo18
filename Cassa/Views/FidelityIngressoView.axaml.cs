using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cassa.ViewModels;
namespace Cassa.Views;
public partial class FidelityIngressoView:UserControl
{
    private ObservableCollection<TimbroIngressoFidelityViewModel>? _timbri;
    private bool _queued;
    public FidelityIngressoView()
    {
        InitializeComponent();
        DataContextChanged+=(_,_)=>BindTimbri();
        SizeChanged+=(_,_)=>ShowCurrentStamp();
        AttachedToVisualTree+=(_,_)=>BindTimbri();
        DetachedFromVisualTree+=(_,_)=>UnbindTimbri();
    }
    private void BindTimbri()
    {
        var source=(DataContext as CassaViewModel)?.TimbriTesseraFidelity;
        if(ReferenceEquals(source,_timbri))return;
        UnbindTimbri();
        if(TopLevel.GetTopLevel(this) is null)return;
        _timbri=source;
        if(_timbri is not null)_timbri.CollectionChanged+=OnTimbriChanged;
        ShowCurrentStamp();
    }
    private void UnbindTimbri()
    {
        if(_timbri is not null)_timbri.CollectionChanged-=OnTimbriChanged;
        _timbri=null;
    }
    private void OnTimbriChanged(object? sender,NotifyCollectionChangedEventArgs e)=>ShowCurrentStamp();
    private void ShowCurrentStamp()
    {
        if(_queued)return;
        _queued=true;
        Dispatcher.UIThread.Post(()=>
        {
            _queued=false;
            if(!IsEffectivelyVisible || TopLevel.GetTopLevel(this) is null)return;
            var stamp=this.GetVisualDescendants().OfType<Button>()
                .FirstOrDefault(x=>x.DataContext is TimbroIngressoFidelityViewModel{Corrente:true});
            ((stamp?.Parent as Control)??stamp)?.BringIntoView();
        },DispatcherPriority.Loaded);
    }
}
