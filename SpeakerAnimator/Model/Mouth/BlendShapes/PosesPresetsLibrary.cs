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
                    new(300, 295), new(405, 293), new(510, 295),
                    new(510, 302), new(405, 303), new(300, 302)
                ],
                TeethPoints =
                [
                    new(280, 272), new(530, 269),
                    new(280, 329), new(530, 329)
                ]
            }
        },
        {
            "Top-Lip-Up", new()
            {
                LipPoints =
                [
                    new(295, 278), new(405, 266), new(515, 278),
                    new(510, 302), new(405, 303), new(300, 302)
                ],
                TeethPoints =
                [
                    new(280, 272), new(530, 269),
                    new(280, 329), new(530, 329)
                ]
            }
        },
        {
            "Bottom-Lip-Down", new()
            {
                LipPoints =
                [
                    new(300, 295), new(405, 293), new(510, 295),
                    new(515, 336), new(405, 341), new(295, 336)
                ],
                TeethPoints =
                [
                    new(280, 272), new(530, 269),
                    new(280, 329), new(530, 329)
                ]
            }
        },
        {
            "Mouth-Open", new()
            {
                LipPoints =
                [
                    new(295, 237), new(405, 231), new(511, 241),
                    new(509, 380), new(405, 389), new(297, 380)
                ],
                TeethPoints =
                [
                    new(280, 211), new(530, 211),
                    new(280, 411), new(530, 411)
                ]
            }
        },
        {
            "Pucker", new()
            {
                LipPoints =
                [
                    new(365, 272), new(405, 260), new(445, 272),
                    new(445, 321), new(405, 333), new(365, 321)

                ],
                TeethPoints =
                [
                    new(280, 239), new(530, 244),
                    new(280, 352), new(530, 357)
                ]
            }
        },
        {
            "Wide", new()
            {
                LipPoints =
                [
                    new(250, 295), new(405, 293), new(560, 295),
                    new(560, 302), new(405, 303), new(250, 302)
                ],
                TeethPoints =
                [
                    new(280, 272), new(530, 269),
                    new(280, 329), new(530, 329)
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
                    if (lipQ.Count == 0) break;
                    position = lipQ.Dequeue();
                }
                else if (visual is MovableTeethPointVisual)
                {
                    if (teethQ.Count == 0) break;
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


//Old preset:
/*
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
                    new(283, 264), new(535, 263),
                    new(287, 320), new(519, 317)
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
                    new(283, 266), new(535, 271),
                    new(287, 319), new(519, 322)
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
                    new(283, 211), new(535, 211),
                    new(287, 411), new(519, 411)
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
                    new(283, 239), new(535, 244),
                    new(287, 352), new(519, 357)
                ]
            }
        },
        {
            "Stretch", new()
            {
                LipPoints =
                [
                    new(238, 285), new( 398,282), new(570,284),
                    new(538,308), new(390,319), new(265,309)
                ],
                TeethPoints =
                [
                    new(283, 272), new(535, 269),
                    new(287, 329), new(519, 329)
                ]
            }
        }
*/