using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace ControlStyles
{
    public class UiEuroTextBox : UiDecimalTextBox
    {
        public UiEuroTextBox()
        {
            InnerRightContent = new TextBlock
            {
                Text = "€",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                Focusable = false,
                IsHitTestVisible = false
            };
        }
    }
}
