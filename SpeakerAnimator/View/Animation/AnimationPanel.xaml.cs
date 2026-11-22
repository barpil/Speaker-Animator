using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using SpeakerAnimator.Model.NN;

namespace SpeakerAnimator.View.Panels;

public partial class AnimationPanel : UserControl
{
    private sealed record ResultRow
    {
        public double Time { get; init; }
        public string Viseme { get; init; } = "";
        public double Confidence { get; init; }
    }

    private AnimationController? Controller => AnimationController.Instance;

    private bool _updatingSlider;
    private bool _updatingSelection;
    private bool _subscribed;
    private DateTime _lastTableUpdate = DateTime.MinValue;
    private static readonly TimeSpan TableThrottle = TimeSpan.FromMilliseconds(100);

    public AnimationPanel() => InitializeComponent();

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        SubscribeController();
        ResyncUi();
    }

    private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        => UnsubscribeController();

    private void SubscribeController()
    {
        var c = Controller;
        if (c == null || _subscribed) return;

        c.StatusChanged += OnStatusChanged;
        c.AudioLoaded += OnAudioLoaded;
        c.RecordingStarted += OnRecordingStarted;
        c.RecordingStopped += OnRecordingStopped;
        c.PredictionCompleted += OnPredictionCompleted;
        c.AnimationStarted += OnAnimationStarted;
        c.AnimationStopped += OnAnimationStopped;
        c.PositionChanged += OnPositionChanged;
        c.FrameIndexChanged += OnFrameIndexChanged;
        c.LiveStarted += OnLiveStarted;
        c.LiveStopped += OnLiveStopped;
        c.LiveVisemeDetected += OnLiveVisemeDetected;
        c.AudioPlaybackStarted += OnAudioPlaybackStarted;
        c.AudioPlaybackStopped += OnAudioPlaybackStopped;

        _subscribed = true;
    }

    private void UnsubscribeController()
    {
        var c = Controller;
        if (c == null || !_subscribed) return;

        c.StatusChanged -= OnStatusChanged;
        c.AudioLoaded -= OnAudioLoaded;
        c.RecordingStarted -= OnRecordingStarted;
        c.RecordingStopped -= OnRecordingStopped;
        c.PredictionCompleted -= OnPredictionCompleted;
        c.AnimationStarted -= OnAnimationStarted;
        c.AnimationStopped -= OnAnimationStopped;
        c.PositionChanged -= OnPositionChanged;
        c.FrameIndexChanged -= OnFrameIndexChanged;
        c.LiveStarted -= OnLiveStarted;
        c.LiveStopped -= OnLiveStopped;
        c.LiveVisemeDetected -= OnLiveVisemeDetected;
        c.AudioPlaybackStarted -= OnAudioPlaybackStarted;
        c.AudioPlaybackStopped -= OnAudioPlaybackStopped;

        _subscribed = false;
    }

    private void ResyncUi()
    {
        var c = Controller;
        if (c == null) return;

        AnimateBtn.Content = c.IsAnimating ? "Stop Animation" : "Animate";
        RecordBtn.Content = c.IsRecording ? "Stop" : "Record";
        LiveBtn.Content = c.IsLive ? "Stop Live" : "Start Live";
        LiveStatusLabel.Content = c.IsLive ? "Running" : "Off";
        TestAudioBtn.Content = c.IsPlayingAudioOnly ? "Stop Audio" : "Test Audio";
    }
    

    private void OnStatusChanged(string message) =>
        Dispatcher.Invoke(() => StatusLabel.Text = message);

    private void OnAudioLoaded(string source, double seconds) => Dispatcher.Invoke(() =>
    {
        AudioFileLabel.Content = source;
        ResultsList.ItemsSource = null;

        _updatingSlider = true;
        PositionSlider.Maximum = seconds;
        PositionSlider.Value = 0;
        PositionSlider.IsEnabled = seconds > 0;
        TimeLabel.Content = $"0.00s / {seconds:F2}s";
        _updatingSlider = false;
    });

    private void OnRecordingStarted() => Dispatcher.Invoke(() =>
    {
        RecordBtn.Content = "Stop";
        RecordStatusLabel.Content = "Recording...";
    });

    private void OnRecordingStopped(double seconds) => Dispatcher.Invoke(() =>
    {
        RecordBtn.Content = "Record";
        RecordStatusLabel.Content = seconds > 0 ? $"Recorded {seconds:F1}s" : "Idle";
        ResultsList.ItemsSource = null;
    });

    private void OnPredictionCompleted(VisemePredictionResult result) => Dispatcher.Invoke(() =>
    {
        ResultsList.ItemsSource = result.Frames
            .Select(f => new ResultRow
            {
                Time = f.TimeSeconds,
                Viseme = f.Viseme,
                Confidence = f.Confidence
            })
            .ToList();
    });

    private void OnAnimationStarted() => Dispatcher.Invoke(() => AnimateBtn.Content = "Stop Animation");
    private void OnAnimationStopped() => Dispatcher.Invoke(() => AnimateBtn.Content = "Animate");

    private void OnPositionChanged(double seconds) => Dispatcher.Invoke(() =>
    {
        _updatingSlider = true;
        PositionSlider.Value = seconds;
        TimeLabel.Content = $"{seconds:F2}s / {Controller?.AudioDuration ?? 0:F2}s";
        _updatingSlider = false;
    });
    

    private void OnFrameIndexChanged(int index) => Dispatcher.Invoke(() =>
    {
        if (index < 0 || ResultsList.Items.Count == 0) return;
        if (FollowCheck.IsChecked != true) return;
        
        var now = DateTime.UtcNow;
        if (now - _lastTableUpdate < TableThrottle) return;
        _lastTableUpdate = now;

        index = Math.Min(index, ResultsList.Items.Count - 1);
        if (ResultsList.SelectedIndex == index) return;

        _updatingSelection = true;
        ResultsList.SelectedIndex = index;
        _updatingSelection = false;

        ScrollIndexToTop(index);
    });

    private void ScrollIndexToTop(int index)
    {
        var item = ResultsList.Items[index];
        ResultsList.ScrollIntoView(item);

        var viewer = FindDescendant<ScrollViewer>(ResultsList);
        if (viewer == null) return;

        double rowHeight =
            (ResultsList.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement)?.ActualHeight
            ?? 22.0;

        viewer.ScrollToVerticalOffset(index * rowHeight);
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) return match;
            var deeper = FindDescendant<T>(child);
            if (deeper != null) return deeper;
        }
        return null;
    }
    

    private void LiveBtn_Click(object sender, RoutedEventArgs e)
    {
        if (Controller == null) return;

        try
        {
            if (Controller.IsLive) Controller.StopLive();
            else Controller.StartLive();
        }
        catch (Exception ex) { ShowError("Live mode failed", ex); }
    }
    
    private ResultRow? _liveRow;
    private List<ResultRow>? _liveList;
    private void OnLiveStarted() => Dispatcher.Invoke(() =>
    {
        LiveBtn.Content = "Stop Live";
        LiveStatusLabel.Content = "Running";

        _updatingSlider = true;
        PositionSlider.IsEnabled = false;
        PositionSlider.Value = 0;
        _updatingSlider = false;
        
        _liveRow = new ResultRow();
        _liveList = new List<ResultRow> { _liveRow };
        ResultsList.ItemsSource = _liveList;
        _updatingSelection = true;
        ResultsList.SelectedIndex = 0;
        _updatingSelection = false;
    });

    private void OnLiveStopped() => Dispatcher.Invoke(() =>
    {
        LiveBtn.Content = "Start Live";
        LiveStatusLabel.Content = "Off";
        ResultsList.ItemsSource = null;
        _liveRow = null;
        _liveList = null;
        PositionSlider.IsEnabled = Controller?.HasAudio == true;
    });

    private void OnLiveVisemeDetected(VisemeFramePrediction frame) => Dispatcher.Invoke(() =>
    {
        if (_liveRow == null) return;
        
        _liveRow = _liveRow with
        {
            Time = frame.TimeSeconds,
            Viseme = frame.Viseme,
            Confidence = frame.Confidence
        };
        
        _liveList![0] = _liveRow;
        ResultsList.Items.Refresh();
    });
    
    private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingSelection) return;

        var c = Controller;
        if (c == null || c.IsLive || !c.HasAudio) return;
        if (ResultsList.SelectedItem is not ResultRow row) return;

        try { c.SeekTo(row.Time); }
        catch (Exception ex) { ShowError("Seek failed", ex); }
    }
    

    private void LoadAudioBtn_Click(object sender, RoutedEventArgs e)
    {
        if (Controller == null) return;

        var dialog = new OpenFileDialog
        {
            Title = "Choose audio file",
            Filter = "Audio files|*.wav;*.mp3;*.mp4;*.wma;*.aiff|All files|*.*"
        };
        if (dialog.ShowDialog() != true) return;

        try { Controller.LoadAudioFromFile(dialog.FileName); }
        catch (Exception ex)
        {
            ShowError("Could not load audio", ex);
            StatusLabel.Text = "Load failed.";
        }
    }

    private void RecordBtn_Click(object sender, RoutedEventArgs e)
    {
        if (Controller == null) return;
        try
        {
            if (!Controller.IsRecording) Controller.StartRecording();
            else Controller.StopRecording();
        }
        catch (Exception ex) { ShowError("Recording failed", ex); }
    }

    private void TestAudioBtn_Click(object sender, RoutedEventArgs e)
    {
        var c = Controller;
        if (c == null) return;

        try
        {
            if (c.IsPlayingAudioOnly) c.StopAudioOnly();
            else
            {
                c.PlayAudioOnly();
            }
        }
        catch (Exception ex) { ShowError("Playback failed", ex); }
    }

    private void OnAudioPlaybackStarted() =>
        Dispatcher.Invoke(() => TestAudioBtn.Content = "Stop Audio");

    private void OnAudioPlaybackStopped() =>
        Dispatcher.Invoke(() => TestAudioBtn.Content = "Test Audio");

    private async void PredictBtn_Click(object sender, RoutedEventArgs e)
    {
        if (Controller == null) return;

        PredictBtn.IsEnabled = false;
        try { await Controller.PredictAsync(); }
        catch (Exception ex)
        {
            ShowError("Inference failed", ex);
            StatusLabel.Text = "Prediction failed.";
        }
        finally { PredictBtn.IsEnabled = true; }
    }

    private void AnimateBtn_Click(object sender, RoutedEventArgs e)
    {
        if (Controller == null) return;
        try
        {
            if (Controller.IsAnimating) Controller.StopAnimation();
            else Controller.StartAnimation();
        }
        catch (Exception ex) { ShowError("Animation failed", ex); }
    }

    private void PositionSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        => Controller?.BeginSeek();

    private void PositionSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        => Controller?.EndSeek();

    private void PositionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingSlider) return;
        Controller?.SeekTo(e.NewValue);
    }

    private static void ShowError(string title, Exception ex) =>
        MessageBox.Show($"{title}:\n{ex.Message}", "Error",
            MessageBoxButton.OK, MessageBoxImage.Error);
}