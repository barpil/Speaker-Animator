using System.Windows;
using System.Windows.Documents;
using SpeakerAnimator.View;

namespace SpeakerAnimator.Model.Mouth.BlendShapes;

public static class PosesPresetsLibrary
{
    private struct DefaultPosePointDefinitions
    {
        public List<Point> LipPoints;
        public List<Point> TeethPoints;
    }

    private static readonly Dictionary<string, DefaultPosePointDefinitions> DEFAULT_POSES = new()
    {
        {
            "Neutral", new()
            {
                LipPoints =
                [
                    new(300, 287), new(398, 282), new(498, 285),
                    new(486, 313), new(390, 319), new(306, 312)
                ],
                TeethPoints =
                [
                    new(283, 272), new(535, 269),
                    new(287, 329), new(519, 329)
                ]
            }
        },
        {
            "Open", new()
            {
                LipPoints =
                [
                    new(296, 260), new(403, 253), new(504, 264),
                    new(498, 344), new(398, 341), new(299, 334)
                ],
                TeethPoints =
                [
                    new(273, 264), new(530, 263),
                    new(260, 320), new(534, 317)
                ]
            }
        },
        {
            "Closed", new()
            {
                LipPoints =
                [
                    new(304, 293), new(396, 289), new(491, 293),
                    new(492, 299), new(398, 297), new(308, 298)
                ],
                TeethPoints =
                [
                    new(275, 266), new(526, 271),
                    new(275, 319), new(509, 322)
                ]
            }
        },
        {
            "Wide", new()
            {
                LipPoints =
                [
                    new(295, 237), new(405, 231), new(511, 241),
                    new(509, 380), new(405, 389), new(297, 380)
                ],
                TeethPoints =
                [
                    new(285, 211), new(525, 211),
                    new(285, 411), new(525, 411)
                ]
            }
        },
        {
            "Pucker", new()
            {
                LipPoints =
                [
                    new(357, 253), new(404, 247), new(456, 259),
                    new(457, 322), new(398, 342), new(350, 312)
                ],
                TeethPoints =
                [
                    new(241, 239), new(543, 244),
                    new(261, 352), new(539, 357)
                ]
            }
        }
    };

    //Potem mozna dodac wiecej presetow, na razie najprostsze rozwiazanie
    public static Dictionary<string, PoseRecord> GetPoseDictFromPreset(List<MovablePointVisual> visuals)
    {
        var dict = DEFAULT_POSES.ToDictionary(d => d.Key, d =>
        {
            Queue<Point> lipQ = new Queue<Point>(d.Value.LipPoints);
            Queue<Point> teethQ = new Queue<Point>(d.Value.TeethPoints);
            List<PointPositionRecord> pointPositions = [];
            foreach (var visual in visuals)
            {
                Point position;
                if (visual is MovableLipPointVisual)
                {
                    position = lipQ.Dequeue();
                }
                else if (visual is MovableTeethPointVisual)
                {
                    position = teethQ.Dequeue();
                }
                else
                {
                    throw new ArgumentException("Unknow visual type approached while loading pose presets");
                }

                pointPositions.Add(new PointPositionRecord() { visual = visual, position = position });
            }

            var record = new PoseRecord() { name = d.Key, pointPositions = pointPositions };
            return record;
        });
        return dict;
    }
}