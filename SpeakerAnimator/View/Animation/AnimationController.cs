using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NAudio.Wave;
using SpeakerAnimator.Model;
using SpeakerAnimator.Model.Mouth.BlendShapes;
using SpeakerAnimator.Model.NN;
using SpeakerAnimator.Model.utils;

namespace SpeakerAnimator.View.Panels;

public class AnimationController
{
    private enum PlaybackMode
    {
        None,
        AudioOnly,
        Animation
    }

    public static AnimationController Instance { get; private set; }
    
    private readonly VisemeOnnxPredictor _predictor;
    private readonly DispatcherTimer _timer;
    private readonly Dispatcher _dispatcher;
    
    private float[]? _audio;
    
    private WaveIn? _waveIn;
    private MemoryStream? _recordedBytes;
    private readonly object _recordLock = new();
    
    private WaveOut? _waveOut;
    private MemoryStream? _playStream;
    private RawSourceWaveStream? _rawStream;
    private PlaybackMode _mode = PlaybackMode.None;
    private double _playheadSeconds;
    private double _seekBaseSeconds;
    private double _lastSmoothedTime;
    
    private bool _isSeeking;
    private PlaybackMode _modeBeforeSeek = PlaybackMode.None;
    public bool IsPlayingAudioOnly => _mode == PlaybackMode.AudioOnly;

    public event Action? AudioPlaybackStarted;
    public event Action? AudioPlaybackStopped;
    
    private VisemePredictionResult? _lastPrediction;
    private VisemePoseTimeline? _timeline;
    
    public double ProbabilityFloor { get; set; } = 0.3;
    public double PoseGain { get; set; } = 1.0;
    public double SmoothingTimeSeconds { get; set; } = 0.045;
    public double AudioLatencyCompensationSeconds { get; set; } = 0.0;
    public int AnimationTickMilliseconds { get; set; } = 15;
    
    public bool IsRecording { get; private set; }
    public bool IsAnimating => _mode == PlaybackMode.Animation;
    public bool IsPlayingAudio => _mode != PlaybackMode.None;
    public bool HasAudio => _audio is { Length: > 0 };
    public bool HasPrediction => _timeline is { FrameCount: > 0 };
    public bool IsMouthReady => BlendShapesPoseStorage.IsInitialized;
    public double CurrentTimeSeconds => _playheadSeconds;
    public VisemePredictionResult? LastPrediction => _lastPrediction;

    public double AudioDuration =>
        HasAudio ? _audio!.Length / (double)VisemeOnnxPredictor.SampleRate : 0;

    public event Action<string>? StatusChanged;
    public event Action<string, double>? AudioLoaded;
    public event Action? RecordingStarted;
    public event Action<double>? RecordingStopped;
    public event Action<VisemePredictionResult>? PredictionCompleted;
    public event Action? AnimationStarted;
    public event Action? AnimationStopped;
    public event Action<double>? PositionChanged;
    private readonly MouthPoseDriver _driver = new();
    private readonly LiveVisemeAnimator _live;
    private int _lastFrameIndex = -1;

    public bool IsLive => _live.IsRunning;

    public event Action? LiveStarted;
    public event Action? LiveStopped;
    public event Action<VisemeFramePrediction>? LiveVisemeDetected;
    public event Action<int>? FrameIndexChanged;
    
    
    public static void Instantiate(VisemeOnnxPredictor predictor)
        => Instance = new AnimationController(predictor);

