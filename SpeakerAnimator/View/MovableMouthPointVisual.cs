using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SpeakerAnimator.View;

public class MovableMouthPointVisual
{
    public Ellipse MouthPointGraphicElement { get; }

    private readonly Canvas _parentCanvas;
    
    private bool _isDragged;
    private Point _dragPointOffset;

    public MovableMouthPointVisual(Canvas parentCanvas, Point initialPosition, Brush fillColor, Brush outlineColor,
        double radius = 10)
    {
        _parentCanvas = parentCanvas;
        MouthPointGraphicElement = new Ellipse
        {
            Width = radius*2, Height = radius*2, Fill = fillColor,
            Stroke = outlineColor, StrokeThickness = radius/4, Cursor = Cursors.Hand
        };
        //zeby podana pozycja to byl srodek kolka a nie lewy gorny rog
        Canvas.SetLeft(MouthPointGraphicElement, initialPosition.X-radius);
        Canvas.SetTop(MouthPointGraphicElement, initialPosition.Y-radius);

        MouthPointGraphicElement.MouseLeftButtonDown += OnMouthPointDragStart;
        MouthPointGraphicElement.MouseMove += OnMouthPointDragged;
        MouthPointGraphicElement.MouseLeftButtonUp += OnMouthPointDragReleased;

        parentCanvas.Children.Add(MouthPointGraphicElement);
    }


    private void OnMouthPointDragStart(object sender, MouseButtonEventArgs e)
    {
        if (MouthPointGraphicElement.CaptureMouse())
        {
            _isDragged = true;
            //zeby moc odpowiednio offsetowac pozycje wzgledem miejsca na kolku gdzie sie go kliknelo
            Point mouseCLickPoint = e.GetPosition(_parentCanvas);
            _dragPointOffset = new(mouseCLickPoint.X - Canvas.GetLeft(MouthPointGraphicElement),
                mouseCLickPoint.Y - Canvas.GetTop(MouthPointGraphicElement));
            //zeby nie spradzalo wyzej drzewa elementow dla tego zdarzenia
            e.Handled = true;
        }
    }

    private void OnMouthPointDragged(object sender, MouseEventArgs e)
    {
        if (_isDragged)
        {
            Point currentMousePosition = e.GetPosition(_parentCanvas);
            Canvas.SetLeft(MouthPointGraphicElement, 
                currentMousePosition.X-_dragPointOffset.X);
            Canvas.SetTop(MouthPointGraphicElement, 
                currentMousePosition.Y-_dragPointOffset.Y);
        }
    }

    private void OnMouthPointDragReleased(object sender, MouseButtonEventArgs e)
    {
        if (_isDragged)
        {
            _isDragged = false;
            MouthPointGraphicElement.ReleaseMouseCapture();
            e.Handled = true;
        }
    }
    
}