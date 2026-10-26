namespace SpeakerAnimator.Model;


public class MouthPoint
{
    public double X;
    public double Y;
}

public class MouthModel
{
    private List<MouthPoint> _mouthPoints = new List<MouthPoint>();

    public MouthModel(List<MouthPoint> initialMouthPoints)
    {
        _mouthPoints = initialMouthPoints;
    }

    public void ChangeMouthPointPosition(MouthPoint mouthPoint, double newX, double newY)
    {
        mouthPoint.X = newX;
        mouthPoint.Y = newY;
    }
}

