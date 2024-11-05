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
using SvgExporter = OxyPlot.SvgExporter;

namespace Plotter;

public partial class MainWindow
{
    private const double MinRadius = 0.05;
    private const double MaxRadius = 0.5;

    private DispatcherTimer _rotationTimer;
    private AxisAngleRotation3D _rotation;

    private PlotModel ScatterPlotModel { get; set; } = null!;
    private List<Cluster> _clusters = null!;
    private readonly Dictionary<GeometryModel3D, Cluster> _geometryModelToClusterMap = new();

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
            // Load and process the selected CSV file
            LoadAndProcessData(openFileDialog.FileName);
            // Populate the combo boxes with property keys
            PopulateComboBoxes();
            // Create the galaxy map visualization
            CreateGalaxyMap();
        }
        else
        {
            // Show an error message and close the application if no file is selected
            MessageBox.Show("No file selected. The application will now close.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Application.Current.Shutdown();
        }
    }

    private void LoadCsvData(string csvPath)
    {
        // Open the CSV file for reading
        using var reader = new StreamReader(csvPath);
        // Initialize the CSV reader with the appropriate culture
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        // Register the class map for the Cluster type
        csv.Context.RegisterClassMap<ClusterMap>();
        // Read the records from the CSV file and store them in the _clusters list
        _clusters = csv.GetRecords<Cluster>().ToList();
    }

    private void LoadAndProcessData(string csvPath)
    {
        // Load the CSV data into the _clusters list
        LoadCsvData(csvPath);

        // Find the minimum and maximum absolute magnitudes in the clusters
        var minMagnitude = _clusters.Min(c => c.AbsoluteMagnitude);
        var maxMagnitude = _clusters.Max(c => c.AbsoluteMagnitude);

        // Normalize the radius of each cluster based on its absolute magnitude
        foreach (var cluster in _clusters)
        {
            cluster.Radius = NormalizeMagnitude(cluster.AbsoluteMagnitude, minMagnitude, maxMagnitude);
        }

        // Add a special cluster representing the Sol system
        _clusters.Add(new Cluster()
        {
            Position = new Point3D(8.2, 0, 0),
            Color = Colors.Yellow,
            Radius = 0.2
        });
    }

    private static double NormalizeMagnitude(double magnitude, double minMagnitude, double maxMagnitude)
    {
        // Normalize the magnitude to a value between MinRadius and MaxRadius
        var normalized = (magnitude - maxMagnitude) / (minMagnitude - maxMagnitude);
        return MinRadius + normalized * (MaxRadius - MinRadius);
    }

    private void PopulateComboBoxes()
    {
        // Check if there are any clusters loaded
        if (_clusters.Count == 0)
            return;

        // Get the property keys from the first cluster
        var propertyKeys = _clusters.First().Properties.Keys.ToList();
        // Set the property keys as the items source for the combo boxes
        xAxisComboBox.ItemsSource = propertyKeys;
        yAxisComboBox.ItemsSource = propertyKeys;
        colorComboBox.ItemsSource = propertyKeys;
    }

    private void CreateGalaxyMap()
    {
        // Create a group to hold all the scatter points (stars)
        var scatterGroup = new Model3DGroup();
        foreach (var star in _clusters)
        {
            // Add each star as a sphere to the scatter group
            scatterGroup.Children.Add(CreateSphere(star.Position, star.Radius, star.Color, star));
        }

        // Create lights and a plane for the galaxy map
        var lights = CreateLights();
        var plane = CreatePlane();

        // Add the scatter group, lights, and plane to the HelixViewport
        helixViewport.Children.Add(new ModelVisual3D { Content = scatterGroup });
        helixViewport.Children.Add(new ModelVisual3D { Content = lights });
        helixViewport.Children.Add(new ModelVisual3D { Content = plane });
        helixViewport.Children.Add(new CoordinateSystemVisual3D());
    }

    private Model3D CreatePlane()
    {
        // Create a mesh builder to construct the plane geometry
        var meshBuilder = new MeshBuilder();
        double size = 5; // A large value to simulate an infinite plane

        // Add a quad (rectangle) to the mesh builder to represent the plane
        meshBuilder.AddQuad(
            new Point3D(-size, -size, 0),
            new Point3D(size, -size, 0),
            new Point3D(size, size, 0),
            new Point3D(-size, size, 0)
        );

        // Convert the mesh builder to a mesh
        var mesh = meshBuilder.ToMesh();

        // Create a semi-transparent blue material for the plane
        var material = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(128, 0, 0, 255)));

        // Return the geometry model for the plane
        return new GeometryModel3D(mesh, material);
    }

    private static Model3DGroup CreateLights()
    {
        // Create a new group to hold the lights
        var lights = new Model3DGroup();

        // Add an ambient light to the group
        lights.Children.Add(new AmbientLight(Colors.White));

        // Add a directional light to the group
        lights.Children.Add(new DirectionalLight(Colors.White, new Vector3D(1, -1, -1)));

        // Return the group containing the lights
        return lights;
    }

    private Model3D CreateSphere(Point3D center, double radius, Color color, Cluster cluster)
    {
        // Create a mesh builder to construct the sphere geometry
        var mesh = new MeshBuilder();
        mesh.AddSphere(center, radius);

        // Create a material for the sphere using the specified color
        var material = MaterialHelper.CreateMaterial(color);

        // Create a geometry model for the sphere using the mesh and material
        var geometryModel = new GeometryModel3D(mesh.ToMesh(), material);

        // Add the geometry model and cluster to the dictionary for future reference
        _geometryModelToClusterMap[geometryModel] = cluster;

        // Return the geometry model for the sphere
        return geometryModel;
    }

    private void GeneratePlot()
    {
        var xProperty = xAxisComboBox.SelectedItem?.ToString();
        var yProperty = yAxisComboBox.SelectedItem?.ToString();
        var colorProperty = colorComboBox.SelectedItem?.ToString();

        // Ensure all properties are selected
        if (xProperty == null || yProperty == null || colorProperty == null)
        {
            return;
        }

        // Create the scatter plot model with the selected properties
        CreateScatterPlotModel(xProperty, yProperty, colorProperty);
    }

    private void OnGeneratePlotClick(object sender, RoutedEventArgs e)
    {
        // Generate the plot when the button is clicked
        GeneratePlot();
    }

    private void OnAnimateClick(object sender, RoutedEventArgs e)
    {
        // Start the rotation animation when the button is clicked
        StartRotationAnimation();
        // StartOrbitAnimation(); // TODO: Fix the orbital animation
    }

    private void OnAxisSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Regenerate the plot when the axis selection changes
        GeneratePlot();
    }

    private void CreateScatterPlotModel(string xProperty, string yProperty, string colorProperty)
    {
        // Initialize the scatter plot model with a title
        ScatterPlotModel = new PlotModel { Title = $"{xProperty} vs {yProperty}" };
        // Setup the axes for the plot
        SetupAxes(xProperty, yProperty);

        // Create a scatter series for the plot
        var scatterSeries = CreateScatterSeries();

        var xValues = new List<double>();
        var yValues = new List<double>();
        var colorValues = new List<double>();

        // Clear any existing series in the plot model
        ScatterPlotModel.Series.Clear();
        // Populate the scatter series with data
        PopulateScatterSeries(scatterSeries, xValues, yValues, colorValues, xProperty, yProperty, colorProperty);

#if RENDER_GALAXY
    // Optionally add the galaxy to the scatter plot
    AddGalaxyToScatterPlot(xProperty, yProperty);
#endif // RENDER_GALAXY

        // Add the scatter series to the plot model
        ScatterPlotModel.Series.Add(scatterSeries);

        // Add a fitted line to the plot
        AddFittedLineToPlot(xValues, yValues);

        // Setup the color axis if there are color values
        if (colorValues.Count > 0)
        {
            var minColorValue = colorValues.Min();
            var maxColorValue = colorValues.Max();
            SetupColorAxis(colorProperty, minColorValue, maxColorValue);
        }

        // Set the plot model to the scatter plot view
        scatterPlotView.Model = ScatterPlotModel;
    }

    private void SetupAxes(string xProperty, string yProperty)
    {
        // Setup the X-axis
        ScatterPlotModel.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Title = xProperty,
            Minimum = double.NaN,
            Maximum = double.NaN,
            MinimumPadding = 0.1,
            MaximumPadding = 0.1
        });

        // Setup the Y-axis
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
        // Create and return a new scatter series
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

        // Populate the scatter series with data from the clusters
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

    private void SetupColorAxis(string property, double minColorValue, double maxColorValue)
    {
        // Setup the color axis for the plot
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

    private void AddFittedLineToPlot(List<double> xValues, List<double> yValues)
    {
        if (xValues.Count * yValues.Count == 0)
            return;

        // Perform linear regression to fit a line to the data
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

        // Add the fitted line series to the plot model
        ScatterPlotModel.Series.Add(lineSeries);
    }

    private void SavePlotAsSvg(string filePath)
    {
        // Create an SVG exporter with specified dimensions
        var exporter = new SvgExporter { Width = 1500, Height = 1000 };

        // Export the plot model to an SVG file
        using (var stream = File.Create(filePath))
        {
            exporter.Export(ScatterPlotModel, stream);
        }

        // Show a success message to the user
        MessageBox.Show($"Plot saved to {filePath}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnSavePlotClick(object sender, RoutedEventArgs e)
    {
        // Show a save file dialog to the user
        var saveFileDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "SVG files (*.svg)|*.svg|All files (*.*)|*.*",
            DefaultExt = "svg"
        };

        // Save the plot as an SVG file if the user selects a file
        if (saveFileDialog.ShowDialog() == true)
        {
            SavePlotAsSvg(saveFileDialog.FileName);
        }
    }

#if RENDER_GALAXY
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
#endif // RENDER_GALAXY

    private void StartOrbitAnimation()
    {
        // Create a timer to update cluster positions
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        timer.Tick += (_, _) => UpdateClusterPositions();
        timer.Start();
    }

    private void UpdateClusterPositions()
    {
        // Update the position of each cluster
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

        if (modelVisual is not { Content: Model3DGroup modelGroup }) return;

        // Find the specific GeometryModel3D within the Model3DGroup
        var geometryModel = modelGroup.Children.OfType<GeometryModel3D>()
            .FirstOrDefault(g => _geometryModelToClusterMap[g] == cluster);

        if (geometryModel == null) return;

        // Calculate the translation needed to move the cluster to the new position
        var transform = new TranslateTransform3D(newPosition.X - cluster.Position.X, newPosition.Y - cluster.Position.Y, newPosition.Z - cluster.Position.Z);
        geometryModel.Transform = transform;

        // Update the cluster's position
        cluster.Position = newPosition;
    }

    private void StartRotationAnimation()
    {
        // Initialize the rotation around the Z-axis with an initial angle of 0 degrees
        _rotation = new AxisAngleRotation3D(new Vector3D(0, 0, 1), 0);
        var rotateTransform = new RotateTransform3D(_rotation);
        // Apply the rotation transform to the camera
        helixViewport.Camera.Transform = rotateTransform;

        // Create a timer to update the rotation at ~60 FPS
        _rotationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        // Attach the UpdateRotation method to the timer's Tick event
        _rotationTimer.Tick += (s, e) => UpdateRotation();
        // Start the timer
        _rotationTimer.Start();
    }

    private void UpdateRotation()
    {
        // Increment the rotation angle by 0.5 degrees
        _rotation.Angle += 0.5; // Adjust the rotation speed as needed
        // Reset the angle to 0 if it reaches or exceeds 360 degrees
        if (_rotation.Angle >= 360)
        {
            _rotation.Angle -= 360;
        }
    }
}
