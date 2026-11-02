using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SpeakerAnimator.Model;
using SpeakerAnimator.Model.Mouth.Rig;

namespace SpeakerAnimator.View.BlendShapes;

public class MouthRig
{
    private readonly Canvas _parentCanvas;
    private readonly Canvas _rigCanvas;
    private readonly Dictionary<string, MouthRigPoint> _rigPoints = new();
    private readonly Dictionary<MovablePointVisual, Dictionary<MouthRigPoint, double>> _pointWeights = new();

    private class PointRegistration
    {
        public MovablePointVisual Point { get; set; }
        public bool IsTeeth { get; set; }
    }

    private readonly List<PointRegistration> _registeredPoints = new();

    public MouthRig(Canvas parentCanvas, List<(string, Point)> initialRigPointPositions)
    {
        _parentCanvas = parentCanvas;
        _rigCanvas = new Canvas();
        _parentCanvas.Children.Add(_rigCanvas);
        foreach (var (rpName, rpPos) in initialRigPointPositions)
        {
            CreateMouthRigPoint(rpName, rpPos);
        }

        StateController.EditablePointsToggled += CalculateWeights;
    }
    
    

    private void CreateMouthRigPoint(string rigPointName, Point initialPoint)
    {
        var rigPointVisual =
            new MovableMouthRigPointVisual(_rigCanvas, initialPoint, Brushes.Blue, Brushes.Black, rigPointName);
        var rigPoint = new MouthRigPoint(rigPointName, rigPointVisual);
        _rigPoints.Add(rigPointName, rigPoint);

        rigPointVisual.PositionChanged += OnRigPointMoved;
    }

    public void RegisterPoint(MovablePointVisual point, bool isTeeth = false)
    {
        if (!_pointWeights.ContainsKey(point))
        {
            _pointWeights[point] = new Dictionary<MouthRigPoint, double>();
            _registeredPoints.Add(new PointRegistration { Point = point, IsTeeth = isTeeth });
        }
    }

    //To podejscie z rig pointami calkowicie do wywalenia bo to dziala bardzo slabo
    //Trzeba zrobic blendshapes (zapisywanie jak wygladaja np. szerokie usta, otwarte usta, dziubek itd i potem sliderami przesuwanie miedzy nimi
    public void CalculateWeights()
    {
        if (!_rigPoints.TryGetValue("left_side", out var leftRP) ||
            !_rigPoints.TryGetValue("right_side", out var rightRP) ||
            !_rigPoints.TryGetValue("top_lip", out var topRP) ||
            !_rigPoints.TryGetValue("bottom_lip", out var bottomRP) ||
            !_rigPoints.TryGetValue("jaw", out var jawRP))
        {
            return;
        }
        
        double minX = leftRP.RigPointVisual.NeutralPosition.X;
        double maxX = rightRP.RigPointVisual.NeutralPosition.X;
        double minY = topRP.RigPointVisual.NeutralPosition.Y;
        double maxY = bottomRP.RigPointVisual.NeutralPosition.Y;

        double width = maxX - minX;
        double height = maxY - minY;
        if (width < 1) width = 1;
        if (height < 1) height = 1;

        foreach (var reg in _registeredPoints)
        {
            var point = reg.Point;
            _pointWeights[point].Clear();
            
            if (reg.IsTeeth)
            {
                _pointWeights[point][jawRP] = 1.0;
                continue;
            }
            double u = (point.NeutralPosition.X - minX) / width;
            double v = (point.NeutralPosition.Y - minY) / height;
            u = Math.Max(0, Math.Min(1, u));
            v = Math.Max(0, Math.Min(1, v));
            
            _pointWeights[point][leftRP] = 1.0 - u;
            _pointWeights[point][rightRP] = u;
            
            _pointWeights[point][topRP] = 1.0 - v;
            _pointWeights[point][bottomRP] = v;
            
            double jawInfluence = Math.Max(0, (v - 0.4) / 0.6);
            _pointWeights[point][jawRP] = jawInfluence;
        }
    }

    private void OnRigPointMoved(Point newPos)
    {
        //nie ruszamy ust jezeli edytujemy pozycje rigpointow
        if (StateController.EditablePoints) return;

        UpdateAllInfluencedPoints();
    }

    private void UpdateAllInfluencedPoints()
    {
        foreach (var reg in _registeredPoints)
        {
            var point = reg.Point;
            double finalX = point.NeutralPosition.X;
            double finalY = point.NeutralPosition.Y;

            foreach (var rigPointWeight in _pointWeights[point])
            {
                var rigPoint = rigPointWeight.Key;
                double weight = rigPointWeight.Value;
                if (weight <= 0.0001) continue;

                Vector offset = Point.Subtract(rigPoint.RigPointVisual.CurrentPosition,
                    rigPoint.RigPointVisual.NeutralPosition);
                finalX += offset.X * weight;
                finalY += offset.Y * weight;
            }

            //updatujemy pozycje tylko jezeli znaczaco przesunelismy rigpoint
            if (Math.Abs(point.CurrentPosition.X - finalX) > 0.1 || Math.Abs(point.CurrentPosition.Y - finalY) > 0.1)
            {
                point.MoveTo(new Point(finalX, finalY));
            }
        }
    }
    
    public void ResetToDefaultPositions(List<(string, Point)> defaultPositions)
    {
        foreach (var (name, pos) in defaultPositions)
        {
            if (_rigPoints.TryGetValue(name, out var rigPoint))
            {
                rigPoint.RigPointVisual.MoveTo(pos);
                rigPoint.RigPointVisual.NeutralPosition = pos; 
            }
        }
        CalculateWeights();
    }
}