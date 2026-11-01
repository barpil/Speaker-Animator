using System.Windows;
using System.Windows.Controls;
using SpeakerAnimator.Model;

namespace SpeakerAnimator.View.Panels;

public partial class DebugPanel : UserControl
{
    public DebugPanel()
    {
        InitializeComponent();

#if DEBUG
        this.Visibility = Visibility.Visible;
        UpdateEditablePointsLabel();
#else
        this.Visibility = Visibility.Collapsed;
#endif

        
    }

    private void PrintKeyPointListBtn_Click(object sender, RoutedEventArgs routedEventArgs)
    {
        if (AnimatedMouthController.Instance == null) return;
        Console.WriteLine(string.Join(", ", AnimatedMouthController.Instance.GetMouthKeyPoints()));
    }

    private void ToggleEditablePointsBtn_Click(object sender, RoutedEventArgs e)
    {
        StateController.ToggleEditableRigPoints();
        UpdateEditablePointsLabel();
    }

    private void UpdateEditablePointsLabel()
    {
        EditablePointsStatusLabel.Content = StateController.EditableRigPoints ? "ON" : "OFF";
    }

    private void ResetToNeutralsBtn_Click(object sender, RoutedEventArgs e)
    {
        MouthCanvas.Instance?.CreateNewAnimatedMouthController();
    }
}