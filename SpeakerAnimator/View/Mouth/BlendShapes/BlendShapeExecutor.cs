using System.Numerics;
using System.Windows;
using SpeakerAnimator.Model.Mouth.BlendShapes;
using Vector = System.Windows.Vector;

namespace SpeakerAnimator.View.Mouth.BlendShapes;

public class BlendShapeExecutor
{
   
    public static void ExecuteBlendShapePose(ExecutePoseArgs args)
    {
        //Moze zamiast neutral powinienem kierowac sie od current miejsca dla punktu?
        //Z drugiej strony wtedy ciezko bedzie chyba zrobic zeby doszlo do neutral pozycji
        Dictionary<string, double> poseWeightsDict = args.PoseWeights.Where(kvp => kvp.Value >= 0.01).ToDictionary();
        Dictionary<MovablePointVisual, Point> neutralPointsDict = BlendShapesPoseStorage.Instance.GetPointPositionsForPose("Neutral")
            .ToDictionary(pr => pr.visual, pr => pr.position);
        Dictionary<MovablePointVisual, Point> destinationPointsDict = BlendShapesPoseStorage.Instance.GetPointPositionsForPose("Neutral")
            .ToDictionary(pr => pr.visual, pr => pr.position);
        foreach (var poseWeight in poseWeightsDict)
        {
            string poseName = poseWeight.Key;
            var positionRecords = BlendShapesPoseStorage.Instance.GetPointPositionsForPose(poseName);
            foreach (var posRec in positionRecords)
            {
                if (!neutralPointsDict.TryGetValue(posRec.visual, out Point neutralPos))
                    continue;
                Vector vec = posRec.position - neutralPos;
                destinationPointsDict[posRec.visual] += vec*poseWeight.Value;
            }
        }

        foreach (var desKVP in destinationPointsDict)
        {
            desKVP.Key.MoveTo(desKVP.Value);
        }
    }
    
}

public struct ExecutePoseArgs
{ 
    public Dictionary<string, double> PoseWeights;
}
