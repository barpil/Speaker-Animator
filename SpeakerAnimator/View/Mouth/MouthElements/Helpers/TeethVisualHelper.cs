using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SpeakerAnimator.View.MouthElementHelpers;

public static class TeethVisualHelper
{
    public static MovableTeethPointVisual CreateToothPointVisual(Canvas parentCanvas, Point point, double radius)
    {
        Brush fillColor = Brushes.WhiteSmoke;
        Brush outlineColor = Brushes.Black;
        var toothPointVisual = new MovableTeethPointVisual(parentCanvas, point, fillColor, outlineColor, radius: radius);

        return toothPointVisual;
    }   
    
    
    public static void CreateTeethStripVisual(Canvas parentCanvas, Point a, Point b, double toothHeight, double toothWidth, double gapSizePrc)
    {
        Brush fillColor = Brushes.WhiteSmoke;
        Brush outlineColor = Brushes.Black;

        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);
    
        if (toothWidth <= 0 || toothHeight <= 0 || gapSizePrc < 0 || length < 0.1) return;

        double gapSize = toothWidth * gapSizePrc;
        double toothPlusGapWidth = toothWidth + gapSize;
    
        int teethCount = (int)Math.Floor((length + gapSize) / toothPlusGapWidth);
        if (teethCount < 1) return;

        double totalTeethWidth = (teethCount * toothWidth) + ((teethCount - 1) * gapSize);
        double startOffset = (length - totalTeethWidth) / 2;

        //wyznaczenie obrotu paska kwadratow
        double angle = Math.Atan2(dy, dx);
        double degrees = angle * 180 / Math.PI;

        for (int i = 0; i < teethCount; i++)
        {
            double dist = startOffset + (i * toothPlusGapWidth) + (toothWidth / 2.0);
            double t = dist / length;
        
            double x = a.X + t * dx;
            double y = a.Y + t * dy;
        
            var square = new Rectangle
            {
                Width = toothWidth,
                Height = toothHeight,
                Fill = fillColor,
                Stroke = outlineColor,
                StrokeThickness = 1,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(degrees)
            };
            
            Canvas.SetLeft(square, x - toothWidth / 2);
            Canvas.SetTop(square, y - toothHeight / 2);
        
            parentCanvas.Children.Add(square);
        }
    }
}