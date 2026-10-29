using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SpeakerAnimator.View;

public class MovableTeethPointVisual: MovablePointVisual
{
    public MovableTeethPointVisual(Canvas parentCanvas, Point initialPosition, Brush fillColor, Brush outlineColor, double radius = 10) : base(parentCanvas, initialPosition, fillColor, outlineColor, radius)
    {
        ZIndex = 10;
    }
}