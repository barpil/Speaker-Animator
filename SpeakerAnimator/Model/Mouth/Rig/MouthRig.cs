using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SpeakerAnimator.Model;
using SpeakerAnimator.Model.Mouth.Rig;

namespace SpeakerAnimator.View.BlendShapes;

public class MouthRig
{
    private readonly Canvas _parentCanvas;
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
        foreach (var (rpName, rpPos) in initialRigPointPositions)
        {
            CreateMouthRigPoint(rpName, rpPos);
        }
        StateController.EditableRigPointsToggled += CalculateWeights;
    }

    private void CreateMouthRigPoint(string rigPointName, Point initialPoint)
    {
        var rigPointVisual = new MovableMouthRigPointVisual(_parentCanvas, initialPoint, Brushes.Blue, Brushes.Black, rigPointName);
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

    public void CalculateWeights()
    {
        if (!_rigPoints.TryGetValue("left_side", out var leftRP) || !_rigPoints.TryGetValue("right_side", out var rightRP) ||
            !_rigPoints.TryGetValue("top_lip", out var topRP) || !_rigPoints.TryGetValue("bottom_lip", out var bottomRP) ||
            !_rigPoints.TryGetValue("jaw", out var jawRP))
        {
            return;
        }

        Point L = leftRP.RigPointVisual.NeutralPosition;
        Point R = rightRP.RigPointVisual.NeutralPosition;
        Point C = new Point((L.X + R.X) / 2.0, (L.Y + R.Y) / 2.0);
        
        Vector XAxis = Point.Subtract(R, L);
        double xLen = XAxis.Length;
        if (xLen < 0.001) return;
        XAxis.Normalize();
        
        Vector YAxis = new Vector(XAxis.Y, -XAxis.X);
        
        foreach (var reg in _registeredPoints)
        {
            var point = reg.Point;
            _pointWeights[point].Clear();

            if (reg.IsTeeth)
            {
                _pointWeights[point][jawRP] = 1.0;
                continue;
            }
            
            Vector CP = Point.Subtract(point.NeutralPosition, C);
            
            double u = Vector.Multiply(CP, XAxis);
            double uNorm = Math.Max(-1.0, Math.Min(1.0, u / (xLen / 2.0)));
            
            double v = Vector.Multiply(CP, YAxis);
            
            double centerFalloff = Math.Cos(uNorm * Math.PI / 2.0);
            if (centerFalloff < 0) centerFalloff = 0;

            // Calculate raw weights based on position
            double wLeft  = Math.Max(0, -uNorm);
            double wRight = Math.Max(0, uNorm);
            
            double wTop    = Math.Max(0, v) * centerFalloff;
            double wBottom = Math.Max(0, -v) * centerFalloff;

            double wJaw = Math.Max(0, -v) * (0.3 + 0.7 * centerFalloff);
            
            _pointWeights[point][leftRP]   = wLeft;
            _pointWeights[point][rightRP]  = wRight;
            _pointWeights[point][topRP]    = wTop;
            _pointWeights[point][bottomRP] = wBottom;
            _pointWeights[point][jawRP]    = wJaw;
            
            double total = wLeft + wRight + wTop + wBottom + wJaw;
            if (total > 0.0001)
            {
                foreach (var key in _pointWeights[point].Keys.ToList())
                {
                    _pointWeights[point][key] /= total;
                }
            }
        }
    }

    private void OnRigPointMoved(Point newPos)
    {
        //nie ruszamy ust jezeli edytujemy pozycje rigpointow
        if (StateController.EditableRigPoints) return;

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

                Vector offset = Point.Subtract(rigPoint.RigPointVisual.CurrentPosition, rigPoint.RigPointVisual.NeutralPosition);
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
}