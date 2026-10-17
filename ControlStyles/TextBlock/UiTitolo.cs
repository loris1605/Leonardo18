using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace ControlStyles
{
    public class UiTitolo : TemplatedControl
    {
#if DEBUG
    ~UiTitolo() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<UiTitolo, string>(
                nameof(Text),
                defaultValue: string.Empty);

        public string Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly StyledProperty<bool> SubTitoloProperty =
            AvaloniaProperty.Register<UiTitolo, bool>(
                nameof(SubTitolo),
                defaultValue: false);

        public bool SubTitolo
        {
            get => GetValue(SubTitoloProperty);
            set => SetValue(SubTitoloProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SubTitoloProperty)
                PseudoClasses.Set(":subtitle", change.GetNewValue<bool>());
        }
    }
}
