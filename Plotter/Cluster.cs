using SciColorMaps.Portable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Plotter;

public class Cluster
{
    private const double MinMetallicity = -2.5; // Metal-poor (blue)
    private const double MaxMetallicity = 0.5; // Metal-rich (red)

    private static readonly ColorMap ViridisColorMap = new();

    public string ID { get; set; }
    public Point3D Position { get; set; }
    public Color Color
    {
        get
        {
            if (fColor != null) return (Color)fColor;
            try
            {
                var metallicity = double.Parse(Properties["[Fe/H]"].ToString());
                var normalizedMetallicity = (metallicity - MinMetallicity) / (MaxMetallicity - MinMetallicity);
                normalizedMetallicity = Math.Max(0, Math.Min(1, normalizedMetallicity)); // Clamp between 0 and 1
                fColor = ViridisColorMap[normalizedMetallicity].ToMediaColor();
            }
            catch (Exception e)
            {
                fColor = Color.FromRgb(0, 255, 0);
            }

            return (Color)fColor;
        }
        init => fColor = value;
    }
    public Dictionary<string, object> Properties { get; set; } = new();

    public double AbsoluteMagnitude
    {
        get
        {
            try
            {
                return double.Parse(Properties["M_V,t"].ToString() ?? "");
            }
            catch (Exception e)
            {
                return 0;
            }
        }
    }
    public double Metallicity { get; set; }
    public double Radius { get; set; } = 0.1;
    public double HeliocentrixRadialVelicoty { get; set; }

    // Provided orbital parameters
    public double Pericenter { get; set; }
    public double Apocenter { get; set; }
    public double AngularMomentumX { get; set; }
    public double AngularMomentumY { get; set; }
    public double AngularMomentumZ { get; set; }

    // Derived orbital parameters
    public double SemiMajorAxis => (Pericenter + Apocenter) / 2;
    public double Eccentricity => (Apocenter - Pericenter) / (Apocenter + Pericenter);

    private Color? fColor = null;
}
