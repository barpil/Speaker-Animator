namespace SpeakerAnimator.Model.NN;

public static class VisemeToPoseMapper
{

    private static readonly Dictionary<VisemeEnum, Dictionary<string, double>> _visemeToPoseMap = new()
    {
        [VisemeEnum.sil] = new()
        {
            ["Top-Lip-Up"] = 0, ["Bottom-Lip-Down"] = 0, ["Mouth-Open"] = 0, ["Pucker"] = 0, ["Wide"] = 0
        },
        [VisemeEnum.PP] = new()
        {
            ["Top-Lip-Up"] = 0.1, ["Bottom-Lip-Down"] = 1, ["Mouth-Open"] = 0.05, ["Pucker"] = 0, ["Wide"] = 0
        },
        [VisemeEnum.FF] = new()
        {
            ["Top-Lip-Up"] = 0.6, ["Bottom-Lip-Down"] = 0, ["Mouth-Open"] = 0, ["Pucker"] = 0.05, ["Wide"] = 0.3
        },
        [VisemeEnum.TH] = new()
        {
            ["Top-Lip-Up"] = 0.3, ["Bottom-Lip-Down"] = 0.3, ["Mouth-Open"] = 0.15, ["Pucker"] = 0.3, ["Wide"] = 0.2
        },
        [VisemeEnum.DD] = new()
        {
            ["Top-Lip-Up"] = 0.3, ["Bottom-Lip-Down"] = 0.3, ["Mouth-Open"] = 0.15, ["Pucker"] = 0, ["Wide"] = 0.2
        },
        [VisemeEnum.kk] = new()
        {
            ["Top-Lip-Up"] = 1, ["Bottom-Lip-Down"] = 0.4, ["Mouth-Open"] = 0.3, ["Pucker"] = 0.3, ["Wide"] = 0
        },
        [VisemeEnum.CH] = new()
        {
            ["Top-Lip-Up"] = 0.5, ["Bottom-Lip-Down"] = 1, ["Mouth-Open"] = 0.0, ["Pucker"] = 0.35, ["Wide"] = 0
        },
        [VisemeEnum.SS] = new()
        {
            ["Top-Lip-Up"] = 0.6, ["Bottom-Lip-Down"] = 0.6, ["Mouth-Open"] = 0, ["Pucker"] = 0, ["Wide"] = 0.6
        },
        [VisemeEnum.nn] = new()
        {
            ["Top-Lip-Up"] = 0.2, ["Bottom-Lip-Down"] = 0.9, ["Mouth-Open"] = 0.05, ["Pucker"] = 0, ["Wide"] = 0
        },
        [VisemeEnum.RR] = new()
        {
            ["Top-Lip-Up"] = 0.35, ["Bottom-Lip-Down"] = 0, ["Mouth-Open"] = 0, ["Pucker"] = 0.15, ["Wide"] = 0
        },
        [VisemeEnum.aa] = new()
        {
            ["Top-Lip-Up"] = 0, ["Bottom-Lip-Down"] = 0, ["Mouth-Open"] = 0.5, ["Pucker"] = 0.15, ["Wide"] = 0
        },
        [VisemeEnum.E] = new()
        {
            ["Top-Lip-Up"] = 0.25, ["Bottom-Lip-Down"] = 0, ["Mouth-Open"] = 0.5, ["Pucker"] = 0, ["Wide"] = 0.4
        },
        [VisemeEnum.I] = new()
        {
            ["Top-Lip-Up"] = 0.25, ["Bottom-Lip-Down"] = 0.1, ["Mouth-Open"] = 0.2, ["Pucker"] = 0.2, ["Wide"] = 0.4
        },
        [VisemeEnum.O] = new()
        {
            ["Top-Lip-Up"] = 0, ["Bottom-Lip-Down"] = 0, ["Mouth-Open"] = 0.3, ["Pucker"] = 0.8, ["Wide"] = 0
        },
        [VisemeEnum.U] = new()
        {
            ["Top-Lip-Up"] = 0, ["Bottom-Lip-Down"] = 0, ["Mouth-Open"] = 0, ["Pucker"] = 1, ["Wide"] = 0
        },
        
    };

