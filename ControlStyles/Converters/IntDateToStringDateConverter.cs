using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace ControlStyles;

public sealed class IntDateToStringDateConverter : IValueConverter
{
#if DEBUG
    ~IntDateToStringDateConverter() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int dateValue || dateValue <= 0)
            return string.Empty;

        var dateText = dateValue.ToString("D8", CultureInfo.InvariantCulture);

        return DateOnly.TryParseExact(
            dateText,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? date.ToString("dd/MM/yyyy", culture)
            : string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => AvaloniaProperty.UnsetValue;
}
