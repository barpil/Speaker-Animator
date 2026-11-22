namespace SpeakerAnimator.Model.Mouth.BlendShapes;

public sealed class OneEuroFilter
{
    public double MinCutoff { get; set; } = 1.5;
    public double Beta { get; set; } = 0.6;
    public double DCutoff { get; set; } = 1.0;

    private double[] _x = Array.Empty<double>();
    private double[] _dx = Array.Empty<double>();
    private bool _initialized;

    public void Configure(int n)
    {
        if (_x.Length == n) return;
        _x = new double[n];
        _dx = new double[n];
        _initialized = false;
    }

    public void Snap(double[] target)
    {
        Configure(target.Length);
        Array.Copy(target, _x, target.Length);
        Array.Clear(_dx, 0, target.Length);
        _initialized = true;
    }

    public void Reset() => _initialized = false;
    
    public double[] Filter(double[] target, double dt, double[] output)
    {
        int n = target.Length;
        Configure(n);

        if (!_initialized || dt <= 0)
        {
            Snap(target);
            Array.Copy(_x, output, n);
            return output;
        }

        double ad = Alpha(DCutoff, dt);

        for (int i = 0; i < n; i++)
        {
            double deriv = (target[i] - _x[i]) / dt;
            double dxHat = ad * deriv + (1.0 - ad) * _dx[i];

            double cutoff = MinCutoff + Beta * Math.Abs(dxHat);
            double a = Alpha(cutoff, dt);

            _x[i] = a * target[i] + (1.0 - a) * _x[i];
            _dx[i] = dxHat;
            output[i] = _x[i];
        }

        return output;
    }

    private static double Alpha(double cutoff, double dt)
    {
        double r = 2.0 * Math.PI * cutoff * dt;
        return r / (r + 1.0);
    }
}