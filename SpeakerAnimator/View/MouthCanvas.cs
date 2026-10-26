using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SpeakerAnimator.View;

public class MouthCanvas : Canvas
{
    private List<MovableMouthPointVisual> _mouthPointVisuals = new List<MovableMouthPointVisual>();
    
    
    Point[] DEFAULT_MOUTH_MOUTHPOINT_POSITIONS = {
        new(20, 20)
    };
    
    public MouthCanvas()
    {
        Background = Brushes.Transparent;

        foreach (var mouthPoint in DEFAULT_MOUTH_MOUTHPOINT_POSITIONS)
        {
            AddMouthPointVisual(mouthPoint.X, mouthPoint.Y);
        }
    }

    private const string DEFAULT_FILL_COLOR_HEX = "#F2A65A";
    private const string DEFAULT_OUTLINE_COLOR_HEX = "#F58549";
    
    public void AddMouthPointVisual(double x, double y, Brush? fillColor = null, Brush? outlineColor = null)
    {
        if (fillColor == null) fillColor = (Brush)new BrushConverter().ConvertFromString(DEFAULT_FILL_COLOR_HEX) ?? Brushes.BlueViolet;
        if (outlineColor == null) outlineColor = (Brush)new BrushConverter().ConvertFromString(DEFAULT_OUTLINE_COLOR_HEX) ?? Brushes.Black;;
        var draggableCircle = new MovableMouthPointVisual(this, new(x, y), fillColor, outlineColor, radius: 10);
            
        _mouthPointVisuals.Add(draggableCircle);
    }
    
}