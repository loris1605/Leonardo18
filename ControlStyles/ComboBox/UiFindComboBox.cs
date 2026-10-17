using Avalonia;

namespace ControlStyles;

public class UiFindComboBox : UiComboBox
{
    // Usa lo stesso stile visuale di UiComboBox.
    protected override Type StyleKeyOverride => typeof(UiComboBox);

    public static readonly StyledProperty<EnumComboType> ComboTypeProperty =
        AvaloniaProperty.Register<UiFindComboBox, EnumComboType>(
            nameof(ComboType),
            EnumComboType.Null);

    private static readonly string[] StringOptions =
    [
        "No Ricerca",
        "Uguale a",
        "Inizia Con",
        "Che Contiene"
    ];

    private static readonly string[] NumericOptions =
    [
        "No Ricerca",
        " = Uguale a",
        " <> Diverso da",
        " > Maggiore di",
        " < Minore di"
    ];

    private static readonly string[] DateOptions =
    [
        "No Ricerca",
        "Uguale a",
        "Prima del",
        "Dopo il"
    ];

    public EnumComboType ComboType
    {
        get => GetValue(ComboTypeProperty);
        set => SetValue(ComboTypeProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ComboTypeProperty)
            UpdateItems(change.GetNewValue<EnumComboType>());
    }

    private void UpdateItems(EnumComboType type)
    {
        var options = type switch
        {
            EnumComboType.String => StringOptions,
            EnumComboType.Numeric => NumericOptions,
            EnumComboType.Date => DateOptions,
            _ => Array.Empty<string>()
        };

        ItemsSource = options;
        SelectedIndex = options.Length > 0 ? 0 : -1;
    }
}

public enum EnumComboType
{
    Null = -1,
    String = 0,
    Date = 1,
    Numeric = 2
}
