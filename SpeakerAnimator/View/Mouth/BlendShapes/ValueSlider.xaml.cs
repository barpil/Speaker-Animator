using System.Windows;
using System.Windows.Controls;

namespace SpeakerAnimator.View.Mouth.BlendShapes;

public partial class ValueSlider : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(ValueSlider),
            new PropertyMetadata("Title"));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public event Action? ValueChanged;

    public ValueSlider()
    {
        InitializeComponent();
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        ValueChanged?.Invoke();
    }
}