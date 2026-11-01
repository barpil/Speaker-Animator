using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SpeakerAnimator.View;

public class MouthCanvas : Canvas
{
    public static MouthCanvas? Instance;
    
    
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

    private readonly List<(string, Point)> DEFAULT_RIGPOINT_POSITIONS =
    [
        ("top_lip", new Point(200, 70)),
        ("left_side", new Point(70, 200)), ("right_side", new Point(330, 200)),
        ("bottom_lip", new Point(200, 330)),
        ("jaw", new Point(200, 360))
    ];
    
    
    
    public MouthCanvas()
    {
        Background = Brushes.Transparent;

        CreateNewAnimatedMouthController();
        Instance = this;
    }


    public void CreateNewAnimatedMouthController()
    {
        Children.Clear();
        _animatedMouthController = new AnimatedMouthController(this, DEFAULT_MOUTH_LIPPOINT_POSITIONS,
            DEFAULT_MOUTH_TEETHPOINT_POSITIONS, DEFAULT_RIGPOINT_POSITIONS);
    }

    
    
}