namespace SpeakerAnimator.Model;

public class StateController
{
    public static bool EditablePoints { get; private set; } = true;
    public static MouthMovementOption MouthMovementOption { get; private set; } = MouthMovementOption.Rig;
    public static event Action? EditablePointsToggled;
    public static event Action? MouthMovementOptionChanged;
    
    public static void ToggleEditablePoints()
    {
        EditablePoints = !EditablePoints;
        EditablePointsToggled?.Invoke();
    }
    
    public static void ChangeMouthMovementOption(MouthMovementOption option)
    {
        MouthMovementOption = option;
        MouthMovementOptionChanged?.Invoke();
    }
    
}

public enum MouthMovementOption
{
    Rig,
    Blendshapes
}