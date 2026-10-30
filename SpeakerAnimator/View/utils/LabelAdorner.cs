using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace SpeakerAnimator.View.utils;

public class LabelAdorner : Adorner
{
    private readonly Label _label;
    private readonly Point _centerOffset;
    
    private LabelAdorner(UIElement adornedElement, string content, Point labelCenterOffset
        , int fontSize, Brush textColor, Color textOutlineColor, Brush backgroundColor)
        : base(adornedElement)
    {
        _centerOffset = labelCenterOffset;
        _label = new Label
        {
            Content = content,
            FontSize = fontSize,
            Padding = new Thickness(3),
            Background = backgroundColor,
            Foreground = textColor,
            IsHitTestVisible = false,
            Effect = new DropShadowEffect{Color = textOutlineColor, BlurRadius = 5, ShadowDepth = 0, Direction = 0, Opacity = 1}
        };

        AddVisualChild(_label);
        AddLogicalChild(_label);

        IsHitTestVisible = false;
    }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index)
    {
        if (index != 0)
            throw new ArgumentOutOfRangeException();

        return _label;
    }

    protected override Size MeasureOverride(Size constraint)
    {
        _label.Measure(constraint);
        return _label.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Size size = _label.DesiredSize;

        _label.Arrange(new Rect(-size.Width/2+_centerOffset.X, -size.Height/2+_centerOffset.Y, size.Width, size.Height));

        return finalSize;
    }

    public static void AddLabelAdornerToElement(FrameworkElement element, string labelContent, Point labelCenterOffset, int fontSize,
        Brush? textColor = null, Color? textOutlineColor = null, Brush? backgroundColor = null)
    {
        //poczekanie az elementy zostana utworzone zeby mogl znalezc poprawnie AdornerLayer
        element.Loaded += OnPointLoaded;

        void OnPointLoaded(object sender, RoutedEventArgs e)
        {
            element.Loaded -= OnPointLoaded;
            var layer = AdornerLayer.GetAdornerLayer(element);
            if (layer == null) return;
            var adorner = new LabelAdorner(element, labelContent, labelCenterOffset, fontSize, textColor ?? Brushes.White, textOutlineColor ?? Colors.Black, backgroundColor ?? Brushes.Transparent);
            layer.Add(adorner);
        }
    }
}