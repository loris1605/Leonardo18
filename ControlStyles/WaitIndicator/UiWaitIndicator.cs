using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace ControlStyles;

/// <summary>
/// Indicatore circolare animato riutilizzabile nelle viste Avalonia.
/// </summary>
public sealed class UiWaitIndicator : Control
{
#if DEBUG
    ~UiWaitIndicator() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<UiWaitIndicator, bool>(nameof(IsActive));

    public static readonly StyledProperty<double> DiameterProperty =
        AvaloniaProperty.Register<UiWaitIndicator, double>(nameof(Diameter), 48);

    public static readonly StyledProperty<double> DotSizeProperty =
        AvaloniaProperty.Register<UiWaitIndicator, double>(nameof(DotSize), 4.5);

    public static readonly StyledProperty<int> DotCountProperty =
        AvaloniaProperty.Register<UiWaitIndicator, int>(nameof(DotCount), 12);

    public static readonly StyledProperty<double> RotationSpeedProperty =
        AvaloniaProperty.Register<UiWaitIndicator, double>(nameof(RotationSpeed), 1.2);

    public static readonly StyledProperty<Color> FirstColorProperty =
        AvaloniaProperty.Register<UiWaitIndicator, Color>(nameof(FirstColor), Color.FromRgb(0, 174, 239));

    public static readonly StyledProperty<Color> SecondColorProperty =
        AvaloniaProperty.Register<UiWaitIndicator, Color>(nameof(SecondColor), Color.FromRgb(139, 92, 246));

    public static readonly StyledProperty<Color> ThirdColorProperty =
        AvaloniaProperty.Register<UiWaitIndicator, Color>(nameof(ThirdColor), Color.FromRgb(244, 63, 94));

    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(16);
    private readonly DispatcherTimer _timer;
    private bool _isAttached;
    private double _rotationAngle;
    private double _colorOffset;

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>Diametro desiderato in pixel.</summary>
    public double Diameter
    {
        get => GetValue(DiameterProperty);
        set => SetValue(DiameterProperty, value);
    }

    /// <summary>Raggio dei punti che formano l'anello.</summary>
    public double DotSize
    {
        get => GetValue(DotSizeProperty);
        set => SetValue(DotSizeProperty, value);
    }

    public int DotCount
    {
        get => GetValue(DotCountProperty);
        set => SetValue(DotCountProperty, value);
    }

    /// <summary>Giri completi al secondo.</summary>
    public double RotationSpeed
    {
        get => GetValue(RotationSpeedProperty);
        set => SetValue(RotationSpeedProperty, value);
    }

    public Color FirstColor
    {
        get => GetValue(FirstColorProperty);
        set => SetValue(FirstColorProperty, value);
    }

    public Color SecondColor
    {
        get => GetValue(SecondColorProperty);
        set => SetValue(SecondColorProperty, value);
    }

    public Color ThirdColor
    {
        get => GetValue(ThirdColorProperty);
        set => SetValue(ThirdColorProperty, value);
    }

    public UiWaitIndicator()
    {
        IsVisible = IsActive;
        _timer = new DispatcherTimer { Interval = FrameInterval };
        _timer.Tick += OnAnimationTick;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var diameter = Math.Max(0, Diameter);
        var width = double.IsInfinity(availableSize.Width)
            ? diameter
            : Math.Min(diameter, availableSize.Width);
        var height = double.IsInfinity(availableSize.Height)
            ? diameter
            : Math.Min(diameter, availableSize.Height);

        return new Size(width, height);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (!IsActive || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = Math.Max(0, Math.Min(Bounds.Width, Bounds.Height) / 2 - DotSize * 1.5);
        var dotRadius = Math.Max(0.5, DotSize);
        var count = Math.Max(3, DotCount);

        for (var index = 0; index < count; index++)
        {
            var angle = (_rotationAngle + index * 360d / count) * Math.PI / 180d;
            var point = new Point(
                center.X + Math.Cos(angle) * radius,
                center.Y + Math.Sin(angle) * radius);
            var colorProgress = (index / (double)count + _colorOffset) % 1d;

            var brush = new SolidColorBrush(GetAnimatedColor(colorProgress));
            context.DrawEllipse(brush, null, point, dotRadius, dotRadius);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        UpdateAnimationState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        UpdateAnimationState();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsActiveProperty)
        {
            IsVisible = IsActive;
            UpdateAnimationState();
        }
        else if (change.Property == DiameterProperty ||
                 change.Property == DotSizeProperty ||
                 change.Property == DotCountProperty ||
                 change.Property == FirstColorProperty ||
                 change.Property == SecondColorProperty ||
                 change.Property == ThirdColorProperty)
        {
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    private void UpdateAnimationState()
    {
        if (_isAttached && IsActive)
            _timer.Start();
        else
            _timer.Stop();

        InvalidateVisual();
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        var seconds = FrameInterval.TotalSeconds;
        _rotationAngle = (_rotationAngle + 360 * Math.Max(0, RotationSpeed) * seconds) % 360;
        _colorOffset = (_colorOffset + 0.22 * seconds) % 1;
        InvalidateVisual();
    }

    private Color GetAnimatedColor(double progress)
    {
        if (progress < 1d / 3d)
            return Blend(FirstColor, SecondColor, progress * 3d);

        if (progress < 2d / 3d)
            return Blend(SecondColor, ThirdColor, (progress - 1d / 3d) * 3d);

        return Blend(ThirdColor, FirstColor, (progress - 2d / 3d) * 3d);
    }

    private static Color Blend(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);

        return Color.FromArgb(
            (byte)(from.A + (to.A - from.A) * amount),
            (byte)(from.R + (to.R - from.R) * amount),
            (byte)(from.G + (to.G - from.G) * amount),
            (byte)(from.B + (to.B - from.B) * amount));
    }
}
