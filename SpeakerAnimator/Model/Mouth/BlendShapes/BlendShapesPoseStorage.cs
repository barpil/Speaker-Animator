using System.Windows;
using SpeakerAnimator.View;

namespace SpeakerAnimator.Model.Mouth.BlendShapes;

public class BlendShapesPoseStorage
{
    public static BlendShapesPoseStorage? Instance;
    public static bool IsInitialized => Instance != null;
    
    //Neutral - naturalne usta w spoczynku
    //Open - Otarte usta bez ruszania szczeka
    //Closed - Zamkniete usta bez ruszania szczeka
    //Wide - Poruszenie szczeka w dol
    //Pucker - Dzióbek

    
    
  
    private Dictionary<string, PoseRecord> _posesDict;
    private Dictionary<string, PoseRecord> _defaultPosesDict;
    
    private BlendShapesPoseStorage(List<MovablePointVisual> handledVisuals)
    {
        _defaultPosesDict = PosesPresetsLibrary.GetPoseDictFromPreset(handledVisuals);
        _posesDict = new Dictionary<string, PoseRecord>(_defaultPosesDict);
        
        Instance = this;
    }

    public void SavePose(string poseName, List<MovablePointVisual> visuals)
    {
        PoseRecord poseRecord = new PoseRecord
        {
            name = poseName,
            pointPositions = visuals.Select(v => new PointPositionRecord { visual = v, position = v.CurrentPosition }).ToList()
        };

        _posesDict[poseName] = poseRecord;
    }
    
    
    public List<string> GetSavedPosesNameList()
    {
        return _posesDict.Keys.ToList();
    }

    public List<PointPositionRecord> GetPointPositionsForPose(string poseName)
    {
        return _posesDict.GetValueOrDefault(poseName, _defaultPosesDict["Neutral"]).pointPositions;
    }
    
    public static void Initialize(List<MovablePointVisual> handledVisuals)
    {
        if (Instance == null)
            Instance = new BlendShapesPoseStorage(handledVisuals);
    }

    public static void Reset()
    {
        Instance?._posesDict = new Dictionary<string, PoseRecord>(Instance._defaultPosesDict);
    }

}

public struct PoseRecord
{
    public string name;
    public List<PointPositionRecord> pointPositions;
}

public struct PointPositionRecord
{
    public MovablePointVisual visual;
    public Point position;
}