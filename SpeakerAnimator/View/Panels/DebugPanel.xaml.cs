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
        MouthOptionCB.SelectedIndex = 0;
        UpdateMouthMovementOptionLabel();
#else
        this.Visibility = Visibility.Collapsed;
#endif

        
    }

    private void PrintKeyPointListBtn_Click(object sender, RoutedEventArgs routedEventArgs)
    {
        if (AnimatedMouthController.Instance == null) return;
        Console.WriteLine($"Mouth: {string.Join(", ", AnimatedMouthController.Instance.GetMouthKeyPoints())}");
        Console.WriteLine($"Teeth: {string.Join(", ", AnimatedMouthController.Instance.GetTeethKeyPoints())}");
    }

    private void ToggleEditablePointsBtn_Click(object sender, RoutedEventArgs e)
    {
        StateController.ToggleEditablePoints();
        UpdateEditablePointsLabel();
    }

    private void UpdateEditablePointsLabel()
    {
        EditablePointsStatusLabel.Content = StateController.EditablePoints ? "ON" : "OFF";
    }

    private void UpdateMouthMovementOptionLabel()
    {
        MouthMovementOptionLabel.Content = StateController.MouthMovementOption.ToString();
    }

    private void ResetToNeutralsBtn_Click(object sender, RoutedEventArgs e)
    {
        MouthCanvas.Instance?.CreateNewAnimatedMouthController();
    }

    private void OnMouthMovementOption_SelectionChange(object sender, SelectionChangedEventArgs e)
    {
        string state = ((ComboBoxItem) e.AddedItems[0]).Content.ToString();
        StateController.ChangeMouthMovementOption(Enum.Parse<MouthMovementOption>(state ?? "Rig"));
        UpdateMouthMovementOptionLabel();
        MouthCanvas.Instance?.CreateNewAnimatedMouthController();
        if (StateController.MouthMovementOption == MouthMovementOption.Rig)
        {
            RigPanel.Visibility = Visibility.Visible;
            BlendShapesPanel.Visibility = Visibility.Collapsed;
        }
        else if (StateController.MouthMovementOption == MouthMovementOption.Blendshapes)
        {
            RigPanel.Visibility = Visibility.Collapsed;
            BlendShapesPanel.Visibility = Visibility.Visible;
        }
    }
}