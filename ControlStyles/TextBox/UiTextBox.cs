using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using System.Globalization;

namespace ControlStyles
{
    public class UiTextBox : TextBox
    {
#if DEBUG
    ~UiTextBox() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        protected override Type StyleKeyOverride => typeof(TextBox);

        public UiTextBox()
        {
            // Contrassegna il controllo base e tutte le sue classi derivate per gli stili condivisi.
            Classes.Add("uiTextBox");
        }

        public static readonly StyledProperty<bool> MoveToNextOnEnterProperty =
            AvaloniaProperty.Register<UiTextBox, bool>(
                nameof(MoveToNextOnEnter),
                defaultValue: true);

        public bool MoveToNextOnEnter
        {
            get => GetValue(MoveToNextOnEnterProperty);
            set => SetValue(MoveToNextOnEnterProperty, value);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;

                if (MoveToNextOnEnter)
                    MoveFocusToNext();

                return;
            }

            base.OnKeyDown(e);
        }

        private void MoveFocusToNext()
        {
            var root = TopLevel.GetTopLevel(this);
            if (root is null)
                return;

            // Mantiene l'ordine visuale fra controlli con lo stesso TabIndex.
            var textBoxes = root.GetVisualDescendants()
                .OfType<UiTextBox>()
                .Where(textBox => textBox.IsVisible && textBox.IsEnabled && textBox.Focusable)
                .OrderBy(textBox => textBox.TabIndex)
                .ToList();

            var currentIndex = textBoxes.IndexOf(this);
            if (currentIndex < 0 || textBoxes.Count < 2)
                return;

            var nextIndex = (currentIndex + 1) % textBoxes.Count;
            var nextTarget = textBoxes[nextIndex];

            if (nextTarget.Focus())
                nextTarget.SelectAll();
        }
    }

    public static class TextBoxExtensions
    {
        public static readonly AttachedProperty<int> MinCharWidthProperty =
            AvaloniaProperty.RegisterAttached<UiTextBox, int>(
                "MinCharWidth",
                typeof(TextBoxExtensions),
                defaultValue: 0);

        public static int GetMinCharWidth(UiTextBox element) =>
            element.GetValue(MinCharWidthProperty);

        public static void SetMinCharWidth(UiTextBox element, int value) =>
            element.SetValue(MinCharWidthProperty, value);

        static TextBoxExtensions()
        {
            MinCharWidthProperty.Changed.AddClassHandler<UiTextBox>((textBox, change) =>
            {
                if (change.NewValue is int charCount && charCount > 0)
                    UpdateWidth(textBox, charCount);
            });
        }

        private static void UpdateWidth(TextBox textBox, int charCount)
        {
            var sampleText = new string('M', charCount);
            var typeface = new Typeface(
                textBox.FontFamily,
                textBox.FontStyle,
                textBox.FontWeight);

            var formattedText = new FormattedText(
                sampleText,
                CultureInfo.CurrentCulture,
                textBox.FlowDirection,
                typeface,
                textBox.FontSize,
                Brushes.Black);

            textBox.MinWidth = formattedText.Width
                + textBox.Padding.Left
                + textBox.Padding.Right;
        }
    }
}


