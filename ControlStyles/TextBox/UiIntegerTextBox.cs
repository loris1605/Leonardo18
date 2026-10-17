using Avalonia.Input;

namespace ControlStyles
{
    public class UiIntegerTextBox : UiTextBox
    {
        protected override void OnTextInput(TextInputEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Text) &&
                e.Text.Any(static character => character is < '0' or > '9'))
            {
                e.Handled = true;
                return;
            }

            base.OnTextInput(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            // Blocca lo spazio anche quando non genera un evento TextInput.
            if (e.Key == Key.Space)
            {
                e.Handled = true;
                return;
            }

            // Mantiene la gestione di Invio definita in UiTextBox.
            base.OnKeyDown(e);
        }
    }
}
