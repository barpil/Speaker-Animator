using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SpeakerAnimator.Model;
using SpeakerAnimator.Model.Mouth.BlendShapes;
using SpeakerAnimator.Model.Splines;
using SpeakerAnimator.Model.utils;
using SpeakerAnimator.View.BlendShapes;
using SpeakerAnimator.View.MouthElementHelpers;

namespace SpeakerAnimator.View;

public class AnimatedMouthController
{
    public static AnimatedMouthController? Instance { get; private set; }
    
    

    private const double KEY_POINT_RADIUS = 10;
    private const double LINE_THICKNESS = 4;
    private const double CATMULL_ROM_TENSION = 1;
    private const double TOOTH_WIDTH = 30;
    private const double TOOTH_HEIGHT = 50;
    private const double TEETH_GAP_PRC = 0.1;

    private readonly List<Point> _defaultLipPositions;
    private readonly List<Point> _defaultTeethPositions;
    private readonly List<(string, Point)> _defaultRigPositions;
    
    private readonly Canvas _parentCanvas;
    private readonly List<Point> _lipKeyPointPositions;
    public List<MovableLipPointVisual> LipPointVisuals { get; private set; }
    private readonly List<Point> _teethKeyPointPositions;
    public List<MovableTeethPointVisual> TeethPointVisuals { get; private set; }

    private PolyBezierSegment _polyBezierSegment;
    private PathFigure _figure;
    private Path _splinePath;

    private Canvas _teethCanvas;

    private MouthRig _mouthRig;
    
    
    public AnimatedMouthController(Canvas parentParentCanvas, List<Point> initialLipPointPositions, List<Point> initialTeethPointPositions,
        List<(string, Point)> initialRigPositions,
        string? keyPointFillColorHex = null, string? keyPointOutlineColorHex = null,
        string? lineFillColorHex = null, string? lineOutlineColorHex = null)
    {
        _parentCanvas = parentParentCanvas;
        _lipKeyPointPositions = new List<Point>(initialLipPointPositions);
        _defaultLipPositions = new List<Point>(initialLipPointPositions);
        LipPointVisuals = new List<MovableLipPointVisual>();
        _teethKeyPointPositions = new List<Point>(initialTeethPointPositions);
        _defaultTeethPositions = new List<Point>(initialTeethPointPositions);
        TeethPointVisuals = new List<MovableTeethPointVisual>();
        _teethCanvas = new Canvas();

        _defaultRigPositions = new List<(string, Point)>(initialRigPositions);
        
        //usta
        for (int i = 0; i < _lipKeyPointPositions.Count; i++)
        {
            int idx = i;
            var mouthPointVisual =
                LipsVisualHelper.CreateLipPointVisual(_parentCanvas, KEY_POINT_RADIUS, _lipKeyPointPositions[i], keyPointFillColorHex, keyPointOutlineColorHex);
            mouthPointVisual.PositionChanged += newPosition => OnLipKeyPointMoved(idx, newPosition);
            LipPointVisuals.Add(mouthPointVisual);
        }
        (_polyBezierSegment, _figure, _splinePath) = LipsVisualHelper.CreateLipOutlineSplinePath(LINE_THICKNESS, lineFillColorHex, lineOutlineColorHex);
        _parentCanvas.Children.Add(_splinePath);
        
        //zeby
        for (int i = 0; i < _teethKeyPointPositions.Count; i++)
        {
            int idx = i;
            var toothPointVisual =
                TeethVisualHelper.CreateToothPointVisual(_parentCanvas, _teethKeyPointPositions[i], KEY_POINT_RADIUS);
            toothPointVisual.PositionChanged += newPosition => OnToothKeyPointMoved(idx, newPosition);
            TeethPointVisuals.Add(toothPointVisual);
        }
        _parentCanvas.Children.Add(_teethCanvas);

        //rig
        if (StateController.MouthMovementOption == MouthMovementOption.Rig)
        {
            _mouthRig = new MouthRig(_parentCanvas, initialRigPositions);
            foreach (var lp in LipPointVisuals)
            {
                _mouthRig.RegisterPoint(lp, isTeeth: false);
            }

            foreach (var tp in TeethPointVisuals)
            {
                _mouthRig.RegisterPoint(tp, isTeeth: true);
            }

            _mouthRig.CalculateWeights();
        }else if(StateController.MouthMovementOption == MouthMovementOption.Blendshapes)
        { 
            BlendShapesPoseStorage.Initialize(TeethPointVisuals.Concat<MovablePointVisual>(LipPointVisuals).ToList());  
        }
        
        
        Instance = this;
        UpdateVisuals();
    }


    
    
    


    

    

    private void OnLipKeyPointMoved(int keyPointIdx, Point newPosition)
    {
        _lipKeyPointPositions[keyPointIdx] = newPosition;
        UpdateLipsVisuals();
    }

    private void OnToothKeyPointMoved(int keyPointIdx, Point newPosition)
    {
        _teethKeyPointPositions[keyPointIdx] = newPosition;
        UpdateTeethVisuals();
    }

    private void UpdateLipsVisuals()
    {
        if (_lipKeyPointPositions.Count < 2)
        {
            _polyBezierSegment.Points = new PointCollection();
            return;
        }

        List<Point> bezierPoints = CatmullRomHelper.GetBezierKeyPointsFromCatmullRomKeyPoints(
            _lipKeyPointPositions,
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

    private void UpdateTeethVisuals()
    {
        _teethCanvas.Children.Clear();
        //przyciecie plotna zeby tylko zeby wewnatrz ust byly widoczne
        _teethCanvas.Clip = _splinePath.Data;
        for (int i = 0; i < _teethKeyPointPositions.Count - 1; i += 2)
        {
            Point a = _teethKeyPointPositions[i];
            Point b = _teethKeyPointPositions[i + 1];
            TeethVisualHelper.CreateTeethStripVisual(_teethCanvas, a, b, TOOTH_HEIGHT, TOOTH_WIDTH, TEETH_GAP_PRC);
        }
    }
    

    private void UpdateVisuals()
    {
        UpdateLipsVisuals();
        UpdateTeethVisuals();
    }
    
    
    public void ResetToDefaultPositions()
    {
        for (int i = 0; i < LipPointVisuals.Count; i++)
        {
            Point defaultPos = _defaultLipPositions[i];

            LipPointVisuals[i].MoveTo(defaultPos);

            LipPointVisuals[i].NeutralPosition = defaultPos;
 
            _lipKeyPointPositions[i] = defaultPos;
        }

        for (int i = 0; i < TeethPointVisuals.Count; i++)
        {
            Point defaultPos = _defaultTeethPositions[i];
            
            TeethPointVisuals[i].MoveTo(defaultPos);
            TeethPointVisuals[i].NeutralPosition = defaultPos;
            _teethKeyPointPositions[i] = defaultPos;
        }

        if (StateController.MouthMovementOption == MouthMovementOption.Rig)
        {
            _mouthRig.ResetToDefaultPositions(_defaultRigPositions);
        }
        else if(StateController.MouthMovementOption == MouthMovementOption.Blendshapes)
        { 
            BlendShapesPoseStorage.Initialize(TeethPointVisuals.Concat<MovablePointVisual>(LipPointVisuals).ToList());  
        }
        
        
        UpdateVisuals();
    }
    
    //GETTERS/SETTERS
    public List<Point> GetMouthKeyPoints()
    {
        return _lipKeyPointPositions;
    }
    
    public List<Point> GetTeethKeyPoints()
    {
        return _teethKeyPointPositions;
    }
    
    
    
    
    
}