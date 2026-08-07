using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Foundation;

namespace Poker_With_Your_Friends.View.Controls;

public sealed class SemicirclePlayerPanel : Panel
{
    private static readonly Point[] SeatPositions =
    [
        new(0.50, 1.00),
        new(0.78, 0.82),
        new(0.22, 0.82),
        new(0.95, 0.43),
        new(0.05, 0.43),
        new(0.50, 0.00),
    ];

    protected override Size MeasureOverride(Size availableSize)
    {
        double widestChild = 0;
        double tallestChild = 0;
        var childConstraint = new Size(double.PositiveInfinity, double.PositiveInfinity);

        foreach (UIElement child in Children)
        {
            child.Measure(childConstraint);
            widestChild = Math.Max(widestChild, child.DesiredSize.Width);
            tallestChild = Math.Max(tallestChild, child.DesiredSize.Height);
        }

        double desiredWidth = double.IsFinite(availableSize.Width)
            ? availableSize.Width
            : widestChild * 4;
        double desiredHeight = double.IsFinite(availableSize.Height)
            ? availableSize.Height
            : tallestChild * 2.5;

        return new Size(desiredWidth, desiredHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        for (int index = 0; index < Children.Count; index++)
        {
            UIElement child = Children[index];
            Point position = SeatPositions[Math.Min(index, SeatPositions.Length - 1)];
            Size childSize = child.DesiredSize;
            Canvas.SetZIndex(child, Children.Count - index);

            double left = Clamp(
                position.X * finalSize.Width - childSize.Width / 2,
                0,
                Math.Max(0, finalSize.Width - childSize.Width));
            double top = Clamp(
                position.Y * finalSize.Height - childSize.Height / 2,
                0,
                Math.Max(0, finalSize.Height - childSize.Height));

            child.Arrange(new Rect(left, top, childSize.Width, childSize.Height));
        }

        return finalSize;
    }

    private static double Clamp(double value, double minimum, double maximum)
    {
        return Math.Min(Math.Max(value, minimum), maximum);
    }
}
