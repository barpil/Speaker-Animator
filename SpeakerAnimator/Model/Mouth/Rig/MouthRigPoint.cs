using System.Windows;
using SpeakerAnimator.View;
using SpeakerAnimator.View.BlendShapes;

namespace SpeakerAnimator.Model.Mouth.Rig;

public class MouthRigPoint
{
    public string Name { get; }
    public MovableMouthRigPointVisual RigPointVisual { get; }

    public MouthRigPoint(string name, MovableMouthRigPointVisual rigPointVisual)
    {
        Name = name;
        RigPointVisual = rigPointVisual;
    }
}