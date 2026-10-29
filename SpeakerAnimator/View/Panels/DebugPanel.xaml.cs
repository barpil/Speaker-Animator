using System.Windows;
using System.Windows.Controls;

namespace SpeakerAnimator.View.Panels;

public partial class DebugPanel : UserControl
{
    public DebugPanel()
    {
        InitializeComponent();

#if DEBUG
        this.Visibility = Visibility.Visible;
#else
        this.Visibility = Visibility.Collapsed;
#endif

        
    }

    private void PrintKeyPointListBtn_Click(object sender, RoutedEventArgs routedEventArgs)
    {
        if (AnimatedMouthController.Instance == null) return;
        Console.WriteLine(string.Join(", ", AnimatedMouthController.Instance.GetMouthKeyPoints()));
    }
}