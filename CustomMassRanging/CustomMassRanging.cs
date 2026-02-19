using System;
using System.Collections.Generic; //IEnumerable<>
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using CommunityToolkit.Mvvm.Input;
using System.Text;
using System.Xml.Serialization;

using CommunityToolkit.HighPerformance;
using System.Fabric;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;

//using ClosedXML.Excel; // NuGet install ClosedXML

/// CustomMassRanging
/// 
/// dotnet pack --configuration Release --property:Version=1.0.0 --property:PackageOutputPath="C:\Users\tprosa\OneDrive - AMETEK Inc\Desktop\MyExtensions"
/// 
/// Update:
///     Start with ranges as defined in IVAS ROI
///     Confirm they are non-overlapping
///     Find max histogram bin, determine FWHunM (FWThM was used in paper)
///     Use FWHunM to determine binning for display and ranging
///     Display
/// 
/// Allow user to modify default parameters
///
/// Rerange:
///     Following parameter values, determine new range min, max
///     Beginning implimation is overly simple
///     Check again for non-overlapping results
/// 
/// Apply:
///     Move values back to IVAS
///     This change will trigger reloading
///     
/// Saving...
///     At start, "Properties" for this extension are either
///         1) default from this extension, or
///         2) values from saved analysis state
///     Any change in "Properties" triggers a need to "Update" from beginning
///     Any changes to "Parameters" are copied to "Properties" upon Apply  
///     
/// Programming Notes:
///     the controls:Chart2D is not the same and an IVAS chart.
///     Perhaps this can become more equivelant to the IVAS chart, but it is only aproximate:
///     1) Double-clicking does not completely unzoom, you need to do it twice
///     2) The auto-y-scaling does not work correctly
///     3) Log to lin can also have unexpected results
///     4) Interesting:  The plot is X vs. Z (it is a view into a 3D graph).  So, the renering
///     of varios plots is orderd by the Y value (negative being in front and positive in back).
///     5) Looking for a work around to current Sphere and Points plotting options.  Not useful.

namespace CustomMassRanging;

[DefaultView(CustomMassRangingViewModel.UniqueId, typeof(CustomMassRangingViewModel))]
internal partial class CustomMassRanging : BasicCustomAnalysisBase<CustomMassRangingProperties>
{
    public const string UniqueId = "CustomMassRanging.CustomMassRanging";
    public static INodeDisplayInfo DisplayInfo { get; } = new NodeDisplayInfo("Custom Mass Ranging");
    public ObservableCollection<IRenderData> HistogramData { get; } = new();
    public float MaxIntensity { get; set; } = 0.0f;
    public float Resolution { get; set; } = 0.0f;

    public ObservableCollection<IRenderData> SepHistogramData { get; } = new();

    //RangesTable is the displayed table (one being used for compositions)
    //StartTable contains the original imported ranges
    public ObservableCollection<RangesTableEntries> RangesTable { get; set; } = new();
    public ObservableCollection<RangesTableEntries> StartTable { get; set; } = new();

    //CompositionTable can be built after RangesTable has been updated
    public ObservableCollection<CompositionTableEntries> IonicCompositionTable { get; } = new();
    public CompositionTableTotals IonicCompositionTotals { get; set; } = new(); 
    public ObservableCollection<CompositionTableEntries> DecomposedCompositionTable { get; } = new();
    public CompositionTableTotals DecomposedCompositionTotals { get; set; } = new();

    //[Display(Name = "MultisInformation", AutoGenerateField = true, Description = "Multihit Information")]
    public MyViewableString MultisInformation { get; set; } = new();
    
    //values is the class that does all the processing of the coarsened mass spectrum
    public MyRanging? values;

    // Bound to the PropertyGrid
    public Parameters Parameters { get; set; } = new();

    // Plotting colors to use
    public Color[] color = new Color[] {
        Colors.Blue,
        Colors.Red,
        Colors.Green,
        Colors.Black,
        Colors.Purple,
        Colors.Orange,
        Colors.Brown,
        Colors.Cyan,
        Colors.Salmon
    };
    public Color[] lightColor = new Color[] {
        Colors.LightBlue,
        Colors.Magenta,
        Colors.LightGreen,
        Colors.Gray,
        Colors.Lavender,
        Colors.PeachPuff,
        Colors.Tan,
        Colors.LightCyan,
        Colors.LightSalmon
    };

    // Need to seperatly track sepPlots for export
    public List<Vector2[]> savedPlots = new();
    public List<string> savedLegends = new();
    public string saveFileName = "";

    // Add a line to the constructor to wire up the event handler to the Properties object PropertyChanged event
    public CustomMassRanging(IStandardAnalysisFilterNodeBaseServices services, ResourceFactory resourceFactory)
    : base(services, resourceFactory)
    {
        //Changed for first implimentataion to allow for PropertyChanged (otherwise nothing here)
        Parameters.PropertyChanged += OnParametersChanged;
    }

