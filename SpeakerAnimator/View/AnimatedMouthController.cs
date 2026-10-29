using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SpeakerAnimator.Model.Splines;
using SpeakerAnimator.Model.utils;

namespace SpeakerAnimator.View;

public class AnimatedMouthController
{
    private const string DEFAULT_FILL_COLOR_HEX = "#F2A65A";
    private const string DEFAULT_OUTLINE_COLOR_HEX = "#F58549";
    private const double KEY_POINT_RADIUS = 10;
    private const double LINE_THICKNESS = 4;
    private const double CATMULL_ROM_TENSION = 1;

    
    private readonly Canvas _canvas;
    private readonly List<Point> _mouthKeyPointPositions;
    private readonly List<MovableMouthPointVisual> _mouthPointVisuals;

    private PolyBezierSegment _polyBezierSegment;
    private PathFigure _figure;
    private Path _splinePath;
    
    
    public AnimatedMouthController(Canvas parentCanvas, List<Point> initialPointPositions,
        string? keyPointFillColorHex = null, string? keyPointOutlineColorHex = null,
        string? lineFillColorHex = null, string? lineOutlineColorHex = null)
    {
        _canvas = parentCanvas;
        _mouthKeyPointPositions = new List<Point>(initialPointPositions);
        _mouthPointVisuals = new List<MovableMouthPointVisual>();


        for (int i = 0; i < _mouthKeyPointPositions.Count; i++)
        {
            int idx = i;
            var mouthPointVisual =
                CreateMouthPointVisual(_mouthKeyPointPositions[i], keyPointFillColorHex, keyPointOutlineColorHex);
            mouthPointVisual.PositionChanged += newPosition => OnKeyPointMoved(idx, newPosition);
            _mouthPointVisuals.Add(mouthPointVisual);
        }

        (_polyBezierSegment, _figure, _splinePath) = CreateMouthOutlineSplinePath(lineFillColorHex, lineOutlineColorHex);
        _canvas.Children.Add(_splinePath);
        UpdateVisuals();
    }


    public MovableMouthPointVisual CreateMouthPointVisual(Point point, string? fillColorHex = null, string? outlineColorHex = null)
    {
        Brush fillColor = BrushExtensions.GetBrushFromHexColor(fillColorHex ?? DEFAULT_FILL_COLOR_HEX);
        Brush outlineColor = BrushExtensions.GetBrushFromHexColor(outlineColorHex ?? DEFAULT_FILL_COLOR_HEX);
        var mouthPointVisual = new MovableMouthPointVisual(_canvas, point, fillColor, outlineColor, radius: KEY_POINT_RADIUS);

        return mouthPointVisual;
    }


    private (PolyBezierSegment, PathFigure, Path) CreateMouthOutlineSplinePath(string? fillColorHex = null, string? outlineColorHex = null, bool isFigureClosed = true)
    {
        Brush fillColor = BrushExtensions.GetBrushFromHexColor(fillColorHex ?? DEFAULT_FILL_COLOR_HEX);
        Brush outlineColor = BrushExtensions.GetBrushFromHexColor(outlineColorHex ?? DEFAULT_OUTLINE_COLOR_HEX);
        
        var polyBezier = new PolyBezierSegment { IsStroked = true };
        var figure = new PathFigure { IsClosed = isFigureClosed, Segments = { polyBezier } };
        var geometry = new PathGeometry([figure]);
        var splinePath = new Path
            { Data = geometry, Stroke = outlineColor, Fill = fillColor, StrokeThickness = LINE_THICKNESS };

        return (polyBezier, figure, splinePath);
    }

    private void OnKeyPointMoved(int keyPointIdx, Point newPosition)
    {
        _mouthKeyPointPositions[keyPointIdx] = newPosition;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (_mouthKeyPointPositions.Count < 2)
        {
            _polyBezierSegment.Points = new PointCollection();
            return;
        }

        List<Point> bezierPoints = CatmullRomHelper.GetBezierKeyPointsFromCatmullRomKeyPoints(
            _mouthKeyPointPositions,
            isClosed: _figure.IsClosed,
            tension: CATMULL_ROM_TENSION);

        if (bezierPoints.Count >= 4 && (bezierPoints.Count - 1) % 3 == 0)
        {
            _figure.StartPoint = bezierPoints[0];
            _polyBezierSegment.Points = new PointCollection(bezierPoints.Skip(1));
        }
        else
        {
            _polyBezierSegment.Points = new PointCollection();
        }
    }
}