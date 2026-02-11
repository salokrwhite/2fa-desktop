using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TwoFactorAuth.Controls;

public class CountdownPieControl : Control
{
    public static readonly StyledProperty<double> AngleProperty =
        AvaloniaProperty.Register<CountdownPieControl, double>(nameof(Angle), 360.0);

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<CountdownPieControl, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> TrackFillProperty =
        AvaloniaProperty.Register<CountdownPieControl, IBrush?>(nameof(TrackFill));

    public double Angle
    {
        get => GetValue(AngleProperty);
        set => SetValue(AngleProperty, value);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public IBrush? TrackFill
    {
        get => GetValue(TrackFillProperty);
        set => SetValue(TrackFillProperty, value);
    }

    static CountdownPieControl()
    {
        AffectsRender<CountdownPieControl>(AngleProperty, FillProperty, TrackFillProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0) return;

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = size / 2;

        if (TrackFill != null)
        {
            context.DrawEllipse(TrackFill, null, center, radius, radius);
        }

        var angle = Math.Clamp(Angle, 0, 360);
        if (angle <= 0 || Fill == null) return;

        if (angle >= 360)
        {
            context.DrawEllipse(Fill, null, center, radius, radius);
            return;
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var startAngle = -90.0; 
            var endAngle = startAngle + angle;

            var startRad = startAngle * Math.PI / 180.0;
            var endRad = endAngle * Math.PI / 180.0;

            var startPoint = new Point(
                center.X + radius * Math.Cos(startRad),
                center.Y + radius * Math.Sin(startRad));

            var endPoint = new Point(
                center.X + radius * Math.Cos(endRad),
                center.Y + radius * Math.Sin(endRad));

            ctx.BeginFigure(center, true);
            ctx.LineTo(startPoint);
            ctx.ArcTo(endPoint, new Size(radius, radius), 0, angle > 180, SweepDirection.Clockwise);
            ctx.LineTo(center);
            ctx.EndFigure(true);
        }

        context.DrawGeometry(Fill, null, geometry);
    }
}
