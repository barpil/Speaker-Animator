using System.Windows;
using SpeakerAnimator.View;
using SpeakerAnimator.View.Mouth.BlendShapes;

namespace SpeakerAnimator.Model.Mouth.BlendShapes;


public sealed class MouthPoseDriver
{
    private string[] _keys = Array.Empty<string>();
    private double[] _pose = Array.Empty<double>();
    

    public bool IsReady => _keys.Length > 0 && BlendShapesPoseStorage.IsInitialized;

    private readonly OneEuroFilter _filter = new();

    public double MinCutoff { get; set; } = 1.5;
    public double Beta { get; set; } = 0.3;

    public void Configure(string[] shapeKeys)
    {
        _keys = shapeKeys;
        _pose = new double[_keys.Length];
        _filter.Configure(_keys.Length);
        _filter.Reset();
    }

    public double[] ComputeSmoothedPose(double[] target, double dt)
    {
        if (!IsReady || target.Length != _pose.Length)
            return Array.Empty<double>();

        _filter.MinCutoff = MinCutoff;
        double curMag = 0, tgtMag = 0;
        for (int i = 0; i < target.Length; i++)
        {
            curMag += _pose[i] * _pose[i];
            tgtMag += target[i] * target[i];
        }

        _filter.Beta = tgtMag < curMag ? Beta * 0.5 : Beta;
        return _filter.Filter(target, dt, _pose);
    }
    
    public void SnapTo(double[] target)
    {
        if (target.Length != _pose.Length) return;
        Array.Copy(target, _pose, target.Length);
        _filter.Snap(target);
    }

    public List<(MovablePointVisual point, Point position)> ComputeMoves(double[] pose)
    {
        if (!IsReady || pose.Length != _keys.Length) 
            return new List<(MovablePointVisual, Point)>();

        var dict = new Dictionary<string, double>(_keys.Length);
        for (int i = 0; i < _keys.Length; i++)
            dict[_keys[i]] = pose[i];

        return BlendShapeExecutor.ComputeBlendShapeMoves(new ExecutePoseArgs { PoseWeights = dict });
    }

    public void ApplyMoves(List<(MovablePointVisual point, Point position)> moves)
    {
        var ctrl = AnimatedMouthController.Instance;

        if (ctrl == null)
        {
            foreach (var (point, position) in moves) point.MoveTo(position);
            return;
        }

        ctrl.BeginBatchUpdate();
        try
        {
            foreach (var (point, position) in moves) point.MoveTo(position);
        }
        finally
        {
            ctrl.EndBatchUpdate();
        }
    }

    public void Apply(double[] target, double dt, bool smooth)
    {
        double[] pose;

        if (smooth)
        {
            pose = ComputeSmoothedPose(target, dt);
        }
        else
        {
            SnapTo(target);
            pose = target;
        }

        if (pose.Length == 0) return;
        ApplyMoves(ComputeMoves(pose));
    }

    public void Reset(double[]? silencePose = null)
    {
        if (!BlendShapesPoseStorage.IsInitialized) return;

        var target = silencePose is { Length: > 0 } && silencePose.Length == _pose.Length
            ? silencePose
            : new double[_pose.Length];

        SnapTo(target);
        ApplyMoves(ComputeMoves(target));
    }
}