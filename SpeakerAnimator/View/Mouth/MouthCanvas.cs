using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SpeakerAnimator.Model.Mouth.BlendShapes;

namespace SpeakerAnimator.View;

public class MouthCanvas : Canvas
{
    public static MouthCanvas? Instance;
    
    
    private List<MovableLipPointVisual> _mouthPointVisuals = new List<MovableLipPointVisual>();

    private AnimatedMouthController _animatedMouthController;

    private readonly List<Point> DEFAULT_MOUTH_LIPPOINT_POSITIONS =
    [
        new(305, 211), new(405, 211), new(505, 211),
        new(505, 411), new(405, 411), new(305, 411)
    ];

    private readonly List<Point> DEFAULT_MOUTH_TEETHPOINT_POSITIONS =
    [
        new(285, 211), new(525, 211),
        new(285, 411), new(525, 411)
    ];

    private readonly List<(string, Point)> DEFAULT_RIGPOINT_POSITIONS =
    [
        ("top_lip", new Point(405, 181)),
        ("left_side", new Point(275, 311)),
        ("right_side", new Point(535, 311)),
        ("bottom_lip", new Point(405, 441)),
        ("jaw", new Point(405, 471))
    ];
    
    
    
    public MouthCanvas()
    {
        Background = Brushes.Transparent;

        _animatedMouthController = new AnimatedMouthController(this, DEFAULT_MOUTH_LIPPOINT_POSITIONS,
            DEFAULT_MOUTH_TEETHPOINT_POSITIONS, DEFAULT_RIGPOINT_POSITIONS);
        Instance = this;
    }


    public void CreateNewAnimatedMouthController()
    {
        _animatedMouthController.ResetToDefaultPositions();
    }

    
    
}