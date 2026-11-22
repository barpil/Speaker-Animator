using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using NAudio.Wave;
using SpeakerAnimator.Model.Mouth.BlendShapes;
using SpeakerAnimator.Model.NN;

namespace SpeakerAnimator.View.Panels;

public sealed class LiveVisemeAnimator : IDisposable
{
    private readonly VisemeOnnxPredictor _predictor;
    private readonly MouthPoseDriver _driver;
    private readonly Dispatcher _dispatcher;

    private WaveIn? _waveIn;
    private DispatcherTimer? _timer;

    private float[] _ring = Array.Empty<float>();
    private int _ringWrite;
    private int _ringCount;
    private readonly object _ringLock = new();

    private int _busy;
    private string[] _labels = Array.Empty<string>();
    private string[] _shapeKeys = Array.Empty<string>();
    private double[] _silence = Array.Empty<double>();

    private readonly Stopwatch _clock = new();
    private double _lastPoseTime;

    public double WindowSeconds { get; set; } = 0.8;
    public int InferEveryMilliseconds { get; set; } = 50;
    public double ProbabilityFloor { get; set; } = 0.3;
    public double PoseGain { get; set; } = 1.0;
    public int EdgeSkipFrames { get; set; } = 4;
    public int AggregateFrames { get; set; } = 6; 

    private double[]? _targetPose;
    private DispatcherTimer? _renderTimer;
    private readonly Stopwatch _renderClock = new();
    private double _lastRenderTime;

    public int RenderIntervalMilliseconds { get; set; } = 16;
    
    public bool IsRunning { get; private set; }

    public event Action? Started;
    public event Action? Stopped;
    public event Action<VisemeFramePrediction>? VisemeDetected;
    public event Action<string>? StatusChanged;

    public LiveVisemeAnimator(VisemeOnnxPredictor predictor, MouthPoseDriver driver)
    {
        _predictor = predictor;
        _driver = driver;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    public void Start()
    {
        if (IsRunning) return;

        if (!BlendShapesPoseStorage.IsInitialized)
            throw new InvalidOperationException("Mouth view is not loaded yet.");

        (_shapeKeys, _silence) = VisemePoseTimeline.BuildSchema(_predictor.Labels);
        _labels = _predictor.Labels.ToArray();
        _driver.Configure(_shapeKeys);

        _ring = new float[(int)(WindowSeconds * VisemeOnnxPredictor.SampleRate)];
        _ringWrite = 0;
        _ringCount = 0;

        _waveIn = new WaveIn
        {
            WaveFormat = new WaveFormat(VisemeOnnxPredictor.SampleRate, 16, 1),
            BufferMilliseconds = 20
        };
        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.StartRecording();

        _clock.Restart();
        _lastPoseTime = 0;

        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(InferEveryMilliseconds)
        };
        _timer.Tick += OnInferTick;
        _timer.Start();
        
        _targetPose = null;
        _renderClock.Restart();
        _lastRenderTime = 0;

        _renderTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(RenderIntervalMilliseconds)
        };
        _renderTimer.Tick += OnRenderTick;
        _renderTimer.Start();

