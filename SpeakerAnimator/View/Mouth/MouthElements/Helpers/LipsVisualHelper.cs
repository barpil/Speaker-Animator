using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SpeakerAnimator.Model.utils;

namespace SpeakerAnimator.View.MouthElementHelpers;

public static class LipsVisualHelper
{
    private const string DEFAULT_FILL_COLOR_HEX = "#F2A65A";
    private const string DEFAULT_OUTLINE_COLOR_HEX = "#F58549";
    
    public static MovableLipPointVisual CreateLipPointVisual(Canvas parentCanvas, double radius, Point point, string? fillColorHex = null, string? outlineColorHex = null)
    {
        Brush fillColor = BrushExtensions.GetBrushFromHexColor(fillColorHex ?? DEFAULT_FILL_COLOR_HEX);
        Brush outlineColor = BrushExtensions.GetBrushFromHexColor(outlineColorHex ?? DEFAULT_FILL_COLOR_HEX);
        var mouthPointVisual = new MovableLipPointVisual(parentCanvas, point, fillColor, outlineColor, radius: radius);

        return mouthPointVisual;
    }
    
    
    public static (PolyBezierSegment, PathFigure, Path) CreateLipOutlineSplinePath(double lineThickness, string? fillColorHex = null, string? outlineColorHex = null, bool isFigureClosed = true)
    {
        Brush fillColor = BrushExtensions.GetBrushFromHexColor(fillColorHex ?? DEFAULT_FILL_COLOR_HEX);
        Brush outlineColor = BrushExtensions.GetBrushFromHexColor(outlineColorHex ?? DEFAULT_OUTLINE_COLOR_HEX);
        
        var polyBezier = new PolyBezierSegment { IsStroked = true };
        var figure = new PathFigure { IsClosed = isFigureClosed, Segments = { polyBezier } };
        var geometry = new PathGeometry([figure]);
        var splinePath = new Path
            { Data = geometry, Stroke = outlineColor, Fill = fillColor, StrokeThickness = lineThickness };

        return (polyBezier, figure, splinePath);
    }
}