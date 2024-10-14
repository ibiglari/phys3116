using System.Diagnostics;
using CsvHelper;
using HelixToolkit.Wpf;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace Plotter;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    private const double MinRadius = 0.05;
    private const double MaxRadius = 0.5;
    private PlotModel ScatterPlotModel { get; set; } = null!;
    private List<Cluster> _clusters = null!;
    private readonly Random _random = new();
    private readonly Dictionary<GeometryModel3D, Cluster> _geometryModelToClusterMap = new();

    private readonly Cluster _galaxy = new()
    {
        ID = "MilkyWay",
        Position = new Point3D(0, 0, 0),
        Color = Colors.White,
        Properties = new Dictionary<string, object>()
        {
            {"Age1", "13.61"},
            {"Age2", "13.61"},
            {"[Fe/H]", "0.02"},
            {"FeH", "0.02"}
    }
    };

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        // Prompt the user to select a CSV file on startup
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            string selectedFilePath = openFileDialog.FileName;
            LoadAndProcessData(selectedFilePath);
            PopulateComboBoxes();
            CreateGalaxyMap();
        }
        else
        {
            MessageBox.Show("No file selected. The application will now close.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Application.Current.Shutdown();
        }
    }

    private void LoadAndProcessData(string csvPath)
    {
        LoadCsvData(csvPath);
        var minMagnitude = _clusters.Min(c => c.AbsoluteMagnitude);
        var maxMagnitude = _clusters.Max(c => c.AbsoluteMagnitude);

        foreach (var cluster in _clusters)
        {
            cluster.Radius = NormalizeMagnitude(cluster.AbsoluteMagnitude, minMagnitude, maxMagnitude);
        }

        _clusters.Add(new Cluster()
        {
            Position = new Point3D(8.2, 0, 0),
            Color = Colors.Yellow,
            Radius = 0.2
        });
    }

    private static double NormalizeMagnitude(double magnitude, double minMagnitude, double maxMagnitude)
    {
        var normalized = (magnitude - maxMagnitude) / (minMagnitude - maxMagnitude);
        return MinRadius + normalized * (MaxRadius - MinRadius);
    }

    private void PopulateComboBoxes()
    {
        if (_clusters.Count == 0)
            return;

        var propertyKeys = Enumerable.First(_clusters).Properties.Keys.ToList();
        xAxisComboBox.ItemsSource = propertyKeys;
        yAxisComboBox.ItemsSource = propertyKeys;
        colorComboBox.ItemsSource = propertyKeys;
    }

    private void GeneratePlot()
    {
        var xProperty = xAxisComboBox.SelectedItem?.ToString();
        var yProperty = yAxisComboBox.SelectedItem?.ToString();
        var colorProperty = colorComboBox.SelectedItem?.ToString();

        if (xProperty == null || yProperty == null || colorProperty == null)
        {
            return;
        }

        CreateScatterPlotModel(xProperty, yProperty, colorProperty);
    }

    private void OnGeneratePlotClick(object sender, RoutedEventArgs e)
    {
        GeneratePlot();
    }

    private void OnAnimateClick(object sender, RoutedEventArgs e)
    {
        StartOrbitAnimation();
    }

    private void OnAxisSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        GeneratePlot();
    }

    private void CreateScatterPlotModel(string xProperty, string yProperty, string colorProperty)
    {
        ScatterPlotModel = new PlotModel { Title = $"{xProperty} vs {yProperty}" };
        SetupAxes(xProperty, yProperty);

        var scatterSeries = CreateScatterSeries();

        var xValues = new List<double>();
        var yValues = new List<double>();
        var colorValues = new List<double>();

        ScatterPlotModel.Series.Clear();
        PopulateScatterSeries(scatterSeries, xValues, yValues, colorValues, xProperty, yProperty, colorProperty);

 
        // AddGalaxyToScatterPlot(xProperty, yProperty);
        ScatterPlotModel.Series.Add(scatterSeries);

        AddFittedLineToPlot(xValues, yValues);

        if (colorValues.Count > 0)
        {
            // Get the actual min and max color values
            var minColorValue = colorValues.Min();
            var maxColorValue = colorValues.Max();

            // Setup the color axis with the actual min and max values
            SetupColorAxis(colorProperty, minColorValue, maxColorValue);
        }
        scatterPlotView.Model = ScatterPlotModel;
    }

    private void SetupColorAxis(string property, double minColorValue, double maxColorValue)
    {
        var colorAxis = new LinearColorAxis
        {
            Position = AxisPosition.Right,
            Palette = OxyPalettes.Viridis(), // You can choose a different palette if you prefer
            Title = property,
            Minimum = minColorValue, // Set to actual minimum value
            Maximum = maxColorValue  // Set to actual maximum value
        };
        ScatterPlotModel.Axes.Add(colorAxis);
    }

    private void SetupAxes(string xProperty, string yProperty)
    {
        ScatterPlotModel.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Title = xProperty,
            Minimum = double.NaN,
            Maximum = double.NaN,
            MinimumPadding = 0.1,
            MaximumPadding = 0.1
        });

        ScatterPlotModel.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = yProperty,
            Minimum = double.NaN,
            Maximum = double.NaN,
            MinimumPadding = 0.1,
            MaximumPadding = 0.1
        });
    }

    private static ScatterSeries CreateScatterSeries()
    {
        return new ScatterSeries
        {
            MarkerType = MarkerType.Circle,
            MarkerSize = 5,
            TrackerFormatString = "Name: {Tag}\nX: {X}\nY: {Y}\nColor: {Value}"
        };
    }

    private void PopulateScatterSeries(ScatterSeries scatterSeries, List<double> xValues, List<double> yValues, List<double> colorValues, string xProperty, string yProperty, string colorProperty)
    {
        Debug.Assert(_clusters != null, nameof(_clusters) + " != null");

        foreach (var cluster in _clusters)
        {
            if (!cluster.Properties.TryGetValue(xProperty, out var xObj) ||
                !cluster.Properties.TryGetValue(yProperty, out var yObj) ||
                !cluster.Properties.TryGetValue(colorProperty, out var colorObj) ||
                xObj is not string xStr || yObj is not string yStr || colorObj is not string colorStr ||
                !double.TryParse(xStr, out var xValue) || !double.TryParse(yStr, out var yValue) || !double.TryParse(colorStr, out var colorValue)) continue;

            var scatterPoint = new ScatterPoint(xValue, yValue, 5, colorValue)
            {
                Tag = cluster.ID // Set the Tag property to the cluster's name
            };

            scatterSeries.Points.Add(scatterPoint);
            xValues.Add(xValue);
            yValues.Add(yValue);
            colorValues.Add(colorValue);
        }
    }

    private void AddGalaxyToScatterPlot(string xProperty, string yProperty)
    {
        // Add the galaxy cluster if it contains the specified properties
        if (!_galaxy.Properties.TryGetValue(xProperty, out var xGalaxyObj) ||
            !_galaxy.Properties.TryGetValue(yProperty, out var yGalaxyObj) ||
            xGalaxyObj is not string xGalaxyStr || yGalaxyObj is not string yGalaxyStr ||
            !double.TryParse(xGalaxyStr, out var xGalaxyValue) ||
            !double.TryParse(yGalaxyStr, out var yGalaxyValue)) return;

        // Create a separate ScatterSeries for the galaxy cluster
        var galaxySeries = new ScatterSeries
        {
            MarkerType = MarkerType.Diamond, // Change the marker type
            MarkerSize = 10, // Increase the marker size
            MarkerFill = OxyColors.Yellow // Change the marker color
        };

        galaxySeries.Points.Add(new ScatterPoint(xGalaxyValue, yGalaxyValue));

        // Add the galaxy series to the plot model
        ScatterPlotModel.Series.Add(galaxySeries);
    }

    private void AddFittedLineToPlot(List<double> xValues, List<double> yValues)
    {
        if (xValues.Count * yValues.Count == 0)
            return;
        var (intercept, slope) = MathNet.Numerics.LinearRegression.SimpleRegression.Fit(xValues.ToArray(), yValues.ToArray());

        var lineSeries = new LineSeries
        {
            Color = OxyColors.Red,
            StrokeThickness = 2
        };

        var minX = xValues.Min();
        var maxX = xValues.Max();
        lineSeries.Points.Add(new DataPoint(minX, slope * minX + intercept));
        lineSeries.Points.Add(new DataPoint(maxX, slope * maxX + intercept));

        ScatterPlotModel.Series.Add(lineSeries);
    }

    private void LoadCsvData(string csvPath)
    {
        using var reader = new StreamReader(csvPath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        csv.Context.RegisterClassMap<ClusterMap>();
        _clusters = csv.GetRecords<Cluster>().ToList();
    }

    private void CreateGalaxyMap()
    {
        var scatterGroup = new Model3DGroup();
        foreach (var star in _clusters)
        {
            scatterGroup.Children.Add(CreateSphere(star.Position, star.Radius, star.Color, star));
        }

        var lights = CreateLights();
        helixViewport.Children.Add(new ModelVisual3D { Content = scatterGroup });
        helixViewport.Children.Add(new ModelVisual3D { Content = lights });
        helixViewport.Children.Add(new CoordinateSystemVisual3D());
    }

    private static Model3DGroup CreateLights()
    {
        var lights = new Model3DGroup();
        lights.Children.Add(new AmbientLight(Colors.White));
        lights.Children.Add(new DirectionalLight(Colors.White, new Vector3D(1, -1, -1)));
        return lights;
    }

    private Model3D CreateSphere(Point3D center, double radius, Color color, Cluster cluster)
    {
        var mesh = new MeshBuilder();
        mesh.AddSphere(center, radius);

        var material = MaterialHelper.CreateMaterial(color);
        var geometryModel = new GeometryModel3D(mesh.ToMesh(), material);

        // Add the geometryModel and cluster to the dictionary
        _geometryModelToClusterMap[geometryModel] = cluster;

        return geometryModel;
    }

    private void StartOrbitAnimation()
    {
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        timer.Tick += (_, _) => UpdateClusterPositions();
        timer.Start();
    }

    private void UpdateClusterPositions()
    {
        foreach (var cluster in _clusters)
        {
            var newPosition = CalculateOrbitalPosition(cluster);
            UpdateClusterPosition(cluster, newPosition);
        }
    }

    private static Point3D CalculateOrbitalPosition(Cluster cluster)
    {
        // Implement orbital mechanics calculations here using cluster.SemiMajorAxis and cluster.Eccentricity
        // For simplicity, let's assume a circular orbit for now
        var time = DateTime.Now.Ticks / TimeSpan.TicksPerSecond;
        var angle = (time % 360) * Math.PI / 180; // Convert to radians
        var x = cluster.SemiMajorAxis * Math.Cos(angle);
        var y = cluster.SemiMajorAxis * Math.Sin(angle);
        return new Point3D(x, y, cluster.Position.Z);
    }

    private void UpdateClusterPosition(Cluster cluster, Point3D newPosition)
    {
        // Find the corresponding ModelVisual3D for the cluster
        var modelVisual = helixViewport.Children.OfType<ModelVisual3D>()
            .FirstOrDefault(m => m.Content is Model3DGroup group && group.Children.OfType<GeometryModel3D>()
            .Any(g => _geometryModelToClusterMap[g] == cluster));

        if (modelVisual != null && modelVisual.Content is Model3DGroup modelGroup)
        {
            // Find the specific GeometryModel3D within the Model3DGroup
            var geometryModel = modelGroup.Children.OfType<GeometryModel3D>()
                .FirstOrDefault(g => _geometryModelToClusterMap[g] == cluster);

            if (geometryModel != null)
            {
                var transform = new TranslateTransform3D(newPosition.X - cluster.Position.X, newPosition.Y - cluster.Position.Y, newPosition.Z - cluster.Position.Z);
                geometryModel.Transform = transform;
                cluster.Position = newPosition;
            }
        }
    }
}
