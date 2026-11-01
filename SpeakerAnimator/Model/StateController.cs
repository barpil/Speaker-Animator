namespace SpeakerAnimator.Model;

public class StateController
{
    public static bool EditableRigPoints { get; private set; } = true;
    public static event Action? EditableRigPointsToggled;
    public static void ToggleEditableRigPoints()
    {
        EditableRigPoints = !EditableRigPoints;
        EditableRigPointsToggled?.Invoke();
    }
    
}