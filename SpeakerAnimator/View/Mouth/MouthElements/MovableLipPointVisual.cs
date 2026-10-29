using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SpeakerAnimator.View;

public class MovableLipPointVisual: MovablePointVisual
{
    public MovableLipPointVisual(Canvas parentCanvas, Point initialPosition, Brush fillColor, Brush outlineColor, double radius = 10) : base(parentCanvas, initialPosition, fillColor, outlineColor, radius)
    {
        ZIndex = 15;
    }
}