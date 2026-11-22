using SpeakerAnimator.Model.NN;

namespace SpeakerAnimator.View.Panels;


public sealed class VisemePoseTimeline
{
    private readonly double[] _times;
    private readonly double[][] _poses;

    public string[] ShapeKeys { get; }
    public double[] SilencePose { get; }
    public int FrameCount => _times.Length;

    private VisemePoseTimeline(string[] shapeKeys, double[] silencePose,
                               double[] times, double[][] poses)
    {
        ShapeKeys = shapeKeys;
        SilencePose = silencePose;
        _times = times;
        _poses = poses;
    }

    public static VisemePoseTimeline Build(
        VisemePredictionResult result,
        double probabilityFloor,
        double poseGain)
    {
        var labels = result.Labels.ToArray();

        string[] shapeKeys = labels
            .SelectMany(l => VisemeToPoseMapper.GetPoseDict(l).Keys)
            .Distinct()
            .ToArray();

        double[] silence = ToVector(VisemeToPoseMapper.GetPoseDict("sil"), shapeKeys);

        var times = new double[result.Frames.Count];
        var poses = new double[result.Frames.Count][];

        for (int i = 0; i < result.Frames.Count; i++)
        {
            var frame = result.Frames[i];
            times[i] = frame.TimeSeconds;
            poses[i] = Blend(frame.Probabilities, labels, shapeKeys, silence,
                             probabilityFloor, poseGain);
        }

        return new VisemePoseTimeline(shapeKeys, silence, times, poses);
    }


    public static double[] Blend(
        float[] probabilities, string[] labels, string[] shapeKeys,
        double[] silencePose, double probabilityFloor, double poseGain)
    {
        double total = 0;
        var survivors = new List<(int Index, double W)>(probabilities.Length);

        for (int i = 0; i < probabilities.Length; i++)
        {
            double w = Math.Max(0.0, probabilities[i] - probabilityFloor);
            if (w > 0) { survivors.Add((i, w)); total += w; }
        }

        if (survivors.Count == 0 || total <= 1e-9)
            return (double[])silencePose.Clone();

        var pose = new double[shapeKeys.Length];

        foreach (var (index, p) in survivors)
        {
            double weight = p / total;
            var poseDict = VisemeToPoseMapper.GetPoseDict(labels[index]);

            for (int k = 0; k < shapeKeys.Length; k++)
                if (poseDict.TryGetValue(shapeKeys[k], out double value))
                    pose[k] += value * weight;
        }

        if (Math.Abs(poseGain - 1.0) > 1e-9)
            for (int k = 0; k < pose.Length; k++)
                pose[k] *= poseGain;

        return pose;
    }

    private static double[] ToVector(Dictionary<string, double> poseDict, string[] shapeKeys)
    {
        var v = new double[shapeKeys.Length];
        for (int k = 0; k < shapeKeys.Length; k++)
            if (poseDict.TryGetValue(shapeKeys[k], out double value))
                v[k] = value;
        return v;
    }
    
    public double[] Sample(double time)
    {
        if (_times.Length == 0) return new double[ShapeKeys.Length];
        if (time <= _times[0]) return (double[])_poses[0].Clone();
        if (time >= _times[^1]) return (double[])_poses[^1].Clone();

        int lo = 0, hi = _times.Length - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (_times[mid] <= time) lo = mid; else hi = mid;
        }

        double span = _times[hi] - _times[lo];
        double t = span > 1e-9 ? (time - _times[lo]) / span : 0;

        var a = _poses[lo];
        var b = _poses[hi];
        var result = new double[ShapeKeys.Length];

        for (int k = 0; k < result.Length; k++)
            result[k] = a[k] + (b[k] - a[k]) * t;

        return result;
    }
    
    public static (string[] Keys, double[] Silence) BuildSchema(IEnumerable<string> labels)
    {
        var keys = labels.SelectMany(l => VisemeToPoseMapper.GetPoseDict(l).Keys)
            .Distinct().ToArray();
        return (keys, ToVector(VisemeToPoseMapper.GetPoseDict("sil"), keys));
    }
    
    public int IndexAt(double time)
    {
        if (_times.Length == 0) return -1;
        if (time <= _times[0]) return 0;
        if (time >= _times[^1]) return _times.Length - 1;

        int lo = 0, hi = _times.Length - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (_times[mid] <= time) lo = mid; else hi = mid;
        }
        return (time - _times[lo] <= _times[hi] - time) ? lo : hi;
    }
}