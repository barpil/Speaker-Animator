using System;
using System.Windows;
using System.Windows.Controls;
using SpeakerAnimator.Model;

namespace SpeakerAnimator.View.Panels;

public partial class SidePanel : UserControl
{
    public SidePanel()
    {
        InitializeComponent();
        MouthOptionCB.SelectedIndex = 1;
        UpdateMouthMovementOptionLabel();
        UpdateMouthPanelsVisibility();
        UpdateEditablePointsLabel();

#if DEBUG
        DebugToolsPanel.Visibility = Visibility.Visible;
#else
        DebugToolsPanel.Visibility = Visibility.Collapsed;
#endif
    }

    private void OnMouthMovementOption_SelectionChange(object sender, SelectionChangedEventArgs e)
    {
        if (RigPanel == null || BlendShapesPanel == null) return;
        if (MouthOptionCB.SelectedItem is not ComboBoxItem item) return;

        string state = item.Content?.ToString() ?? "Rig";

        StateController.ChangeMouthMovementOption(
            Enum.Parse<MouthMovementOption>(state));

        UpdateMouthMovementOptionLabel();
        UpdateMouthPanelsVisibility();

        MouthCanvas.Instance?.CreateNewAnimatedMouthController();
    }

    private void UpdateMouthPanelsVisibility()
    {
        bool isRig = StateController.MouthMovementOption == MouthMovementOption.Rig;

        RigPanel.Visibility = isRig ? Visibility.Visible : Visibility.Collapsed;
        BlendShapesPanel.Visibility = isRig ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateMouthMovementOptionLabel()
    {
        MouthMovementOptionLabel.Content = StateController.MouthMovementOption.ToString();
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

    private void ResetToNeutralsBtn_Click(object sender, RoutedEventArgs e)
    {
        MouthCanvas.Instance?.CreateNewAnimatedMouthController();
    }


    private void PrintKeyPointListBtn_Click(object sender, RoutedEventArgs e)
    {
        if (AnimatedMouthController.Instance == null) return;
        var controller = AnimatedMouthController.Instance;

        string mouthString = string.Join(", ",
            controller.GetMouthKeyPoints()
                .Select(p => $"new({p.X}, {p.Y})"));

        string teethString = string.Join(", ",
            controller.GetTeethKeyPoints()
                .Select(p => $"new({p.X}, {p.Y})"));

        Console.WriteLine($"Mouth: {mouthString}");
        Console.WriteLine($"Teeth: {teethString}");
    }
}