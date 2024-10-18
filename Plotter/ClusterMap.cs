using CsvHelper.Configuration;
using System.Windows.Media.Media3D;

namespace Plotter;

public sealed class ClusterMap : ClassMap<Cluster>
{
    public ClusterMap()
    {
        // Map ID
        Map(m => m.ID).Name("ID");

        // Map X, Y, Z to Position object
        Map(m => m.Position).Convert(args =>
        {
            var x = double.Parse(args.Row.GetField("X")) + 8.2; // Translate to galactic centric
            var y = double.Parse(args.Row.GetField("Y"));
            var z = double.Parse(args.Row.GetField("Z"));
            return new Point3D(x, y, z);
        });

        // Map Angular Momentum components
        //Map(m => m.AngularMomentumX).Name("AngularMomentumX");
        //Map(m => m.AngularMomentumY).Name("AngularMomentumY");
        //Map(m => m.AngularMomentumZ).Name("AngularMomentumZ");

        // Handle the dynamic fields
        Map(m => m.Properties).Convert(args =>
        {
            var dynamicFields = new Dictionary<string, object>();
            var row = args.Row;

            foreach (var header in row.HeaderRecord)
            {
                // Skip the ID
                if (header is "ID")
                    continue;

                dynamicFields[header] = row.GetField(header);
            }

            return dynamicFields;
        });
    }
}
