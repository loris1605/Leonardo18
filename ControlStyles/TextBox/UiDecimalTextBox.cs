using Avalonia.Input;
using System.Globalization;

namespace ControlStyles
{
    public class UiDecimalTextBox : UiTextBox
    {
        protected override void OnTextInput(TextInputEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Text))
            {
                var decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

                // Accetta sia punto sia virgola e li converte nel separatore della cultura corrente.
                var normalizedInput = e.Text
                    .Replace(".", decimalSeparator, StringComparison.Ordinal)
                    .Replace(",", decimalSeparator, StringComparison.Ordinal);

                e.Text = normalizedInput;

                var currentText = Text ?? string.Empty;
                var selectionStart = Math.Min(SelectionStart, SelectionEnd);
                var selectionLength = Math.Abs(SelectionEnd - SelectionStart);
                var proposedText = currentText
                    .Remove(selectionStart, selectionLength)
                    .Insert(selectionStart, normalizedInput);

                if (!IsValidDecimalInput(proposedText, decimalSeparator,
                                         CultureInfo.CurrentCulture.NumberFormat.NegativeSign))
                {
                    e.Handled = true;
                    return;
                }
            }

            base.OnTextInput(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            // Evita spazi anche quando non viene generato un evento TextInput.
            if (e.Key == Key.Space)
            {
                e.Handled = true;
                return;
            }

            // Mantiene la gestione di Invio definita in UiTextBox.
            base.OnKeyDown(e);
        }

        private static bool IsValidDecimalInput(string text, string decimalSeparator, string negativeSign)
        {
            if (text.Length == 0)
                return true;

            if (negativeSign.Length > 0 && text.StartsWith(negativeSign, StringComparison.Ordinal))
                text = text[negativeSign.Length..];

            var separatorIndex = text.IndexOf(decimalSeparator, StringComparison.Ordinal);

            if (separatorIndex >= 0 &&
                text.IndexOf(decimalSeparator, separatorIndex + decimalSeparator.Length, StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            var integerPart = separatorIndex >= 0 ? text[..separatorIndex] : text;
            var fractionalPart = separatorIndex >= 0
                ? text[(separatorIndex + decimalSeparator.Length)..]
                : string.Empty;

            return ContainsOnlyAsciiDigits(integerPart) && ContainsOnlyAsciiDigits(fractionalPart);
        }

        private static bool ContainsOnlyAsciiDigits(string text)
        {
            foreach (var character in text)
            {
                if (character is < '0' or > '9')
                    return false;
            }

            return true;
        }
    }
}
