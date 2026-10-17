using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using FluentIcons.Avalonia;
using FluentIcons.Common;

namespace ControlStyles
{
    public class UiIconButton : UiButton
    {
#if DEBUG
    ~UiIconButton() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        protected override Type StyleKeyOverride => typeof(UiButton);

        private readonly SymbolIcon _iconControl;
        private readonly TextBlock _labelControl;

        #region TypeButton 

        public static readonly StyledProperty<ButtonType> TypeButtonProperty =
        AvaloniaProperty.Register<UiIconButton, ButtonType>(
            nameof(TypeButton),
            ButtonType.Normal);

        public ButtonType TypeButton
        {
            get => GetValue(TypeButtonProperty);
            set => SetValue(TypeButtonProperty, value);
        }

        #endregion

        #region XLabel

        public static readonly StyledProperty<string> XLabelProperty =
        AvaloniaProperty.Register<UiIconButton, string>(
            nameof(XLabel),
            string.Empty);

        public string XLabel
        {
            get => GetValue(XLabelProperty);
            set => SetValue(XLabelProperty, value);
        }


        #endregion

        // Assicurati che IconSymbol sia di questo tipo esatto
        public static readonly StyledProperty<Symbol?> IconSymbolProperty =
        AvaloniaProperty.Register<UiIconButton, Symbol?>(
            nameof(IconSymbol));

        public Symbol? IconSymbol
        {
            get => GetValue(IconSymbolProperty);
            set => SetValue(IconSymbolProperty, value);
        }

        public static readonly StyledProperty<IconVariant> IconVariantProperty =
            AvaloniaProperty.Register<UiIconButton, IconVariant>(nameof(IconVariant), FluentIcons.Common.IconVariant.Regular);

        public IconVariant IconVariant
        {
            get => GetValue(IconVariantProperty);
            set => SetValue(IconVariantProperty, value);
        }

        public UiIconButton()
        {
            _iconControl = new SymbolIcon
            {
                FontSize = 20,
                Width = 20,
                Height = 20,
                IsVisible = false,
                VerticalAlignment = VerticalAlignment.Center
            };

            _labelControl = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center
            };

            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            content.Children.Add(_iconControl);
            content.Children.Add(_labelControl);
            Content = content;

            UpdateAppearance(TypeButton);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TypeButtonProperty)
            {
                UpdateAppearance(change.GetNewValue<ButtonType>());
            }
            else if (change.Property == IconSymbolProperty)
            {
                UpdateIcon(change.GetNewValue<Symbol?>());
            }
            else if (change.Property == IconVariantProperty)
            {
                if (_iconControl is not null)
                    _iconControl.IconVariant = change.GetNewValue<IconVariant>();
            }
            else if (change.Property == XLabelProperty)
            {
                UpdateLabel(change.GetNewValue<string>());
            }
        }

        private void UpdateAppearance(ButtonType type)
        {
            var (symbol, label) = type switch
            {
                ButtonType.Adding => (Symbol.Add, "AGGIUNGI "),
                ButtonType.Delete or ButtonType.Cestino => (Symbol.Delete, "RIMUOVI "),
                ButtonType.Save => (Symbol.Save, "REGISTRA "),
                ButtonType.Search => (Symbol.Search, string.Empty),
                ButtonType.Setting => (Symbol.Settings, string.Empty),
                ButtonType.Print => (Symbol.Print, string.Empty),
                ButtonType.Exit => (Symbol.ArrowExit, "USCITA "),
                ButtonType.Close => (Symbol.Dismiss, string.Empty),
                ButtonType.Database => (Symbol.Database, string.Empty),
                _ => ((Symbol?)null, string.Empty)
            };

            IconSymbol = symbol;
            XLabel = label;
       
        }

        private void UpdateIcon(Symbol? symbol)
        {
            if (_iconControl is null)
                return;

            _iconControl.IsVisible = symbol.HasValue;
            if (symbol.HasValue)
                _iconControl.Symbol = symbol.Value;
        }

        private void UpdateLabel(string? label)
        {
            if (_labelControl is null)
                return;

            _labelControl.Text = label ?? string.Empty;
            _labelControl.IsVisible = !string.IsNullOrWhiteSpace(label);
        }

    }
}
