using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SpeakerAnimator.View;

public abstract class MovablePointVisual
{
    public Ellipse PointGraphicElement { get; }
    protected int ZIndex = 15;
    
    
    private readonly Canvas _parentCanvas;
    
    private bool _isDragged;
    private Point _dragPointOffset;
    private double _radius;
    
    public event Action<Point>? PositionChanged;

    public MovablePointVisual(Canvas parentCanvas, Point initialPosition, Brush fillColor, Brush outlineColor,
        double radius = 10)
    {
        _parentCanvas = parentCanvas;
        _radius = radius;
        PointGraphicElement = new Ellipse
        {
            Width = radius*2, Height = radius*2, Fill = fillColor,
            Stroke = outlineColor, StrokeThickness = radius/4, Cursor = Cursors.Hand
        };
        //zeby podana pozycja to byl srodek kolka a nie lewy gorny rog
        Canvas.SetLeft(PointGraphicElement, initialPosition.X-radius);
        Canvas.SetTop(PointGraphicElement, initialPosition.Y-radius);
        Panel.SetZIndex(PointGraphicElement, ZIndex);

        PointGraphicElement.MouseLeftButtonDown += OnPointDragStart;
        PointGraphicElement.MouseMove += OnPointDragged;
        PointGraphicElement.MouseLeftButtonUp += OnPointDragReleased;

        parentCanvas.Children.Add(PointGraphicElement);
    }


    private void OnPointDragStart(object sender, MouseButtonEventArgs e)
    {
        if (PointGraphicElement.CaptureMouse())
        {
            _isDragged = true;
            //zeby moc odpowiednio offsetowac pozycje wzgledem miejsca na kolku gdzie sie go kliknelo
            Point mouseCLickPoint = e.GetPosition(_parentCanvas);
            _dragPointOffset = new(mouseCLickPoint.X - Canvas.GetLeft(PointGraphicElement),
                mouseCLickPoint.Y - Canvas.GetTop(PointGraphicElement));
            //zeby nie spradzalo wyzej drzewa elementow dla tego zdarzenia
            e.Handled = true;
        }
    }

    private void OnPointDragged(object sender, MouseEventArgs e)
    {
        if (_isDragged)
        {
            Point currentMousePosition = e.GetPosition(_parentCanvas);
            double left = currentMousePosition.X - _dragPointOffset.X;
            double top = currentMousePosition.Y - _dragPointOffset.Y;

            Canvas.SetLeft(PointGraphicElement, left);
            Canvas.SetTop(PointGraphicElement, top);
            Point logicalCenter = new Point(left + _radius, top + _radius);
            PositionChanged?.Invoke(logicalCenter);
        }
    }

    private void OnPointDragReleased(object sender, MouseButtonEventArgs e)
    {
        if (_isDragged)
        {
            _isDragged = false;
            PointGraphicElement.ReleaseMouseCapture();
            e.Handled = true;
        }
    }
}