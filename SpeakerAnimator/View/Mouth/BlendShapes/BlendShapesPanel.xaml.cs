using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SpeakerAnimator.Model;
using SpeakerAnimator.Model.Mouth.BlendShapes;

namespace SpeakerAnimator.View.Mouth.BlendShapes;

public partial class BlendShapesPanel : UserControl
{
    private bool _isInititalized = false;
    
    public BlendShapesPanel()
    {
        InitializeComponent();

        EditStatusLabel.Content = StateController.EditablePoints ? "ON" : "OFF";
        List<ValueSlider> sliders = SliderPanel.Children.OfType<ValueSlider>().ToList();
        
        foreach (var slider in sliders)
        {
            slider.ValueChanged += ExecutePoseWithSliderData;
        }
        
        ResolveVisibility();
        _isInititalized = true;
    }
    
    private void SavePoseBtn_Click(object sender, RoutedEventArgs e)
    {
        if (SavePoseBtn.ContextMenu != null)
        {
            SavePoseBtn.ContextMenu.PlacementTarget = SavePoseBtn;
            SavePoseBtn.ContextMenu.IsOpen = true;
        }
    }
    
    private void SavePoseMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            string? poseName = menuItem.Header.ToString();
            if (poseName == null) return;
            SaveCurrentPose(poseName);
        }
    }

    private void SaveCurrentPose(string poseName)
    {
        List<MovablePointVisual> visuals = [];
        visuals.AddRange(AnimatedMouthController.Instance?.LipPointVisuals ?? []);
        visuals.AddRange(AnimatedMouthController.Instance?.TeethPointVisuals ?? []);

        BlendShapesPoseStorage.Instance?.SavePose(poseName, visuals);
        
        foreach (ComboBoxItem item in PoseCB.Items)
        {
            if (item.Content.ToString() == poseName)
            {
                PoseCB.SelectedItem = item;
                break;
            }
        }
    }

    private void ResetBtn_Click(object sender, RoutedEventArgs e)
    {
        BlendShapesPoseStorage.Reset();
        
        foreach (var slider in SliderPanel.Children.OfType<ValueSlider>())
        {
            if (slider.Slider != null)
                slider.Slider.Value = 0;
        }
        PoseCB.SelectedIndex = 0;
        ExecutePoseWithSliderData();
    }
    
    //Metoda dodatkowa zeby dzialalo rowniez przy wyborze ponownym tej samej opcji
    private void PoseItem_MouseUp(object sender, MouseButtonEventArgs e)
    {
        //item.IsSelected zeby przy wyborze nowej opcji nie wykonywalo sie 2 krotnie
        if (sender is ComboBoxItem item && item.IsSelected)
        {
            PoseCB_SelectionChanged(sender, null);
        }
    }

    private void PoseCB_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInititalized) return;
        if (PoseCB?.SelectedItem == null) return;
        
        string? poseName = null;
        if (PoseCB.SelectedItem is ComboBoxItem comboBoxItem)
        {
            poseName = comboBoxItem.Content?.ToString();
        }
        else if (PoseCB.SelectedItem is string str)
        {
            poseName = str;
        }
        
        if (string.IsNullOrWhiteSpace(poseName)) return;
        
        if (SliderPanel != null)
        {
            foreach (var slider in SliderPanel.Children.OfType<ValueSlider>())
            {
                slider.Slider?.Value = 0;
            }
        }
        BlendShapeExecutor.ExecuteBlendShapePose(new ExecutePoseArgs
        {
            PoseWeights = new Dictionary<string, double> { { poseName, 1.0 } }
        });
    }

    private void ToggleEditBtn_Click(object sender, RoutedEventArgs e)
    {
        StateController.ToggleEditablePoints();
        EditStatusLabel.Content = StateController.EditablePoints ? "ON" : "OFF";
        ResolveVisibility();
    }

    private void ResolveVisibility()
    {
        if (StateController.EditablePoints)
        {
            SliderPanel.Visibility = Visibility.Collapsed;
            PosePanel.Visibility = Visibility.Visible;
        }
        else
        {
            SliderPanel.Visibility = Visibility.Visible;
            PosePanel.Visibility = Visibility.Collapsed;
            ExecutePoseWithSliderData();
        }
    }

    private void ExecutePoseWithSliderData()
    {
        List<ValueSlider> sliders = SliderPanel.Children.OfType<ValueSlider>().ToList();
        
        var weights = sliders.GroupBy(s => s.Title)
                             .ToDictionary(g => g.Key, g => g.First().Slider.Value);

        BlendShapeExecutor.ExecuteBlendShapePose(new ExecutePoseArgs
        {
            PoseWeights = weights
        });
    }

    private void PoseCB_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        PoseCB.SelectedIndex = -1;
    }
}