using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SpeakerAnimator.View;

public class MouthCanvas : Canvas
{
    private List<MovableLipPointVisual> _mouthPointVisuals = new List<MovableLipPointVisual>();

    private AnimatedMouthController _animatedMouthController;

    private readonly List<Point> DEFAULT_MOUTH_LIPPOINT_POSITIONS = [
        new Point(100, 100), new Point(200, 100), new Point(300, 100),
        new Point(300, 300), new Point(200,300), new Point(100, 300)
    ];
    private readonly List<Point> DEFAULT_MOUTH_TEETHPOINT_POSITIONS = [
        new Point(80, 100), new Point(320, 100),
        new Point(80, 300) , new Point(320, 300)
    ];
    
    
    
    public MouthCanvas()
    {
        Background = Brushes.Transparent;

        _animatedMouthController = new AnimatedMouthController(this, DEFAULT_MOUTH_LIPPOINT_POSITIONS, DEFAULT_MOUTH_TEETHPOINT_POSITIONS);

    }

    
    
}