using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SpeakerAnimator.View;

public class MouthCanvas : Canvas
{
    private List<MovableMouthPointVisual> _mouthPointVisuals = new List<MovableMouthPointVisual>();

    private AnimatedMouthController _animatedMouthController;

    private readonly List<Point> DEFAULT_MOUTH_MOUTHPOINT_POSITIONS = [
        new Point(100, 100), new Point(200, 100), new Point(300, 100),
        new Point(200, 300)
    ];
    
    
    public MouthCanvas()
    {
        Background = Brushes.Transparent;

        _animatedMouthController = new AnimatedMouthController(this, DEFAULT_MOUTH_MOUTHPOINT_POSITIONS);

    }

    
    
}