    // Event handler called when the Properties object has changes in one of its properties
    protected override void OnPropertiesChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertiesChanged(e);
        DataStateIsValid = false;
    }

    // Event handler called when the Parameters object has changes in one of its properties
    private void OnParametersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Parameters.ViewportLower))
        {
            Parameters.LowerX = Parameters.ViewportLower.X;
            Parameters.LowerY = Parameters.ViewportLower.Y;
        }
        else if (e.PropertyName == nameof(Parameters.ViewportUpper))
        {
            Parameters.UpperX = Parameters.ViewportUpper.X;
            Parameters.UpperY = Parameters.ViewportUpper.Y;
        }
        else if (e.PropertyName == nameof(Parameters.LowerX) || e.PropertyName == nameof(Parameters.LowerY))
            Parameters.ViewportLower = new Vector2(Parameters.LowerX, Parameters.LowerY);
        else if (e.PropertyName == nameof(Parameters.UpperX) || e.PropertyName == nameof(Parameters.UpperY))
            Parameters.ViewportUpper = new Vector2(Parameters.UpperX, Parameters.UpperY);
    }

    // The main update method
    protected override async Task<bool> Update(CancellationToken cancellationToken)
    {
        await Task.Yield();

        // Clear existing view data
        HistogramData.Clear();
        RangesTable.Clear();
        StartTable.Clear();
        IonicCompositionTable.Clear();
        IonicCompositionTotals.Clear();
        DecomposedCompositionTable.Clear();
        DecomposedCompositionTotals.Clear();
        SepHistogramData.Clear();

        // Check that we have a valid RangeManager (an analysis set with no mass spectrum node will return a null range manager)
        if (Resources.RangeManager is not { } rangeManager)
        {
            MessageBox.Show(
                "Invalid RangeManager",
                "Custom Analysis Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }

        // Get mass spectrum data - not always caught by lack or RangeManager
        var massHist = Resources.GetMassSpectrum()?.GetData<IMassSpectrumData>()?.MassHistogram;
        if (massHist is null)
        {
            MessageBox.Show(
                "Must create a Mass Spectrum Analysis first!",
                "Custom Analysis Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
        var nameExpermientName = Resources.AnalysisSetTitle.ToString(); //R5100_241627 New Analysis Tree
        var nameTool = Resources.Title.ToString(); //Custom Mass Ranging
        saveFileName = nameExpermientName + "_" + nameTool;

        // Use the ViewBuilder to display a chart, in this case -- null check is probably not necessary
        if (massHist is not null)
        {
            //Properties Table and values are filled via AssessHistogram
            AssessHistogramMakeValuesArray(massHist);

            //Check to see if histogram returned values for ranging
            //Also check overlaps
            if (values is null)
            {
                MessageBox.Show(
                    "No values found. No ranges defined in original node.\n" +
                    "Current implimentation modifies _existing_ range definitions.",
                    "Custom Analysis Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
            else
            {
                for (int i = 0; i < values.Values.Length; i++)
                    if (values.Values[i].Y > MaxIntensity) MaxIntensity = values.Values[i].Y;

                //Starts in log scale, so y must be >0 (here 1 or greater)
                if (Properties.ViewportUpper.X <= 0.0f && Properties.ViewportUpper.Y <= 1.0f)
                    Properties.ViewportUpper = new Vector2(values.Values.Last().X, MaxIntensity);

                //Here a copy of the current ranges is made as a starting point for RangesTable
                StartCustomMassRangesTable();

                bool tryResolveOverlapps = false;
                if (CheckForOverlappingRanges(tryResolveOverlapps)) return false; //false = invalid state

                var rangesDisplay = RangesTable
                    .Select(r => (IChart2DSlice)(new Chart2DSlice((float)r.Min, (float)r.Max, r.Color)))
                    .ToList();

                // Create the histogram data to be added to the chart in the view
                var histogramRenderData = Resources.ChartObjects
                    .CreateHistogram(values.Values, Colors.Black, verticalSlices: rangesDisplay, name: "Mass Spectrum");

                // Adding to the HistogramData ObservableCollection notifies the bound Chart2D in the view to update with the plot data
                HistogramData.Add(histogramRenderData);
            }
        }

        //Properties is an enherited observable from CustomMassRangignProperties -- this needs to be after making of plots
        Parameters.ResetParametersObservablesToPropertiesObservables(Properties);

        // Return true as the update completed successfully and the data state of the analysis should be considered valid
        return true;
    }

    // A RerangeCommand property is generated that can be used to bind this action to the view
    [RelayCommand]
    public async Task Rerange()
    {
        await Task.Yield();

        var lower = Parameters.ViewportLower;
        var upper = Parameters.ViewportUpper;

        HistogramData.Clear();

        // Check that there is at least 1 existing range
        if (StartTable.Count() < 1)
        {
            MessageBox.Show(
                "No ranges defined in original node.\n" +
                "Current implimentation modifies _existing_ range definitions.",
                "Custom Analysis Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        // values is the coarsened mass histogram
        // Discover all peaks first
        List<Vector3> peaks = values!.FindAllPeaks(Parameters);
        Vector3[] peakPoints = new Vector3[peaks.Count];
        for (int i = 0; i < peaks.Count; i++)
        {
            peakPoints[i] = peaks.ElementAt(i);
            peakPoints[i].Y = -1.0f;
        }

        ISeriesRenderData series = Resources.ChartObjects.CreateSeries();
        //series.IsLegendVisible = false;
        //series.Name = "";
        series.Positions = peakPoints;  // Position data here
        series.MarkerColor = Colors.Blue;
        series.MarkerShape = MarkerShape.Triangle;
        series.Thickness = 0;
        HistogramData.Add(series);

        /* Lines instead of triangles?
        foreach (var peak in peaks)
        {
            Vector3[] peakLine = new Vector3[2];
            peakLine[0] = peak;
            peakLine[1] = peak;
            peakLine[1].Z = 1.0f;
            peakLine[0].Y = 1.0f;//Behind any defined ranges
            peakLine[1].Y = 1.0f;
            var histogramRenderData2 = Resources.ChartObjects.CreateLine(peakLine, Colors.Blue, 1f);
            HistogramData.Add(histogramRenderData2);
        }
        */

        if (Parameters.BIgnoreDiscoveredUnknownPeaks)
        {
            var MinSortedRangesTable = StartTable.OrderBy(o => o.Pos).ToList();
            // Remember scheme unless null
            foreach (var range0 in MinSortedRangesTable)
            {
                // Find previous
                foreach (var range in RangesTable)
                {
                    if (range.Min <= range0.Pos && range.Max >= range0.Pos)
                    {
                        // Remember MultiUse
                        range0.MultiUse = range.MultiUse;
                        if (range.Scheme != null)
                        {
                            range0.Scheme = range.Scheme;
                            break;
                        }
                    }
                }
            }

            //RangesTable is redefined in DetermineNewRanges
            //Need to go from low to high m/z for our left consideration
            //What about discovered peaks!
            DetermineNewRanges(MinSortedRangesTable, peaks);
        }
        else
        {
            List<RangesTableEntries> MinSortedRangesTable = new List<RangesTableEntries>();
            foreach (var peak in peaks)
            {
                //Dummy entry...only .POS really matters
                IonFormula tempIonFormula = new IonFormula(Enumerable.Empty<IonFormula.Component>());
                IonTypeInfoRange tempIonTypeInforRange = new IonTypeInfoRange("Discovered", tempIonFormula, 0d, (double)peak.X - 0.05d, (double)peak.X + 0.05d, Colors.Black);
                RangesTableEntries dummy = new RangesTableEntries(tempIonTypeInforRange);
                dummy.Pos = peak.X;
                dummy.Scheme = null;
                foreach (var range in StartTable)
                {
                    if (range.Min <= peak.X && range.Max >= peak.X)
                    {
                        //dummy = range;
                        IonTypeInfoRange tempIonTypeInforRange2 = new IonTypeInfoRange(range.Name, range.Formula, range.Volume, (double)peak.X - 0.05d, (double)peak.X + 0.05d, range.Color);
                        RangesTableEntries dummy2 = new RangesTableEntries(tempIonTypeInforRange2);
                        dummy2.Pos = peak.X;
                        dummy2.Scheme = null;
                        dummy = dummy2;
                        break;
                    }
                }
                //dummy.Scheme will be null or have StartTable value, want RangesTable value
                //dummy has start table values, so dummy may not == range
                //note, if original ranges are huge (cover multiple peaks), then discovred peaks in tail will be ignored.  Need to fix
                foreach (var range in RangesTable)
                {
                    if (range.Min <= dummy.Pos && range.Max >= dummy.Pos && range.Scheme != null)
                    {
                        dummy.Scheme = range.Scheme;
                        break;
                    }
                }
                MinSortedRangesTable.Add(dummy);
            }
            MinSortedRangesTable = MinSortedRangesTable.OrderBy(o => o.Pos).ToList();

            //RangesTable is redefined in DetermineNewRanges
            //Need to go from low to high m/z for our left consideration
            DetermineNewRanges(MinSortedRangesTable, peaks);
        }

        bool tryResolveOverlapps = true;
        if (CheckForOverlappingRanges(tryResolveOverlapps)) return;

        AddAnyTailEstimates();

        IonicCompositionTable.Clear();
        CreateIonicCompositionTable();

        DecomposedCompositionTable.Clear();
        CreateDecomposedCompositionTable();

        // Show ranges
        var rangesDisplay = RangesTable
            .Select(r => (IChart2DSlice)(new Chart2DSlice((float)r.Min, (float)r.Max, r.Color)))
            .ToList();

        // Create the histogram data to be added to the chart in the view
        var histogramRenderData = Resources.ChartObjects
            .CreateHistogram(values?.Values, Colors.Black, verticalSlices: rangesDisplay, name: "Mass Spectrum");

        // Adding to the HistogramData ObservableCollection notifies the bound Chart2D in the view to update with the plot data
        HistogramData.Add(histogramRenderData);

        // Adding line segments for each range
        if (values is not null)
        {
            foreach (var range in RangesTable)
            {
                //Scale of line height depends on binning
                for (int i = 0; i < range.LineCoordinates.Length; i++)
                    range.LineCoordinates[i].Z *= values!.BinWidth;

                var histogramRenderData2 = Resources.ChartObjects.CreateLine(range.LineCoordinates, range.Color, 5f);
                HistogramData.Add(histogramRenderData2);
            }
        }

        //Keep view same as start of Rerange.  Apparently need a real slight change otherwise it defaults back to unzooomed?
        lower.Y -= 0.0001f;
        upper.Y += 0.0001f;
        Parameters.ViewportLower = lower;
        Parameters.ViewportUpper = upper;
    }

    // An ApplyRangeChangesCommand property is generated that can be used to bind this action to the view
    [RelayCommand]
    public async Task ApplyRangeChanges()
    {
        await Task.Yield();

        // Check that we have a valid RangeManager (an analysis set with no mass spectrum node will return a null range manager)
        if (Resources.RangeManager is not { } rangeManager)
        {
            MessageBox.Show(
                "This analysis requires a Mass Spectrum to be present to apply modified ranges",
                "Custom Analysis Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        //var existingRanges = rangeManager.GetIonRanges();
        IonTypeInfoRange[] ionRanges = new IonTypeInfoRange[RangesTable.Count()];

        int ii = 0;
        foreach (IonTypeInfoRange range in RangesTable)
            ionRanges[ii++] = range;

        Properties.UpdatePropertiesObservablesToParametersObservables(Parameters);
        Properties.UpdatePropertiesSchemesToProperties(RangesTable);

        // Set the ranges
        await Resources.RangeManager.SetIonRanges(ionRanges);
    }

    // A HelpCommand property is generated that can be used to bind this action to the view
    [RelayCommand]
    public async Task Help()
    {
        await Task.Yield();

        // This will resolve the path to where the extension is installed regardless of used in AP Suite/IVAS, etc.
        string extensionDirectory = new DirectoryInfo(Assembly.GetExecutingAssembly().Location).Parent!.FullName;

        // And then the full path to your PDF should be
        string absHelpPath = Path.Join(extensionDirectory, "CustomMassRanging2.1.pdf");

        var processStartInfo = new ProcessStartInfo()
        {
            FileName = absHelpPath,
            UseShellExecute = true,
        };
        Process.Start(processStartInfo);
    }

    // A ProcessMultisCommand property is generated that can be used to bind this action to the view
    [RelayCommand]
    public async Task ProcessMultis()
    {
        await Task.Yield();

        //Need to add some error code here
        //Guid InstanceId = Services.IdProvider.Get(this);
        //if (await Services.IonDataProvider.GetIonData(NodeId) is not { } ionData)
        if (await Services.IonDataProvider.GetOwnerIonData(Id) is not { } ionData)
            return;

        var sectionInfo = new HashSet<string>(ionData.Sections.Keys);
        if (!(IsIonDataValid(ionData)))
            return;

        ObservableCollection<RangesTableEntries> useRanges = new();
        foreach (var range in RangesTable)
            if (range.MultiUse) useRanges.Add(range);

        if (useRanges.Count == 0)
        {
            string errorString =  "At least one Multi checkbox must be selected\n";
                   errorString += "to perform multi-hit analysis.\n";

            MessageBox.Show(
                errorString,
                "Custom MultiHit Analysis Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        SepHistogramData.Clear();

        MultiHits multiHits = new(ionData, values!.Values, useRanges, RangesTable, Parameters);
        MultisInformation.Value = String.Empty;
        MultisInformation.Value = multiHits.MultisSummaryString(Parameters);

        // Create the sep histogram data to be added to the chart in the view
        int numPlots = 6;
        if (Parameters.EPlotsList.Equals(EPlots.MultisOnly) || Parameters.EPlotsList.Equals(EPlots.PseudosOnly)) numPlots = 3;
        if (Parameters.EPlotsList.Equals(EPlots.MultisOnly) || Parameters.EPlotsList.Equals(EPlots.PseudosOnly)) numPlots = (numPlots * 2) / 3;

        //Vector2[] sepPlot = new Vector2[MultiHits.NDistBins];
        savedPlots.Clear();
        savedLegends.Clear();

        //Find the end of the distribution for the first range
        //Use 1/2 that value to do the normalization
        //Can use for pseudos too
        int normMaxUncorrStart = 0;
        int[] uncorrSSInt = new int[useRanges.Count];
        int uncorrSSprimeInt = 0;
        if (Parameters.ESepPlotScaling.Equals(EScaling.MaxUncorr))
        {
            foreach (var range in useRanges)
            {
                //distanceCorrelations[range1][dp][type 0=all, 1=non-same-same, 2=same-same][NDISTBINS]
                for (int i = MultiHits.NDistBins - 1; i >= 0; i--)
                {
                    if (multiHits.dpDistanceCorrelations[0, 0, 0, i] > 0)
                    {
                        normMaxUncorrStart = i / 2;
                        break;
                    }
                }
                break;
            }
        }
        int j = 0;
        if (Parameters.EPlotsList.Equals(EPlots.Both) || Parameters.EPlotsList.Equals(EPlots.MultisOnly) ||
            Parameters.EPlotsList.Equals(EPlots.BothNotAll) || Parameters.EPlotsList.Equals(EPlots.MultisOnlyNotAll))
        {  
            //same-same
            j = 0;
            foreach (var range in useRanges)
            {
                Vector2[] sepPlot = new Vector2[MultiHits.NDistBins];
                //distanceCorrelations[range1][dp][type 0=all, 1=non-same-same, 2=same-same][NDISTBINS]
                if (normMaxUncorrStart > 0)
                {
                    uncorrSSInt[j] = 0;
                    for (int i = 0; i < normMaxUncorrStart; i++)
                    {
                        sepPlot[i].X = (float)(i * MultiHits.DistRes);
                        sepPlot[i].Y = multiHits.dpDistanceCorrelations[j, 0, 2, i];
                    }
                    for (int i = normMaxUncorrStart;i < MultiHits.NDistBins; i++)
                    {
                        sepPlot[i].X = (float)(i * MultiHits.DistRes);
                        sepPlot[i].Y = multiHits.dpDistanceCorrelations[j, 0, 2, i];
                        uncorrSSInt[j] += multiHits.dpDistanceCorrelations[j, 0, 2, i];
                    }
                }
                else
                {
                    for (int i = 0; i < MultiHits.NDistBins; i++)
                    {
                        sepPlot[i].X = (float)(i * MultiHits.DistRes);
                        sepPlot[i].Y = multiHits.dpDistanceCorrelations[j, 0, 2, i];
                    }
                }

                savedPlots.Add(sepPlot);
                savedLegends.Add($"{multiHits.rangeNames[j]}, dp=0, same-same");

                var histogramRenderData = Resources.ChartObjects
                    .CreateHistogram(sepPlot, lightColor[j % lightColor.Length], name: $"{multiHits.rangeNames[j]}, dp=0, same-same");

                SepHistogramData.Add(histogramRenderData);
                j++;
            }
            //non-same-same and normallize
            j = 0;
            foreach (var range in useRanges)
            {
                //Normalize by uncorrelated counts
                //A = Total of same-same, B = Total of non-same-same
                //Normalize non-same-same by (B+2A)^2/4N^2
                double normalize = 1d;
                int missing = 0;
                //Alternate normalization
                uncorrSSprimeInt = 0;
                if (normMaxUncorrStart > 0)
                {
                    for (int i = normMaxUncorrStart; i < MultiHits.NDistBins; i++)
                        uncorrSSprimeInt += multiHits.dpDistanceCorrelations[j, 0, 1, i];
                    normalize = (double)uncorrSSInt[j] / (double)uncorrSSprimeInt;
                }
                Normalize(multiHits, useRanges, j, 0, ref normalize, ref missing);

                Vector2[] sepPlot = new Vector2[MultiHits.NDistBins];
                //distanceCorrelations[range1][dp][type 0=all, 1=non-same-same, 2=same-same][NDISTBINS]
                for (int i = 0; i < MultiHits.NDistBins; i++)
                {
                    sepPlot[i].X = (float)(i * MultiHits.DistRes);
                    sepPlot[i].Y = (int)((double)multiHits.dpDistanceCorrelations[j, 0, 1, i] * normalize+0.49d);
                }

                savedPlots.Add(sepPlot);
                savedLegends.Add(((Parameters.ESepPlotScaling.Equals(EScaling.IntUncorr) || Parameters.ESepPlotScaling.Equals(EScaling.MaxUncorr)) ?
                    $"{multiHits.rangeNames[j]}, dp=0, non-same-same, missing: {(missing == 0 ? "NA" : missing)}" :
                    $"{multiHits.rangeNames[j]}, dp=0, non-same-same"));
                
                //Thicker line
                var histogramRenderData = Resources.ChartObjects
                    .CreateHistogram(sepPlot, color[j % color.Length], 2.0f, name:
                    ( (Parameters.ESepPlotScaling.Equals(EScaling.IntUncorr) || Parameters.ESepPlotScaling.Equals(EScaling.MaxUncorr)) ?
                    $"{multiHits.rangeNames[j]}, dp=0, non-same-same, missing: {(missing == 0 ? "NA" : missing)}" :
                    $"{multiHits.rangeNames[j]}, dp=0, non-same-same"));
                
                SepHistogramData.Add(histogramRenderData);
                j++;
            }
            //all
            j = 0;
            if (Parameters.EPlotsList.Equals(EPlots.Both) || Parameters.EPlotsList.Equals(EPlots.MultisOnly))
            {
                Vector2[] sepPlot = new Vector2[MultiHits.NDistBins];
                foreach (var range in useRanges)
                {
                    //distanceCorrelations[range1][dp][type 0=all, 1=non-same-same, 2=same-same][NDISTBINS]
                    for (int i = 0; i < MultiHits.NDistBins; i++)
                    {
                        sepPlot[i].X = (float)(i * MultiHits.DistRes);
                        sepPlot[i].Y = multiHits.dpDistanceCorrelations[j, 0, 0, i];
                    }

                    savedPlots.Add(sepPlot);
                    savedLegends.Add($"{multiHits.rangeNames[j]}, dp=0, all");

                    //Thicker line
                    var histogramRenderData = Resources.ChartObjects
                        .CreateHistogram(sepPlot, color[j % color.Length], 3.0f, name: $"{multiHits.rangeNames[j]}, dp=0, all");

                    SepHistogramData.Add(histogramRenderData);
                    j++;
                }
            }
        }

        if (Parameters.EPlotsList.Equals(EPlots.Both) || Parameters.EPlotsList.Equals(EPlots.PseudosOnly) ||
            Parameters.EPlotsList.Equals(EPlots.BothNotAll) || Parameters.EPlotsList.Equals(EPlots.PseudosOnlyNotAll))
        {
            //same-same
            j = 0;
            foreach (var range in useRanges)
            {
                Vector2[] sepPlot = new Vector2[MultiHits.NDistBins];
                //distanceCorrelations[range1][dp][type 0=all, 1=non-same-same, 2=same-same][NDISTBINS]
                if (normMaxUncorrStart > 0)
                {
                    uncorrSSInt[j] = 0;
                    for (int i = 0; i < normMaxUncorrStart; i++)
                    {
                        sepPlot[i].X = (float)(i * MultiHits.DistRes);
                        sepPlot[i].Y = multiHits.dpDistanceCorrelations[j, Parameters.IPseudoMultiMaxdp, 2, i];
                    }
                    for (int i = normMaxUncorrStart; i < MultiHits.NDistBins; i++)
                    {
                        sepPlot[i].X = (float)(i * MultiHits.DistRes);
                        sepPlot[i].Y = multiHits.dpDistanceCorrelations[j, Parameters.IPseudoMultiMaxdp, 2, i];
                        uncorrSSInt[j] += multiHits.dpDistanceCorrelations[j, Parameters.IPseudoMultiMaxdp, 2, i];
                    }
                }
                else
                {
                    for (int i = 0; i < MultiHits.NDistBins; i++)
                    {
                        sepPlot[i].X = (float)(i * MultiHits.DistRes);
                        sepPlot[i].Y = multiHits.dpDistanceCorrelations[j, Parameters.IPseudoMultiMaxdp, 2, i];
                    }
                }

                savedPlots.Add(sepPlot);
                savedLegends.Add($"{multiHits.rangeNames[j]}, dp>0, same-same");

                var histogramRenderData = Resources.ChartObjects
                    .CreateHistogram(sepPlot, lightColor[j % lightColor.Length], name: $"{multiHits.rangeNames[j]}, dp>0, same-same");

                SepHistogramData.Add(histogramRenderData);
                j++;
            }
            //non-same-same and normalize
            j = 0;
            foreach (var range in useRanges)
            {
                //Normalize by uncorrelated counts
                //A = Total of same-same, B = Total of non-same-same
                //Normalize non-same-same by (B+2A)^2/4N^2
                double normalize = 1d;
                int missing = 0;
                //Alternate normalization
                uncorrSSprimeInt = 0;
                if (normMaxUncorrStart > 0)
                {
                    for (int i = normMaxUncorrStart; i < MultiHits.NDistBins; i++)
                        uncorrSSprimeInt += multiHits.dpDistanceCorrelations[j, Parameters.IPseudoMultiMaxdp, 1, i];
                    normalize = (double)uncorrSSInt[j] / (double)uncorrSSprimeInt;
                }
                Normalize(multiHits, useRanges, j, Parameters.IPseudoMultiMaxdp, ref normalize, ref missing);

                Vector2[] sepPlot = new Vector2[MultiHits.NDistBins];
                //distanceCorrelations[range1][dp][type 0=all, 1=non-same-same, 2=same-same][NDISTBINS]
                for (int i = 0; i < MultiHits.NDistBins; i++)
                {
                    sepPlot[i].X = (float)(i * MultiHits.DistRes);
                    sepPlot[i].Y = (int)((double)multiHits.dpDistanceCorrelations[j, Parameters.IPseudoMultiMaxdp, 1, i] * normalize + 0.49d);
                }

                savedPlots.Add(sepPlot);
                savedLegends.Add(((Parameters.ESepPlotScaling.Equals(EScaling.IntUncorr) || Parameters.ESepPlotScaling.Equals(EScaling.MaxUncorr)) ?
                    $"{multiHits.rangeNames[j]}, dp>0, non-same-same, missing: {(missing == 0 ? "NA" : missing)}" :
                    $"{multiHits.rangeNames[j]}, dp>0, non-same-same"));

                //Thicker line
                var histogramRenderData = Resources.ChartObjects
                    .CreateHistogram(sepPlot, color[j % color.Length], 2.0f, name: 
                    ( (Parameters.ESepPlotScaling.Equals(EScaling.IntUncorr) || Parameters.ESepPlotScaling.Equals(EScaling.MaxUncorr)) ?
                    $"{multiHits.rangeNames[j]}, dp>0, non-same-same, missing: {(missing == 0 ? "NA" : missing)}" :
                    $"{multiHits.rangeNames[j]}, dp>0, non-same-same"));

                SepHistogramData.Add(histogramRenderData);
                j++;
            }
            //all
            j = 0;
            if (Parameters.EPlotsList.Equals(EPlots.Both) || Parameters.EPlotsList.Equals(EPlots.MultisOnly))
            {
                Vector2[] sepPlot = new Vector2[MultiHits.NDistBins];
                foreach (var range in useRanges)
                {
                    //distanceCorrelations[range1][dp][type 0=all, 1=non-same-same, 2=same-same][NDISTBINS]
                    for (int i = 0; i < MultiHits.NDistBins; i++)
                    {
                        sepPlot[i].X = (float)(i * MultiHits.DistRes);
                        sepPlot[i].Y = multiHits.dpDistanceCorrelations[j, Parameters.IPseudoMultiMaxdp, 0, i];
                    }

                    savedPlots.Add(sepPlot);
                    savedLegends.Add($"{multiHits.rangeNames[j]}, dp>0, all");

                    //Thicker line
                    var histogramRenderData = Resources.ChartObjects
                        .CreateHistogram(sepPlot, color[j % color.Length], 3.0f, name: $"{multiHits.rangeNames[j]}, dp>0, all");

                    SepHistogramData.Add(histogramRenderData);
                    j++;
                }
            }
        }
    }

    // A ProcessExportCommand property is generated that can be used to bind this action to the view
    [RelayCommand]
    public async Task ProcessExport()
    {
        await Task.Yield();
        CustomMassRangingExcel customMassRangingExcel = new CustomMassRangingExcel();
        customMassRangingExcel.SaveExcelFile(values, Parameters, RangesTable,
            IonicCompositionTable, IonicCompositionTotals, DecomposedCompositionTable, DecomposedCompositionTotals, MultisInformation.Value, savedPlots, savedLegends,
            saveFileName);
    }

    public void Normalize(MultiHits multiHits, ObservableCollection<RangesTableEntries> useRanges, int j, int dp, ref double normalize, ref int missing)
    {
        //Sep plots are already filtered selected or selected and other or all
        //Tables are tables and they are used here for normalization
        if (Parameters.ESepPlotScaling.Equals(EScaling.IntUncorr) || Parameters.ESepPlotScaling.Equals(EScaling.MaxUncorr))
        {
            int A = 0;
            int B = 0;
            int useRangesMax = useRanges.Count; // Selected
            if (Parameters.ESepPlots.Equals(EIons.SelectedAndOthers)) useRangesMax = useRanges.Count + 1;
            if (Parameters.ESepPlots.Equals(EIons.All)) useRangesMax = useRanges.Count + 2;
            A = multiHits.dpUncMultis[j, j, dp];
            int detected = multiHits.dpCorMultis[j, j, dp];
            int TotUnc = 0;
            int TotCor = 0;
            for (int k = 0; k < useRangesMax; k++)
            {
                TotUnc += multiHits.dpUncMultis[j, k, dp];
                TotUnc += multiHits.dpUncMultis[k, j, dp];
                TotCor += multiHits.dpCorMultis[j, k, dp];
                TotCor += multiHits.dpCorMultis[k, j, dp];
            }
            //2D table so counting A and detected twice
            B = TotUnc - A - A;

            //If MaxUncorr then normalize has already been computed, just determine missing from tables
            if (Parameters.ESepPlotScaling.Equals(EScaling.IntUncorr)) normalize = (double)A / (double)B;
            missing = (int)((normalize * (double)(TotCor - detected - detected)) - (double)detected + 0.49d);
            if (A >= 100 && A >= 100 && missing < 0)
            {
                missing = 0;
            }
            else if (A < 100 || B < 100)
            {
                normalize = 1d;
                missing = 0;
            }
        }
    }

    private bool CheckForOverlappingRanges(bool tryResolveOverlapps)
    {
        // Check for overlapps...
        bool overlappingRanges = false;
        string overlaps = "";
        for (int i = 0; i < RangesTable.Count(); i++)
        {
            var range0 = RangesTable.ElementAt(i);
            for (int j = i + 1; j < RangesTable.Count(); j++)
            {
                var range = RangesTable.ElementAt(j);
                if (!(range0.Min >= range.Max || range0.Max <= range.Min))
                {
                    if (tryResolveOverlapps)
                    {
                        if (range0.Name == range.Name) //Resolved when ion names are the same
                        {
                            if (range0.Net > range.Net) //Keep the biggest
                                RangesTable.RemoveAt(j--);
                            else
                            {
                                RangesTable.RemoveAt(i--);
                                break;
                            }
                        }
                        else if (range0.Name == "Discovered")
                        {
                            RangesTable.RemoveAt(i--);
                            break;
                        }
                        else if (range.Name == "Discovered")
                            RangesTable.RemoveAt(j--);
                        else
                        {
                            overlappingRanges = true;
                            overlaps += $"\n{range0.Name}: ({range0.Min:N3}, {range0.Max:N}) overlapps {range.Name}: ({range.Min:N3}, {range.Max:N3})";
                        }
                    }
                    else
                    {
                        overlappingRanges = true;
                        overlaps += $"\n{range0.Name}: ({range0.Min:N3}, {range0.Max:N}) overlapps {range.Name}: ({range.Min:N3}, {range.Max:N3})";
                    }
                }
            }
        }
        if (overlappingRanges)
        {
            MessageBox.Show(
                $"Original IVAS allows for overlapping ranges; however, range overlaps are not allowed in extensions. Please fix." +
                $"{overlaps}",
                "Custom Analysis Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return true;
        }
        return false;
    }

    private void AddAnyTailEstimates()
    {
        var MinSortedRangesTable = RangesTable.OrderBy(o => o.Pos).ToList();
        var tail = new List<Tuple<float, float>>();
        double tailConsider = Parameters.dConsideredTailRange;
        for (int i = 0; i < MinSortedRangesTable.Count(); i++)
        {
            tail.Clear();
            var range0 = MinSortedRangesTable.ElementAt(i);
            if (range0.Scheme == RangeScheme.LeftTail && values is not null)
            {
                //Start at Max of this range and add bin list for next 2 Da
                for (int j = 0; j < values.Values.Count(); j++)
                {
                    if (values.Values[j].X >= range0.Max && values.Values[j].X <= range0.Max + tailConsider)
                        tail.Add(new Tuple<float, float>(values.Values[j].X, values.Values[j].Y));
                }
                //Go through next ranges and remove items that are ranged
                for (int j = i + 1; j < MinSortedRangesTable.Count(); j++)
                {
                    var range = RangesTable.ElementAt(j);
                    if (range.Min < range0.Max + tailConsider)
                    {
                        foreach (var pair in tail.ToList())
                            if (pair.Item1 >= range.Min && pair.Item1 <= range.Max) tail.Remove(pair);
                    }
                    else //Left to right sorted, so no other ranges should need to be checked
                        break;
                }
                //Now do fitting routine
                float N = 0f;
                float SX = 0f;
                float SY = 0f;
                float SXX = 0f;
                float SXY = 0f;
                //float SYY = 0f;
                foreach (var pair in tail)
                {
                    N += 1f;
                    SX += (float)Math.Sqrt(pair.Item1);
                    SY += (float)Math.Log(pair.Item2);
                    SXX += pair.Item1; //Here sqrt becomes normal
                    SXY += (float)Math.Sqrt(pair.Item1) * (float)Math.Log(pair.Item2);
                    //SYY += (float)Math.Log(pair.Item2) * (float)Math.Log(pair.Item2);
                }
                float D = N * SXX - SX * SX;
                float a = 1 / D * (SXX * SY - SX * SXY);
                float b = 1 / D * (N * SXY - SX * SY);

                //We know the width of .Left scheme
                double bgd = range0.Bgd / (range0.Max - range0.Min) * values.BinWidth;
                double xMax = (Math.Log(bgd) - a) / b;
                xMax *= xMax; //Here because of sqrt

                if (b > 0f || xMax < range0.Max || xMax > range0.Max + Parameters.DTailRangeMaximum)
                {
                    MessageBox.Show(
                        $"Bad fit of exponential tail/slope. Setting ranging back to Left from LeftTail.\n" +
                        $"Exponent cannot be positive, proposed tail cannot be > rangeMax + TailRangeMaximum: {b:N3}, {xMax:N3}",
                        "Fit Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    foreach (var range in RangesTable)
                    {
                        if (range == range0)
                            range.Scheme = RangeScheme.Left;
                        return;
                    }
                }
                else
                {
                    //ln(y)=a+bx or y = exp(a+bx)
                    //now ln(y)=a+bsqrt(x) or y = exp(a+bsqrt(x))
                    //Add line for tail
                    Vector3[] newTail = new Vector3[20];
                    float delta = (float)(xMax - range0.Max) / 20.0f;
                    float start = (float)range0.Max;
                    for (int j = 0; j < 20; j++) //fixed 20 plot points
                    {
                        newTail[j].X = start;
                        newTail[j].Y = -1.0f; //Order of display
                        newTail[j].Z = (float)Math.Exp(a + b * Math.Sqrt(start));
                        start += delta;
                    }

                    //Plot on histogram
                    var histogramRenderDataTail = Resources.ChartObjects.CreateLine(newTail, Colors.Red, 8f);
                    HistogramData.Add(histogramRenderDataTail);

                    float tailTotal = 0.0f;
                    foreach (var range in RangesTable)
                    {
                        if (range == range0)
                        {
                            //Integrated counts for tail
                            for (float x = (float)range0.Max; x < xMax; x += Resolution)
                                tailTotal += (float)Math.Exp(a + b * Math.Sqrt(x)) - (float)bgd;
                            range.Tail = tailTotal;
                            range.Net += tailTotal;
                            range.BgdSigma2 += Parameters.DTailEstimateUncertainty * tailTotal * Parameters.DTailEstimateUncertainty * tailTotal;
                            break;
                        }
                    }
                }
            }
        }
    }

    private void AssessHistogramMakeValuesArray(IHistogramData massHist)
    {
        double maxIntensity = 0.0d;
        int maxPos = 0;
        for (int i = 0; i < massHist.Values.Length; i++)
        {
            if (massHist.Values.Span[i] > maxIntensity)
            {
                maxIntensity = massHist.Values.Span[i];
                maxPos = i;
            }
        }
        int leftFWHunM = 0;
        for (int i = maxPos; i >= 0; i--)
        {
            if (massHist.Values.Span[i] <= 0.01 * maxIntensity)
            {
                leftFWHunM = i;
                break;
            }
        }
        int rightFWHunM = massHist.Values.Length - 1;
        for (int i = maxPos; i < massHist.Values.Length; i++)
        {
            if (massHist.Values.Span[i] <= 0.01 * maxIntensity)
            {
                rightFWHunM = i;
                break;
            }
        }
        int Bins = (rightFWHunM - leftFWHunM);
        Properties.DMaxPeakPosition = (double)(massHist.Start + maxPos * massHist.BinWidth);
        Properties.DMaxPeakFWHunM = (double)(Bins) * massHist.BinWidth;
        Properties.DMaxPeakMRP = (double)((int)(10.0d * Properties.DMaxPeakPosition / Properties.DMaxPeakFWHunM)) / 10.0d;

        //Want main peak to span ~10-20 bins
        Properties.ISpectrumCoarsenFactor = 1;
        int newBins = Bins;
        while (newBins > 30)
        {
            Properties.ISpectrumCoarsenFactor++;
            newBins = Bins / Properties.ISpectrumCoarsenFactor;
        }

        int j = Properties.ISpectrumCoarsenFactor;
        double xShift = j > 1 ? ((double)j / 2.0d) : 0.0d;
        int valuesLength = massHist.Values.Length == 0 ? 0 : (massHist.Values.Length - 1) / j + 1;
        values = new MyRanging(valuesLength, (float)massHist.Start, (float)massHist.BinWidth * (float)j, (float)Properties.DMaxPeakPosition);
        for (int i = 0; i < massHist.Values.Length; i += j)
        {
            float tempTotal = 0;
            for (int k = 0; k < j && i + k < massHist.Values.Length; k++) tempTotal += (float)massHist.Values.Span[i + k];
            //values.Values[i / j].X = (float)(massHist.Start + ((double)i + xShift) * massHist.BinWidth);
            values.Values[i / j].X = (float)(massHist.Start + (double)i * massHist.BinWidth);
            values.Values[i / j].Y = tempTotal;
        }
        Resolution = (float)Properties.ISpectrumCoarsenFactor * (float)massHist.BinWidth;
    }

    private void StartCustomMassRangesTable()
    {
        // Check that we have a valid RangeManager (an analysis set with no mass spectrum node will return a null range manager)
        if (Resources.RangeManager is not { } rangeManager)
        {
            MessageBox.Show(
                "This analysis requires a Mass Spectrum to be present to apply modified ranges",
                "Custom Analysis Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        // Get the current ranges
        var currentRanges = rangeManager.GetIonRanges();

        // Add the current ranges to the table rows binding to be displayed in the view
        foreach (var range in currentRanges)
        {
            if (range.Name == "Discovered") continue; //Skip over any previously "Discovered" peaks
            RangesTableEntries rangesTableEntries = new(range);
            StartTable.Add(rangesTableEntries);
            RangesTable.Add(rangesTableEntries);
            RangesTable.Last().Pos = values?.FindLocalMax(range.Min, range.Max) ?? double.NaN;
            StartTable.Last().Pos = RangesTable.Last().Pos;
            StartTable.Last().Scheme = null;
            RangesTable.Last().Scheme = null;

            //Properties also may have a Saved Analysis Tree RangesTable.  Check to see if a scheme exists for any of these ranges
            //If so, then copy scheme into RangesTable--a non-null scheme is interpreted as a user selected scheme to override auto
            foreach (var range_item in Properties.RangesList)
            {
                if (range_item.Pos >= range.Min && range_item.Pos <= range.Max)
                {
                    StartTable.Last().Scheme = range_item.Scheme;
                    RangesTable.Last().Scheme = range_item.Scheme;
                    StartTable.Last().MultiUse = range_item.MultiUse;
                    RangesTable.Last().MultiUse = range_item.MultiUse;
                }
            }
        }

        // Find which range has bin with the most counts
        Properties.SMaxPeakName = "Not Ranged";
        foreach (var range in currentRanges)
        {
            if (range.Min <= Properties.DMaxPeakPosition && range.Max >= Properties.DMaxPeakPosition)
            {
                Properties.SMaxPeakName = range.Name;
                break;
            }
        }
    }

    private void CreateDecomposedCompositionTable()
    {
        //Note for error propogation.  Consider O + O2.  There is signal S and background B for each.
        //IVAS: O_Total = S_O + 2*S_O2.
        //      Sigma_O_Total^2= (S_O+2*B_O) + 2*(S_O2 + 2*B_O2) = S_O+2*S_O2 + 2*(B_O+2*B_O2).
        //      So just track the total net (or just total) and bgd seperately for error propogation
        //
        //Here: Sigma_O^2 = (S_O+2*B_O).  Sigma_2O2 = 2*sqrt(S_O2 + 2*B_O2) so Sigma_2O2^2 = 4*(S_O2+2*B_O2). 
        //      Sigma_O_Total^2 = (S_O+2*B_O) + 4*(S_O2+2*B_O2) 
        //      So tracking total net and bgd is insufficient.  Need the molecular piece too.

        foreach (var range in RangesTable)
        {
            foreach (var (key, value) in range.Formula)
            {
                CompositionTableEntries tableEntry = new(range);
                tableEntry.Name = key;
                tableEntry.Counts *= (double)value;
                tableEntry.Bgd *= (double)value;
                tableEntry.Net *= (double)value;
                tableEntry.Tail *= (double)value;
                //value*value (not just value) for decomposed error propogation
                tableEntry.BgdSigma2 *= (double)(value * value);

                bool match = false;
                foreach (var entry in DecomposedCompositionTable)
                {
                    if (entry == null)
                    {
                        DecomposedCompositionTable.Add(tableEntry);
                        match = true;
                        break;
                    }
                    else
                    {
                        if (entry.Name.Equals(key))
                        {
                            entry.AddToThisEntry(tableEntry);
                            match = true;
                            break;
                        }
                    }
                }
                if (!match) DecomposedCompositionTable.Add(tableEntry);
            }
        }

        //Go back through and compile compositions
        double Total_Counts = 0.0d;
        double Total_Bgd = 0.0d;
        double Total_Net = 0.0d;
        double Total_Tail = 0.0d;
        double Total_BgdSigma2 = 0.0d;
        foreach (var entry in DecomposedCompositionTable)
        {
            Total_Counts += entry.Counts;
            Total_Bgd += entry.Bgd;
            Total_Net += entry.Net;
            Total_Tail += entry.Tail;
            Total_BgdSigma2 += entry.BgdSigma2;
        }

        //Statistical Test for detection
        foreach (var entry in DecomposedCompositionTable)
        {
            //If not detected, then don't include in totals
            if (entry.Net < 2.33d * Math.Sqrt(entry.BgdSigma2)) //Not deteted at 95% CL
            {
                Total_Counts -= entry.Counts;
                Total_Bgd -= entry.Bgd;
                Total_Net -= entry.Net;
                Total_Tail -= entry.Tail;
                Total_BgdSigma2 -= entry.BgdSigma2;
                entry.DT = 4.65d * Math.Sqrt(entry.BgdSigma2);
            }
        }

        //Compute compositions and errors
        double Total_Composition = 0.0d;
        foreach (var entry in DecomposedCompositionTable)
        {
            if (entry.DT > 0.0d)
            {
                entry.Composition = -1.0d;
                entry.DT /= Total_Net;
            }
            else
            {
                entry.Composition = entry.Net / Total_Net;
                double Nc = Total_Net - entry.Net;
                double Bc = Total_Bgd - entry.Bgd;
                entry.Sigma = Math.Sqrt((entry.Net + entry.BgdSigma2) * (Nc - Bc) * (Nc - Bc) + (Nc + Bc) * (entry.Net - entry.BgdSigma2) * (entry.Net - entry.BgdSigma2))
                    / Total_Net / Total_Net;
                Total_Composition += entry.Composition;
            }
            CreateOutputString(entry);
        }
        //CompositionTableEntries DecomposedCompositionTotals = new(Total_Composition, Total_Counts, Total_Net, Total_Bgd, Total_Tail);
        DecomposedCompositionTotals.Composition = Total_Composition;
        DecomposedCompositionTotals.Counts = Total_Counts;
        DecomposedCompositionTotals.Net = Total_Net;
        DecomposedCompositionTotals.Bgd = Total_Bgd;
        DecomposedCompositionTotals.Tail = Total_Tail;
    }

    private void CreateOutputString(CompositionTableEntries entry)
    {
        if (entry.Composition < 0.0d)
        {
            entry.CompositionString = "-ND-";
            int precision = 1;
            while (Math.Pow(10, -precision) > entry.DT) precision++;
            switch (precision)
            {
                case 1:
                    entry.SigmaString = $"{entry.DT:P0}";
                    break;
                case 2:
                    entry.SigmaString = $"{entry.DT:P1}";
                    break;
                case 3:
                    entry.SigmaString = $"{entry.DT:P2}";
                    break;
                case 4:
                    entry.SigmaString = $"{entry.DT:P3}";
                    break;
                case 5:
                    entry.SigmaString = $"{entry.DT:P4}";
                    break;
                default:
                    entry.SigmaString = $"{entry.DT:P5}";
                    break;
            }
        }
        else
        {
            int precision = 1;
            while (Math.Pow(10, -precision) > entry.Sigma) precision++;
            switch (precision)
            {
                case 1:
                    entry.CompositionString = $"{entry.Composition:P0}";
                    entry.SigmaString = $"{entry.Sigma:P0}";
                    break;
                case 2:
                    entry.CompositionString = $"{entry.Composition:P1}";
                    entry.SigmaString = $"{entry.Sigma:P1}";
                    break;
                case 3:
                    entry.CompositionString = $"{entry.Composition:P2}";
                    entry.SigmaString = $"{entry.Sigma:P2}";
                    break;
                case 4:
                    entry.CompositionString = $"{entry.Composition:P3}";
                    entry.SigmaString = $"{entry.Sigma:P3}";
                    break;
                case 5:
                    entry.CompositionString = $"{entry.Composition:P4}";
                    entry.SigmaString = $"{entry.Sigma:P4}";
                    break;
                default:
                    entry.CompositionString = $"{entry.Composition:P5}";
                    entry.SigmaString = $"{entry.Sigma:P5}";
                    break;
            }
        }
    }

    private void CreateIonicCompositionTable()
    {
        foreach (var range in RangesTable)
        {
            CompositionTableEntries ionicCompositionTableEntry = new(range);
            bool match = false;
            foreach (var entry in IonicCompositionTable)
            {
                if (entry == null)
                {
                    IonicCompositionTable.Add(ionicCompositionTableEntry);
                    match = true;
                    break;
                }
                else
                {
                    if (entry.Name.Equals(ionicCompositionTableEntry.Name))
                    {
                        entry.AddToThisEntry(ionicCompositionTableEntry);
                        match = true;
                        break;
                    }
                }
            }
            if (!match) IonicCompositionTable.Add(ionicCompositionTableEntry);
        }

        //Go back through and compile compositions
        double Total_Counts = 0.0d;
        double Total_Bgd = 0.0d;
        double Total_Net = 0.0d;
        double Total_Tail = 0.0d;
        double Total_BgdSigma2 = 0.0d;
        foreach (var entry in IonicCompositionTable)
        {
            Total_Counts += entry.Counts;
            Total_Bgd += entry.Bgd;
            Total_Net += entry.Net;
            Total_Tail += entry.Tail;
            Total_BgdSigma2 += entry.BgdSigma2;
        }

        //Statistical Test for detection
        foreach (var entry in IonicCompositionTable)
        {
            if (entry.Net < 2.33d * Math.Sqrt(entry.BgdSigma2)) //Not deteted at 95% CL
            {
                Total_Counts -= entry.Counts;
                Total_Bgd -= entry.Bgd;
                Total_Net -= entry.Net;
                Total_Tail -= entry.Tail;
                Total_BgdSigma2 -= entry.BgdSigma2;
                entry.DT = 4.65d * Math.Sqrt(entry.BgdSigma2);
            }
        }

        //Compute compositions and errors
        double Total_Composition = 0.0d;
        foreach (var entry in IonicCompositionTable)
        {
            if (entry.DT > 0.0d)
            {
                entry.Composition = -1.0d;
                entry.DT /= Total_Net;
            }
            else
            {
                entry.Composition = entry.Net / Total_Net;
                double Nc = Total_Net - entry.Net;
                double Bc = Total_Bgd - entry.Bgd;
                entry.Sigma = Math.Sqrt((entry.Net + entry.BgdSigma2) * (Nc - Bc) * (Nc - Bc) + (Nc + Bc) * (entry.Net - entry.BgdSigma2) * (entry.Net - entry.BgdSigma2))
                    / Total_Net / Total_Net;
                Total_Composition += entry.Composition;
            }
            CreateOutputString(entry);
        }
        IonicCompositionTotals.Composition = Total_Composition;
        IonicCompositionTotals.Counts = Total_Counts;
        IonicCompositionTotals.Net = Total_Net;
        IonicCompositionTotals.Bgd = Total_Bgd;
        IonicCompositionTotals.Tail = Total_Tail;
    }

    private void DetermineNewRanges(List<RangesTableEntries> MinSortedRangesTable, List<Vector3> peaks)
    {
        var nList = MinSortedRangesTable.Count();

        //List is sorted by Pos
        double leftDistance = MinSortedRangesTable[0].Pos - values?.StartPos ?? double.NaN;
        double rightDistance = 0.0d;
        double leftNeighbor = 0.0d;
        double rightNeighbor = 0.0d;
        //If .Scheme is !null then evaluate, otherwise leave alone
        //GetNearestDiscoveredPeaks takes into account discovered peaks for scheme determination
        if (nList > 1)
        {
            //First item
            if (GetNearestDiscoveredPeaks(peaks, ref leftNeighbor, MinSortedRangesTable[0].Pos, ref rightNeighbor))
            {
                leftDistance = MinSortedRangesTable[0].Pos - leftNeighbor;
                rightDistance = rightNeighbor - MinSortedRangesTable[0].Pos;
                if (MinSortedRangesTable[0].Scheme == null)
                    MinSortedRangesTable[0].Scheme = Scheme.DetermineRangeScheme(leftDistance, rightDistance, Parameters.dLeftRangeCriteria);
            }
            else
            {
                rightDistance = MinSortedRangesTable[1].Pos - MinSortedRangesTable[0].Pos;
                if (MinSortedRangesTable[0].Scheme == null)
                    MinSortedRangesTable[0].Scheme = Scheme.DetermineRangeScheme(leftDistance, rightDistance, Parameters.dLeftRangeCriteria);
            }

            //Last item
            if (GetNearestDiscoveredPeaks(peaks, ref leftNeighbor, MinSortedRangesTable[nList - 1].Pos, ref rightNeighbor))
            {
                leftDistance = MinSortedRangesTable[nList - 1].Pos - leftNeighbor;
                rightDistance = rightNeighbor - MinSortedRangesTable[nList - 1].Pos;
                if (MinSortedRangesTable[nList - 1].Scheme == null)
                    MinSortedRangesTable[nList - 1].Scheme = Scheme.DetermineRangeScheme(leftDistance, rightDistance, Parameters.dLeftRangeCriteria);
            }
            else
            {
                leftDistance = MinSortedRangesTable[nList - 1].Pos - MinSortedRangesTable[nList - 2].Pos;
                rightDistance = values?.Values.Last().X ?? double.NaN - MinSortedRangesTable[nList - 1].Pos;
                if (MinSortedRangesTable[nList - 1].Scheme == null)
                    MinSortedRangesTable[nList - 1].Scheme = Scheme.DetermineRangeScheme(leftDistance, rightDistance, Parameters.dLeftRangeCriteria);
            }

        }

        for (int i = 1; i < nList - 1; i++)
        {
            if (GetNearestDiscoveredPeaks(peaks, ref leftNeighbor, MinSortedRangesTable[nList - 1].Pos, ref rightNeighbor))
            {
                leftDistance = MinSortedRangesTable[i].Pos - leftNeighbor; ;
                rightDistance = rightNeighbor - MinSortedRangesTable[i].Pos;
                if (MinSortedRangesTable[i].Scheme == null)
                    MinSortedRangesTable[i].Scheme = Scheme.DetermineRangeScheme(leftDistance, rightDistance, Parameters.dLeftRangeCriteria);
            }
            else
            {
                leftDistance = MinSortedRangesTable[i].Pos - MinSortedRangesTable[i - 1].Pos; ;
                rightDistance = MinSortedRangesTable[i + 1].Pos - MinSortedRangesTable[i].Pos;
                if (MinSortedRangesTable[i].Scheme == null)
                    MinSortedRangesTable[i].Scheme = Scheme.DetermineRangeScheme(leftDistance, rightDistance, Parameters.dLeftRangeCriteria);
            }
        }

        RangesTable.Clear();
        for (int i = 0; i < nList; i++)
        {
            float left = (float)MinSortedRangesTable[i].Min;
            float right = (float)MinSortedRangesTable[i].Max;
            double netMax = 0.0d;
            double raw = 0.0d;
            double leftBgd = 0.0d;
            double rightBgd = 0.0d;
            values?.DetermineRange((float)MinSortedRangesTable[i].Pos, MinSortedRangesTable[i].Scheme, Parameters, ref left, ref right, ref netMax, ref raw, ref leftBgd, ref rightBgd);

            RangesTableEntries newSortedRangesTable = new(MinSortedRangesTable[i].Name, MinSortedRangesTable[i].MultiUse, MinSortedRangesTable[i].Pos, netMax, raw, MinSortedRangesTable[i].Formula,
                MinSortedRangesTable[i].Volume, (double)left, (double)right, MinSortedRangesTable[i].Scheme, MinSortedRangesTable[i].Color, leftBgd, rightBgd, Parameters.dLeftRangeDelta);

            RangesTable.Add(newSortedRangesTable);
        }
    }

    private bool GetNearestDiscoveredPeaks(List<Vector3> peaks, ref double left, double current, ref double right)
    {
        //Assume in order, so first to be greater or equal
        //Also, assume pos (peak value) should be exactly the same
        int j = peaks.Count();
        for (int i = 0; i < j; i++)
        {
            if (peaks[i].X >= (float)current)
            {
                switch (i, j)
                {
                    //first
                    case (0, > 1):
                        left = (double)values!.Values[0].X;
                        right = (double)peaks[1].X;
                        return true;
                    //break;
                    //last
                    case ( > 0, > 0) when i == j - 1:
                        left = (double)peaks[i - 1].X;
                        right = (double)values!.Values.Last().X;
                        return true;
                    //break;
                    default:
                        left = (double)peaks[i - 1].X;
                        right = (double)peaks[i + 1].X;
                        return true;
                        //break;
                }
            }
        }
        return false;
    }

    private bool IsIonDataValid(IIonData ionData)
    {
        var sectionInfo = new HashSet<string>(ionData.Sections.Keys);
        string[] sections = { "pulse", "pulseDelta", "Mass", "Voltage", "Epos ToF", "Position", "Detector Coordinates" };
        string errorString = "";
        foreach (var section in sections)
            if (!sectionInfo.Contains(section))
            {
                errorString += "Note: Compatibility for ROI multi-hit processing\n";
                errorString += "requires additional non-epos sections pulse and pulseDelta.\n";
                errorString += $"Missing section: {section}\n";
            }

        if (errorString != "")
        {
            MessageBox.Show(
                errorString,
                "Custom MultiHit Analysis Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }

        return true;
    }
}
public class Scheme
{
    public static RangeScheme DetermineRangeScheme(double leftDistance, double rightDistance, double criteria)
    {
        if (leftDistance >= criteria) return RangeScheme.Left;
        else if (leftDistance >= 0.9d && rightDistance >= 0.9d) return RangeScheme.Half;
        else return RangeScheme.Quarter;
    }
}

[TypeConverter(typeof(EnumBindingSourceExtension))]
public enum RangeScheme
{
    [EnumMember(Value = "Left")]
    Left,
    [EnumMember(Value = "LeftTail")]
    LeftTail,
    [EnumMember(Value = "Half")]
    Half,
    [EnumMember(Value = "Quarter")]
    Quarter,
}

public partial class MyViewableString : ObservableObject
{
    private string value = string.Empty;
    public string Value
    {
        get => value;
        set => SetProperty(ref this.value, value);
    }
}