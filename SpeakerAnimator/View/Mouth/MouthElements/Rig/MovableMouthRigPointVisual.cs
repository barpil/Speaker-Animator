using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SpeakerAnimator.Model;
using SpeakerAnimator.View.utils;

namespace SpeakerAnimator.View.BlendShapes;

public class MovableMouthRigPointVisual : MovablePointVisual
{

    
    
    public MovableMouthRigPointVisual(
        Canvas parentCanvas,
        Point initialPosition,
        Brush fillColor,
        Brush outlineColor,
        string labelContent,
        double radius = 10)
        : base(parentCanvas, initialPosition, fillColor, outlineColor, radius)
    {
        LabelAdorner.AddLabelAdornerToElement(
            PointGraphicElement, labelContent, new Point(radius,radius-18), 12);
        ZIndex = 20;
    }

    
    
    
    
    
}