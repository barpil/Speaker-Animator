using System.Windows.Media;

namespace SpeakerAnimator.Model.utils;

public static class BrushExtensions
{
    public static Brush GetBrushFromHexColor(string hexColor)
    {
        return (Brush)new BrushConverter().ConvertFromString(hexColor);
    }
}