        IsRunning = true;
        Started?.Invoke();
        StatusChanged?.Invoke("Live animation running (no audio output).");
    }

    public void Stop()
    {
        if (!IsRunning) return;

        _timer?.Stop();
        _timer = null;

        if (_waveIn != null)
        {
            _waveIn.DataAvailable -= OnDataAvailable;
            try { _waveIn.StopRecording(); } catch {}
            _waveIn.Dispose();
            _waveIn = null;
        }

        IsRunning = false;
        _clock.Stop();

        _driver.Reset(_silence);
        Stopped?.Invoke();
        _renderTimer?.Stop();
        _renderTimer = null;
        _renderClock.Stop();
        _targetPose = null;
        
        StatusChanged?.Invoke("Live animation stopped.");
    }
    
    private void OnRenderTick(object? sender, EventArgs e)
    {
        if (!IsRunning || _targetPose == null) return;

        double now = _renderClock.Elapsed.TotalSeconds;
        double dt = _lastRenderTime <= 0
            ? RenderIntervalMilliseconds / 1000.0
            : now - _lastRenderTime;
        _lastRenderTime = now;

        _driver.Apply(_targetPose, dt, smooth: true);
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_ringLock)
        {
            for (int i = 0; i + 1 < e.BytesRecorded; i += 2)
            {
                float sample = BitConverter.ToInt16(e.Buffer, i) / 32768f;
                _ring[_ringWrite] = sample;
                _ringWrite = (_ringWrite + 1) % _ring.Length;
                if (_ringCount < _ring.Length) _ringCount++;
            }
        }
    }

    private float[] Snapshot()
    {
        lock (_ringLock)
        {
            var buffer = new float[_ring.Length];

            int start = (_ringWrite - _ringCount + _ring.Length) % _ring.Length;
            int first = Math.Min(_ringCount, _ring.Length - start);
            int offset = _ring.Length - _ringCount;

            Array.Copy(_ring, start, buffer, offset, first);
            Array.Copy(_ring, 0, buffer, offset + first, _ringCount - first);
            return buffer;
        }
    }

    private readonly object _thisLock = new();
    private double _silenceCandidateSince = -1;
    public double SilenceHoldSeconds { get; set; } = 0.10;
    private void OnInferTick(object? sender, EventArgs e)
    {
        if (!IsRunning) return;

        int minSamples = (int)(0.3 * VisemeOnnxPredictor.SampleRate);
        if (_ringCount < minSamples) return;

        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return;

        var audio = Snapshot();
        double now = _clock.Elapsed.TotalSeconds;

        Task.Run(() =>
        {
            try
            {
                var result = _predictor.Predict(audio);
                if (result.Frames.Count == 0) return;

                int last = result.Frames.Count - 1 - EdgeSkipFrames;
                if (last < 0) return;
                int first = Math.Max(0, last - AggregateFrames + 1);

                var avg = new float[_labels.Length];
                int count = 0;
                for (int i = first; i <= last; i++)
                {
                    var p = result.Frames[i].Probabilities;
                    for (int c = 0; c < avg.Length; c++) avg[c] += p[c];
                    count++;
                }
                for (int c = 0; c < avg.Length; c++) avg[c] /= count;

                int best = 0;
                for (int c = 1; c < avg.Length; c++)
                    if (avg[c] > avg[best]) best = c;

                var pose = VisemePoseTimeline.Blend(
                    avg, _labels, _shapeKeys, _silence, ProbabilityFloor, PoseGain);
                
                double backSeconds = (EdgeSkipFrames + (count - 1) / 2.0) * 0.01;

                double dt;
                lock (_thisLock)
                {
                    dt = _lastPoseTime <= 0
                        ? InferEveryMilliseconds / 1000.0
                        : now - _lastPoseTime;
                    _lastPoseTime = now;
                }

                var smoothedPose = _driver.ComputeSmoothedPose(pose, dt);
                var moves = _driver.ComputeMoves(smoothedPose);

                _dispatcher.BeginInvoke(() =>
                {
                    if (!IsRunning) return;
                    bool isSilent = _labels[best] == "sil";

                    if (isSilent)
                    {
                        if (_silenceCandidateSince < 0) _silenceCandidateSince = now;
                        if (now - _silenceCandidateSince < SilenceHoldSeconds)
                            return;
                    }
                    else
                    {
                        _silenceCandidateSince = -1;
                    }

                    _targetPose = pose;
                    VisemeDetected?.Invoke(new VisemeFramePrediction
                    {
                        FrameIndex = 0,
                        TimeSeconds = (float)(now-backSeconds),
                        Viseme = _labels[best],
                        Confidence = avg[best],
                        Probabilities = avg
                    });
                });
            }
            catch (Exception ex)
            {
                _dispatcher.BeginInvoke(() => 
                    StatusChanged?.Invoke($"Live error: {ex.Message}"));
            }
            finally
            {
                Interlocked.Exchange(ref _busy, 0);
            }
        });
    }

    public void Dispose() => Stop();
}