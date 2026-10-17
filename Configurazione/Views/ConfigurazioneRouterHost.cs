using Avalonia;
using Avalonia.Controls;
using Configurazione.ViewModels;
using ReactiveUI;
using System.Collections.Specialized;
namespace Configurazione.Views;

public sealed class ConfigurazioneRouterHost : ContentControl
{
#if DEBUG
    ~ConfigurazioneRouterHost() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif
    protected override Type StyleKeyOverride=>typeof(ContentControl);
    public static readonly StyledProperty<RoutingState?> RouterProperty=
        AvaloniaProperty.Register<ConfigurazioneRouterHost,RoutingState?>(nameof(Router));
    public RoutingState? Router {get=>GetValue(RouterProperty);set=>SetValue(RouterProperty,value);}
    private RoutingState? _router;
    private IRoutableViewModel? _displayed;
    private bool _attached;
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if(change.Property==RouterProperty&&_attached)Subscribe();
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e){base.OnAttachedToVisualTree(e);_attached=true;Subscribe();}
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e){_attached=false;Unsubscribe();base.OnDetachedFromVisualTree(e);}
    private void Subscribe()
    {
        Unsubscribe();_router=Router;
        if(_router is null)return;
        _router.NavigationStack.CollectionChanged+=Changed;Display();
    }
    private void Unsubscribe()
    {
        if(_router is not null)_router.NavigationStack.CollectionChanged-=Changed;
        _router=null;_displayed=null;Content=null;
    }
    private void Changed(object? sender,NotifyCollectionChangedEventArgs args)=>Display();
    private void Display()
    {
        var vm=_router?.GetCurrentViewModel();
        if(ReferenceEquals(vm,_displayed))return;
        _displayed=null;Content=null;
        if(vm is null)return;
        Content=vm switch {
            ConfigurazioneGroupViewModel group=>new ConfigurazioneGroupView{ViewModel=group},
            ConfigurazioneInputViewModel input=>new ConfigurazioneInputView{ViewModel=input},
            ConfigurazioneLinksViewModel links=>new ConfigurazioneLinksView{ViewModel=links},
            _=>throw new InvalidOperationException("Vista configurazione non registrata.")
        };
        _displayed=vm;
    }
}
