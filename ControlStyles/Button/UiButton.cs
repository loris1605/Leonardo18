using Avalonia;
using Avalonia.Controls;

namespace ControlStyles
{
    public class UiButton : Button
    {
#if DEBUG
    ~UiButton() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        //protected override Type StyleKeyOverride => typeof(Button);

        
        public UiButton()
        {
            this.Classes.Add("UiButtonStyle");
        }

        #region ToolTipText Property

        public static readonly StyledProperty<string> ToolTipTextProperty =
        AvaloniaProperty.Register<UiButton, string>(nameof(ToolTipText), defaultValue: string.Empty);

        public string ToolTipText
        {
            get => GetValue(ToolTipTextProperty);
            set => SetValue(ToolTipTextProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ToolTipTextProperty)
            {
                var text = change.GetNewValue<string?>();

                ToolTip.SetTip(
                    this,
                    string.IsNullOrWhiteSpace(text) ? null : text);
            }
        }

        #endregion
    }
}