    public AnimationController(VisemeOnnxPredictor predictor)
    {
        _predictor = predictor;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(AnimationTickMilliseconds)
        };
        _timer.Tick += OnTick;
        _live = new LiveVisemeAnimator(predictor, _driver);
        _live.Started += () => LiveStarted?.Invoke();
        _live.Stopped += () => LiveStopped?.Invoke();
        _live.VisemeDetected += v => LiveVisemeDetected?.Invoke(v);
        _live.StatusChanged += SetStatus;
    }

    public void Shutdown()
    {
        StopLive();
        if (IsRecording) StopRecording();
        StopPlayback(resetMouth: true);
        CleanupRecording();
    }
    
    
    public void LoadAudioFromFile(string path)
    {
        StopLive();
        StopPlayback(resetMouth: true);
        if (IsRecording) StopRecording();

        SetStatus("Loading file...");

        var audio = AudioHelper.LoadFileAs16kMono(path);
        if (audio.Length == 0)
            throw new InvalidOperationException("No audio track found in the file.");

        SetAudio(audio, Path.GetFileName(path));
        SetStatus($"Loaded {AudioDuration:F2}s of audio.");
    }

    public void StartRecording()
    {
        if (IsRecording) return;
        StopLive();
        StopPlayback(resetMouth: true);

        _recordedBytes = new MemoryStream();
        _waveIn = new WaveIn
        {
            WaveFormat = new WaveFormat(VisemeOnnxPredictor.SampleRate, 16, 1),
            BufferMilliseconds = 50
        };

        _waveIn.DataAvailable += OnRecordDataAvailable;
        _waveIn.StartRecording();

        IsRecording = true;
        RecordingStarted?.Invoke();
        SetStatus("Recording from microphone.");
    }

    public void StopRecording()
    {
        if (_waveIn == null) return;

        _waveIn.StopRecording();

        byte[] pcm;
        lock (_recordLock)
        {
            pcm = _recordedBytes?.ToArray() ?? Array.Empty<byte>();
        }

        CleanupRecording();

        if (pcm.Length == 0)
        {
            SetStatus("Nothing recorded.");
            RecordingStopped?.Invoke(0);
            return;
        }

        SetAudio(AudioHelper.Pcm16BytesToFloats(pcm), "Microphone");
        RecordingStopped?.Invoke(AudioDuration);
        SetStatus($"Recorded {AudioDuration:F2}s of audio.");
    }

    private void OnRecordDataAvailable(object? sender, WaveInEventArgs args)
    {
        lock (_recordLock)
        {
            _recordedBytes?.Write(args.Buffer, 0, args.BytesRecorded);
        }
    }

    private void CleanupRecording()
    {
        if (_waveIn != null)
        {
            _waveIn.DataAvailable -= OnRecordDataAvailable;
            _waveIn.Dispose();
            _waveIn = null;
        }

        _recordedBytes?.Dispose();
        _recordedBytes = null;
        IsRecording = false;
    }
    
    private void SetAudio(float[] audio, string sourceName)
    {
        _audio = audio;
        _lastPrediction = null;
        _timeline = null;

        _playheadSeconds = 0;
        _seekBaseSeconds = 0;

        PositionChanged?.Invoke(0);
        AudioLoaded?.Invoke(sourceName, AudioDuration);
    }
    

    public async Task<VisemePredictionResult> PredictAsync()
    {
        if (!HasAudio)
            throw new InvalidOperationException("Load a file or record audio first.");

        SetStatus("Running model...");

        var audio = _audio!;
        var result = await Task.Run(() => _predictor.Predict(audio));

        _lastPrediction = result;
        RebuildPoseKeyframes();

        PredictionCompleted?.Invoke(result);
        SetStatus($"{result.Frames.Count} frames predicted " +
                  $"({AudioDuration:F2}s audio, {result.Labels.Count} viseme classes).");

        return result;
    }
    
    public void RebuildPoseKeyframes()
    {
        if (_lastPrediction == null) return;

        _timeline = VisemePoseTimeline.Build(_lastPrediction, ProbabilityFloor, PoseGain);
        _driver.Configure(_timeline.ShapeKeys);
    }
    

    public void StartAnimation()
    {
        StopLive();
        StateController.ChangeMouthMovementOption(MouthMovementOption.Blendshapes);
        string? reason = GetAnimationBlockReason();
        if (reason != null) throw new InvalidOperationException(reason);
        StartTransport(ResolveStartPosition(), PlaybackMode.Animation);
        SetStatus("Animating...");
    }

    public void PlayAudioOnly()
    {
        if (!HasAudio)
            throw new InvalidOperationException("Load a file or record audio first.");
        if (IsRecording)
            throw new InvalidOperationException("Stop recording first.");
        StopLive();
        StartTransport(ResolveStartPosition(), PlaybackMode.AudioOnly);
        SetStatus("Playing audio...");
    }

    public void StopAnimation()
    {
        if (!IsAnimating) return;
        StopPlayback(resetMouth: true);
        SetStatus("Animation stopped.");
    }

    public void StopAudioOnly()
    {
        if (_mode != PlaybackMode.AudioOnly) return;
        StopPlayback(resetMouth: false);
        SetStatus("Playback stopped.");
    }

    private double ResolveStartPosition()
        => _playheadSeconds >= AudioDuration - 0.01 ? 0 : _playheadSeconds;

    private PlaybackMode _lastStartedMode = PlaybackMode.None;

    private void StartTransport(double seconds, PlaybackMode mode)
    {
        var previous = _mode;
        bool modeSwitch = _lastStartedMode != PlaybackMode.None && _lastStartedMode != mode;
        if (modeSwitch) seconds = 0;

        TearDownTransport();
        _timer.Stop();

        _seekBaseSeconds = Math.Clamp(seconds, 0, AudioDuration);
        _playheadSeconds = _seekBaseSeconds;
        _lastSmoothedTime = _seekBaseSeconds + AudioLatencyCompensationSeconds;

        OpenWaveOut(_seekBaseSeconds);

        _mode = mode;
        _lastStartedMode = mode;

        _timer.Interval = TimeSpan.FromMilliseconds(AnimationTickMilliseconds);
        _timer.Start();

        PositionChanged?.Invoke(_playheadSeconds);

        if (previous == PlaybackMode.Animation && mode != PlaybackMode.Animation)
        {
            ResetMouthToSilence();
            AnimationStopped?.Invoke();
        }

        if (previous == PlaybackMode.AudioOnly && mode != PlaybackMode.AudioOnly)
            AudioPlaybackStopped?.Invoke();

        if (mode == PlaybackMode.Animation) AnimationStarted?.Invoke();
        else if (mode == PlaybackMode.AudioOnly) AudioPlaybackStarted?.Invoke();
    }

    private void FinishPlayback(string message)
    {
        bool wasAnimating = IsAnimating;
        _playheadSeconds = AudioDuration;
        StopPlayback(resetMouth: wasAnimating);
        PositionChanged?.Invoke(_playheadSeconds);
        SetStatus(message);
    }

    private void StopPlayback(bool resetMouth)
    {
        bool wasAnimating = IsAnimating;
        bool wasAudioOnly = _mode == PlaybackMode.AudioOnly;

        _timer.Stop();
        TearDownTransport();
        _mode = PlaybackMode.None;

        if (resetMouth) ResetMouthToSilence();

        if (wasAnimating) AnimationStopped?.Invoke();
        if (wasAudioOnly) AudioPlaybackStopped?.Invoke();
    }

    private void OpenWaveOut(double startSeconds)
    {
        var pcm = AudioHelper.FloatsToPcm16Bytes(_audio!);
        _playStream = new MemoryStream(pcm);
        _rawStream = new RawSourceWaveStream(
            _playStream, new WaveFormat(VisemeOnnxPredictor.SampleRate, 16, 1));
        
        long bytePos = (long)(startSeconds * VisemeOnnxPredictor.SampleRate) * 2;
        _rawStream.Position = Math.Clamp(bytePos, 0, _rawStream.Length);

        _waveOut = new WaveOut();
        _waveOut.PlaybackStopped += OnPlaybackStopped;
        _waveOut.Init(_rawStream);
        _waveOut.Play();
    }

    private void TearDownTransport()
    {
        var wave = _waveOut;
        _waveOut = null;

        if (wave != null)
        {
            wave.PlaybackStopped -= OnPlaybackStopped;
            try
            {
                wave.Stop();
            }
            catch
            {
            }

            wave.Dispose();
        }

        _rawStream?.Dispose();
        _rawStream = null;
        _playStream?.Dispose();
        _playStream = null;
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (_dispatcher.CheckAccess()) HandlePlaybackStopped();
        else _dispatcher.BeginInvoke(HandlePlaybackStopped);
    }

    private void HandlePlaybackStopped()
    {
        if (_isSeeking || _mode == PlaybackMode.None) return;
        FinishPlayback(IsAnimating ? "Animation finished." : "Playback finished.");
    }

    private double ReadAudioClock()
    {
        if (_waveOut == null) return _playheadSeconds;
        return _seekBaseSeconds + _waveOut.GetPosition() / (2.0 * VisemeOnnxPredictor.SampleRate);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_mode == PlaybackMode.None || _waveOut == null || _isSeeking) return;

        double time = ReadAudioClock() + AudioLatencyCompensationSeconds;

        if (time >= AudioDuration)
        {
            FinishPlayback(IsAnimating ? "Animation finished." : "Playback finished.");
            return;
        }

        _playheadSeconds = time;
        PositionChanged?.Invoke(time);

        if (_mode == PlaybackMode.Animation)
            ApplyPoseAt(time, smooth: true);
    }
    

    public void BeginSeek()
    {
        if (_isSeeking || !HasAudio) return;

        _modeBeforeSeek = _mode;
        _isSeeking = true;

        _timer.Stop();
        TearDownTransport();
        _mode = PlaybackMode.None;
    }

    public void SeekTo(double seconds)
    {
        if (!HasAudio) return;

        _playheadSeconds = Math.Clamp(seconds, 0, AudioDuration);
        ApplyPoseAt(_playheadSeconds, smooth: false);
        PositionChanged?.Invoke(_playheadSeconds);

        if (!_isSeeking && _mode != PlaybackMode.None)
            StartTransport(_playheadSeconds, _mode);
    }

    public void EndSeek()
    {
        if (!_isSeeking) return;

        _isSeeking = false;
        var resume = _modeBeforeSeek;
        _modeBeforeSeek = PlaybackMode.None;

        if (resume != PlaybackMode.None)
            StartTransport(_playheadSeconds, resume);
    }
    

    private void ApplyPoseAt(double time, bool smooth)
    {
        if (_timeline == null) return;

        var target = _timeline.Sample(Math.Max(0, time));

        double dt = _lastSmoothedTime <= 0
            ? AnimationTickMilliseconds / 1000.0
            : time - _lastSmoothedTime;
        _lastSmoothedTime = time;

        _driver.Apply(target, dt, smooth);

        int idx = _timeline.IndexAt(time);
        if (idx != _lastFrameIndex)
        {
            _lastFrameIndex = idx;
            FrameIndexChanged?.Invoke(idx);
        }
    }


    private void ResetMouthToSilence()
    {
        _driver.Reset(_timeline?.SilencePose);
        _lastFrameIndex = -1;
        FrameIndexChanged?.Invoke(-1);
    }


    public void StartLive()
    {
        if (!IsMouthReady)
            throw new InvalidOperationException("Mouth view is not loaded yet.");
        if (StateController.MouthMovementOption != MouthMovementOption.Blendshapes)
            throw new InvalidOperationException("Set mouth movement to 'Blendshapes' first.");

        StopPlayback(resetMouth: true);
        if (IsRecording) StopRecording();

        _live.ProbabilityFloor = ProbabilityFloor;
        _live.PoseGain = PoseGain;
        _live.Start();
    }

    public void StopLive() => _live.Stop();
    

    public string? GetAnimationBlockReason()
    {
        if (!HasAudio) return "Load a file or record audio first.";
        if (!HasPrediction) return "Run 'Predict Visemes' first.";
        if (IsRecording) return "Stop recording first.";
        if (!IsMouthReady) return "Mouth view is not loaded yet.";
        

        return null;
    }

    public bool CanAnimate => GetAnimationBlockReason() == null;

    private void SetStatus(string message) => StatusChanged?.Invoke(message);
}