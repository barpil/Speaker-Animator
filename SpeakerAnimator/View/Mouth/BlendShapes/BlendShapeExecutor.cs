using System.Windows;
using SpeakerAnimator.Model.Mouth.BlendShapes;

namespace SpeakerAnimator.View.Mouth.BlendShapes;

public class BlendShapeExecutor
{
    public static List<(MovablePointVisual point, Point position)> ComputeBlendShapeMoves(ExecutePoseArgs args)
    {
        var moves = new List<(MovablePointVisual, Point)>();
        
        var poseWeightsDict = args.PoseWeights
            .Where(kvp => kvp.Value >= 0.01)
            .ToDictionary();
            
        var neutralPointsDict = BlendShapesPoseStorage.Instance
            .GetPointPositionsForPose("Neutral")
            .ToDictionary(pr => pr.visual, pr => pr.position);
            
        var destinationPointsDict = new Dictionary<MovablePointVisual, Point>(neutralPointsDict);
        
        foreach (var poseWeight in poseWeightsDict)
        {
            string poseName = poseWeight.Key;
            var positionRecords = BlendShapesPoseStorage.Instance.GetPointPositionsForPose(poseName);
            
            foreach (var posRec in positionRecords)
            {
                if (!neutralPointsDict.TryGetValue(posRec.visual, out Point neutralPos))
                    continue;
                    
                var vec = posRec.position - neutralPos;
                destinationPointsDict[posRec.visual] += vec * poseWeight.Value;
            }
        }

        foreach (var kvp in destinationPointsDict)
        {
            moves.Add((kvp.Key, kvp.Value));
        }

        return moves;
    }

    public static void ApplyMoves(List<(MovablePointVisual point, Point position)> moves)
    {
        foreach (var (point, position) in moves)
        {
            point.MoveTo(position);
        }
    }

    public static void ExecuteBlendShapePose(ExecutePoseArgs args)
    {
        var moves = ComputeBlendShapeMoves(args);
        ApplyMoves(moves);
    }
}

public struct ExecutePoseArgs
{ 
    public Dictionary<string, double> PoseWeights;
}