    public static Dictionary<string, double> GetPoseDict(VisemeEnum viseme)
    {
        return _visemeToPoseMap[viseme];
    }
    
    public static Dictionary<string, double> GetPoseDict(string visemeString)
    {
        try
        {
            VisemeEnum viseme = (VisemeEnum) Enum.Parse(typeof(VisemeEnum), visemeString);
            return GetPoseDict(viseme);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Unknown viseme: {visemeString}");
            return new Dictionary<string, double>();
        }
    }
}

//Pierwszy mapping
/*
[VisemeEnum.sil] = new()
        {
            ["Top-Lip-Up"] = 0, ["Bottom-Lip-Down"] = 0, ["Mouth-Open"] = 0, ["Pucker"] = 0
        },
        [VisemeEnum.PP] = new()
        {
            ["Top-Lip-Up"] = 0, ["Bottom-Lip-Down"] = 1, ["Mouth-Open"] = 0, ["Pucker"] = 0
        },
        [VisemeEnum.FF] = new()
        {
            ["Top-Lip-Up"] = 0.5, ["Bottom-Lip-Down"] = 1, ["Mouth-Open"] = 0, ["Pucker"] = 0.3
        },
        [VisemeEnum.TH] = new()
        {
            ["Top-Lip-Up"] = 0.5, ["Bottom-Lip-Down"] = 1, ["Mouth-Open"] = 0.5, ["Pucker"] = 0.3
        },
        [VisemeEnum.DD] = new()
        {
            ["Top-Lip-Up"] = 1, ["Bottom-Lip-Down"] = 1, ["Mouth-Open"] = 0.5, ["Pucker"] = 0.3
        },
        [VisemeEnum.kk] = new()
        {
            ["Top-Lip-Up"] = 1, ["Bottom-Lip-Down"] = 0.4, ["Mouth-Open"] = 0.3, ["Pucker"] = 0.3
        },
        [VisemeEnum.CH] = new()
        {
            ["Top-Lip-Up"] = 1, ["Bottom-Lip-Down"] = 0.4, ["Mouth-Open"] = 0.1, ["Pucker"] = 0.7
        },
        [VisemeEnum.SS] = new()
        {
            ["Top-Lip-Up"] = 1, ["Bottom-Lip-Down"] = 0, ["Mouth-Open"] = 0, ["Pucker"] = 0
        },
        [VisemeEnum.nn] = new()
        {
            ["Top-Lip-Up"] = 1, ["Bottom-Lip-Down"] = 0.6, ["Mouth-Open"] = 0, ["Pucker"] = 0.5
        },
        [VisemeEnum.RR] = new()
        {
            ["Top-Lip-Up"] = 0, ["Bottom-Lip-Down"] = 1, ["Mouth-Open"] = 0.3, ["Pucker"] = 0
        },
        [VisemeEnum.aa] = new()
        {
            ["Top-Lip-Up"] = 0, ["Bottom-Lip-Down"] = 1, ["Mouth-Open"] = 0.6, ["Pucker"] = 0.7
        },
        [VisemeEnum.E] = new()
        {
            ["Top-Lip-Up"] = 0.2, ["Bottom-Lip-Down"] = 0.4, ["Mouth-Open"] = 0.2, ["Pucker"] = 0
        },
        [VisemeEnum.I] = new()
        {
            ["Top-Lip-Up"] = 0, ["Bottom-Lip-Down"] = 0, ["Mouth-Open"] = 0.3, ["Pucker"] = 0
        },
        [VisemeEnum.O] = new()
        {
            ["Top-Lip-Up"] = 0, ["Bottom-Lip-Down"] = 1, ["Mouth-Open"] = 0.3, ["Pucker"] = 1
        },
        [VisemeEnum.U] = new()
        {
            ["Top-Lip-Up"] = 0, ["Bottom-Lip-Down"] = 1, ["Mouth-Open"] = 0, ["Pucker"] = 0.8
        },
*/