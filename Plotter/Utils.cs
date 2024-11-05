using System.Windows.Media;

namespace Plotter;
public static class Utils
{
    public static Color ToMediaColor(this byte[] rgb)
    {
        return Color.FromRgb(rgb[0], rgb[1], rgb[2]);
    }
}