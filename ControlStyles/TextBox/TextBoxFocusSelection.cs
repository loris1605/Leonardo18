using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
namespace ControlStyles;

/// <summary>Selects a field's contents once when it receives focus, including pointer focus.</summary>
public static class TextBoxFocusSelection
{
    private static int _enabled;
    public static void Enable()
    {
        if(Interlocked.Exchange(ref _enabled,1)!=0)return;
        InputElement.GotFocusEvent.AddClassHandler<TextBox>((box,_)=>
        {
            if(!box.IsFocused)return;
            // Pointer handling places the caret after GotFocus; select after that event completes.
            Dispatcher.UIThread.Post(()=>
            {
                if(box.IsFocused)box.SelectAll();
            },DispatcherPriority.Input);
        },handledEventsToo:true);
    }
}
