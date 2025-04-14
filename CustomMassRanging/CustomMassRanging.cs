using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System;
using System.Collections.Generic; //IEnumerable<>
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows;
using System.ComponentModel;
using System.Xml.Linq;
using System.Resources;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Reflection;
using System.Windows.Documents;
using System.Reflection.Emit;
using System.Windows.Media.Media3D;
using System.Collections;

/// CustomMassRanging
/// 
/// Update:
///     Start with ranges as defined in ROI
///     Confirm they are non-overlapping
///     Find max histogram bin, determine FWHunM (FWThM was used in paper)
///     Use FWHunM to determine binning for display and ranging
///     Display
/// 
/// Allow user to modify default parameters
///
/// Rerange:
///     Following parameter values, determine new range min, max
///     Beginning implimation is overly simple.  Any prospective "left" is implimented as 1/2-ranges
///     Check again for non-overlapping results
/// 
/// Apply:
///     Move values back to IVAS
///     This change will trigger reloading (can we change this?)
///     
/// Saving...
///     At start, "Properties" for this extension are either
///         1) default from this extension, or
///         2) values from saved analysis state
///     Any change in "Properties" triggers a need to "Update" from beginning
///     Any changes to "Parameters" are copied to "Properties" upon Apply
///     
/// Next (April 14, 2025):
///     Allow user modificaiton of type of range
///     Calculate background corrected bulk composions with errors (ionic and non-ionic)
///     Can we do mass fraction?
///     Modify "Update" so that it doesn't restart everyting 
///     (easier to just make a new CustomAnalsis if that is desired)
///     

namespace CustomMassRanging;

[DefaultView(CustomMassRangingViewModel.UniqueId, typeof(CustomMassRangingViewModel))]
internal partial class CustomMassRanging : BasicCustomAnalysisBase<CustomMassRangingProperties>
{
    public const string UniqueId = "CustomMassRanging.CustomMassRanging";
    public static INodeDisplayInfo DisplayInfo { get; } = new NodeDisplayInfo("Custom Mass Ranging");
    public ObservableCollection<IRenderData> HistogramData { get; } = new();

    //RangesTable is the local copy of ranges and results
    public ObservableCollection<DisplayRangesTable> RangesTable { get; } = new();

    //values is the class that does all the processing of the coarsened mass spectrum
    public MyCustomRanging? values;

    // Bound to the PropertyGrid
    public Parameters Parameters { get; } = new();

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
        // if (e.PropertyName == nameof(Parameters.Property1))
        // or similar can be used to identify which property was changed to only call code for certain parameters

        // I will very often use a switch statement to run different code depending on what property changed like this
        /*
        switch (e.PropertyName)
        {
            case nameof(Parameters.Parameter1):
                // Do something when Parameter1 changes
                break;
            default:
                break;
        }
        */
    }

    // The main update method
    protected override async Task<bool> Update(CancellationToken cancellationToken)
    {
        await Task.Yield();

        // Clear existing view data
        HistogramData.Clear();
        RangesTable.Clear();

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

        // Get mass spectrum data
        var massHist = Resources.GetMassSpectrum()?.GetData<IMassSpectrumData>()?.MassHistogram;

        // Use the ViewBuilder to display a chart, in this case
        if (massHist is not null)
        {
            //Properties Table and values are filled via AssessHistogram
            AssessHistogramMakeValuesArray(massHist);

            //Check to see if histogram returned values for ranging
            if (values is null)
            {
                MessageBox.Show(
                    "No values found.",
                    "Custom Analysis Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            // Get the current ranges
            var currentRanges = rangeManager.GetIonRanges();

            // Check for overlapps...
            for (int i = 0; i < currentRanges.Count(); i++)
            {
                var range0 = currentRanges.ElementAt(i);
                for (int j=i+1; j < currentRanges.Count(); j++)
                {
                    var range = currentRanges.ElementAt(j);
                    // No overlap
                    if ( !(range0.Min > range.Max || range0.Max < range.Min) )
                    {
                        MessageBox.Show(
                            $"Range overlaps not allowed. ({range0.Min}, {range0.Max}) overlapps ({range.Min}, {range.Max}). Adjust/delete offending ranges and try again.",
                            "Custom Analysis Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return false;
                    }
                }
            }

            // Show current ranges
            var rangesDisplay = rangeManager.GetIonRanges()
                .Select(r => (IChart2DSlice)(new Chart2DSlice((float)r.Min, (float)r.Max, r.Color)))
                .ToList();

            /*
            // Get the current ranges
            var currentRanges = rangeManager.GetIonRanges()
                .Select(x => new DisplayRangesTable 
                {
                    Name = x.Name,
                    Volume = x.Volume,
                    Min = x.Min,
                    Max = x.Max,
                });
            */

            // Create the histogram data to be added to the chart in the view
            var histogramRenderData = Resources.ChartObjects
                .CreateHistogram(values.Values, Colors.Black, verticalSlices: rangesDisplay, name: "Mass Spectrum");
            
            // Adding to the HistogramData ObservableCollection notifies the bound Chart2D in the view to update with the plot data
            HistogramData.Add(histogramRenderData);
        }

        //Here a copy of the current ranges is made as a starting point for RangesTable
        StartCustomMassRangesTable(rangeManager);

        //Properties is an enherited observable from CustomMassRangignProperties
        Parameters.ResetParametersObservablesToPropertiesObservables(Properties); 

        // Return true as the update completed successfully and the data state of the analysis should be considered valid
        return true;
    }

    // A RerangeCommand property is generated that can be used to bind this action to the view
    [RelayCommand]
    public async Task Rerange()
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

        var MinSortedRangesTable = RangesTable.OrderBy(o => o.Pos).ToList();

        // Check that there is at least 1 existing range
        if (MinSortedRangesTable.Count() < 1)
        {
            MessageBox.Show(
                "No ranges defined in original node.  Current implimentation modifies _existing_ range definitions.",
                "Custom Analysis Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        DetermineNewRanges(MinSortedRangesTable);

        // Show ranges
        var rangesDisplay = RangesTable
            .Select(r => (IChart2DSlice)(new Chart2DSlice((float)r.Min, (float)r.Max, r.Color)))
            .ToList();

        HistogramData.Clear();

        // Create the histogram data to be added to the chart in the view
        var histogramRenderData = Resources.ChartObjects
            .CreateHistogram(values?.Values, Colors.Black, verticalSlices: rangesDisplay, name: "Mass Spectrum");

        // Adding to the HistogramData ObservableCollection notifies the bound Chart2D in the view to update with the plot data
        HistogramData.Add(histogramRenderData);
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

        var existingRanges = rangeManager.GetIonRanges();
        IonTypeInfoRange[] ionRanges = new IonTypeInfoRange[existingRanges.Count()];

        int ii = 0;
        foreach (IonTypeInfoRange range in RangesTable)
            ionRanges[ii++] = range;

        Properties.UpdatePropertiesObservablesToParametersObservables(Parameters);

        // Set the ranges
        await Resources.RangeManager.SetIonRanges(ionRanges);
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
        double xShift = j > 1 ? (j / 2.0d) : 0.0d;
        int valuesLength = massHist.Values.Length == 0 ? 0 : (massHist.Values.Length - 1) / j + 1;
        values = new MyCustomRanging(valuesLength, (float)massHist.Start, (float)massHist.BinWidth * (float)j);
        for (int i = 0; i < massHist.Values.Length; i += j)
        {
            float tempTotal = 0;
            for (int k = 0; k < j && i + k < massHist.Values.Length; k++) tempTotal += (float)massHist.Values.Span[i + k];
            values.Values[i / j].X = (float)(massHist.Start + ((double)i + xShift) * massHist.BinWidth);
            values.Values[i / j].Y = tempTotal;
        }

    }

    private void StartCustomMassRangesTable(IMassSpectrumRangeManager rangeManager)
    {
        // Get the current ranges
        var currentRanges = rangeManager.GetIonRanges();

        // Add the current ranges to the table rows binding to be displayed in the view
        foreach (var range in currentRanges)
        {
            DisplayRangesTable displayRangesTable = new(range);
            RangesTable.Add(displayRangesTable);
            RangesTable.Last().Pos = values?.FindLocalMax(range.Min, range.Max) ?? double.NaN;
            RangesTable.Last().Scheme = null;
        }

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

    private void DetermineNewRanges(List<DisplayRangesTable> MinSortedRangesTable)
    {
        var nList = MinSortedRangesTable.Count();

        //List is sorted by Pos
        double leftDistance = MinSortedRangesTable[0].Pos - values?.StartPos ?? double.NaN;
        double rightDistance = 0.0d;
        if (nList > 1)
        {
            rightDistance = MinSortedRangesTable[1].Pos - MinSortedRangesTable[0].Pos;
            MinSortedRangesTable[0].Scheme = Scheme.DetermineRangeScheme(leftDistance, rightDistance, Parameters.dLeftRangeCriteria);

            leftDistance = MinSortedRangesTable[nList - 1].Pos - MinSortedRangesTable[nList - 2].Pos; ;
            rightDistance = values?.Values.Last().X ?? double.NaN - MinSortedRangesTable[nList - 1].Pos;
            MinSortedRangesTable[nList - 1].Scheme = Scheme.DetermineRangeScheme(leftDistance, rightDistance, Parameters.dLeftRangeCriteria);
        }

        for (int i = 1; i < nList - 1; i++)
        {
            leftDistance = MinSortedRangesTable[i].Pos - MinSortedRangesTable[i - 1].Pos; ;
            rightDistance = MinSortedRangesTable[i + 1].Pos - MinSortedRangesTable[i].Pos;
            MinSortedRangesTable[i].Scheme = Scheme.DetermineRangeScheme(leftDistance, rightDistance, Parameters.dLeftRangeCriteria);
        }

        RangesTable.Clear();
        for (int i = 0; i < nList; i++)
        {
            float left = (float)MinSortedRangesTable[i].Min;
            float right = (float)MinSortedRangesTable[i].Max;
            double netMax = 0.0d;
            double raw = 0.0d; ;
            values?.DetermineRange((float)MinSortedRangesTable[i].Pos, MinSortedRangesTable[i].Scheme, Parameters, ref left, ref right, ref netMax, ref raw);
            DisplayRangesTable newSortedRangesTable = new(MinSortedRangesTable[i].Name, MinSortedRangesTable[i].Pos, netMax, raw, MinSortedRangesTable[i].Formula,
                MinSortedRangesTable[i].Volume, (double)left, (double)right, MinSortedRangesTable[i].Scheme, MinSortedRangesTable[i].Color);

            RangesTable.Add(newSortedRangesTable);
        }
    }
}

public class DisplayVariablesTable
{
    [Display(Name = "Variable", AutoGenerateField = true, Description = "Variable", GroupName = "Parameters")]
    public string Name { get; set; }

    [Display(Name = "Value", AutoGenerateField = true, Description = "Value", GroupName = "Parameters")]
    public string Value { get; set; }
    
    public DisplayVariablesTable(string name, string value)
    {
        Name = name;
        Value = value;
    }
    public DisplayVariablesTable(DisplayVariablesTable variable)
    {
        Name = variable.Name;
        Value = variable.Value;
    }
}

public class DisplayRangesTable : IonTypeInfoRange
{
    // Name -> Column name
    // AutoGenerateField -> If 'false', then this public property will not have a generated column in the table (default is true)
    // Description -> Tool tip text if hovering over the column
    // GroupName -> I don't use this often, but can be used group columns
    // DataFormatString -> Formats the numeric value.
    // See https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings or
    // https://learn.microsoft.com/en-us/dotnet/standard/base-types/custom-numeric-format-strings
    // https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/how-to-add-tables-and-columns-to-the-windows-forms-datagrid-control?view=netframeworkdesktop-4.8

    [Display(Name = "Ion", AutoGenerateField = true, Description = "Ion Name", GroupName = "Ranges")]
    public new string Name { get => base.Name; }

    [Display(Name = "Peak (Da)", AutoGenerateField = true, Description = "Peak Position (Da)", GroupName = "Ranges")]
    [DisplayFormat(DataFormatString = "n3")]
    public double Pos { get; set; } = 0.0d;

    public new IonFormula Formula { get => base.Formula; }

    [Display(AutoGenerateField = false)]
    public double Net { get; set; } = 0.0d;
    [Display(AutoGenerateField = false)]
    public double Counts { get; set; } = 0.0d;

    [Display(AutoGenerateField = false)]
    public new double Volume { get => base.Volume; }

    [Display(Name = "Min (Da)", AutoGenerateField = true, Description = "Left Range Edge", GroupName = "Ranges")]
    [DisplayFormat(DataFormatString = "n3")]
    public new double Min { get => base.Min; }

    [Display(Name = "Max (Da)", AutoGenerateField = true, Description = "Right Range Edge", GroupName = "Ranges")]
    [DisplayFormat(DataFormatString = "n3")]
    public new double Max { get => base.Max; }

    [Display(Name = "Scheme", AutoGenerateField = true, Description = "Right Range Edge", GroupName = "Ranges")]
    public Scheme.RangeScheme? Scheme { get; set; } = null;

    //[Display(Name = "Color", AutoGenerateField = true, Description = "Display Color", GroupName = "Ranges")]
    [Display(AutoGenerateField = false)]
    public new System.Windows.Media.Color Color { get => base.Color; }

    public DisplayRangesTable(string name, double pos, double net, double counts, IonFormula ionFormula, double volume, double min, double max, Scheme.RangeScheme? scheme, System.Windows.Media.Color color) 
        : base(name, ionFormula, volume, min, max, color)
    {
        Pos = pos;
        Net = net;
        Counts = counts;
        Scheme = scheme;
    }
    public DisplayRangesTable(IonTypeInfoRange ionTypeInfoRange)
    : base(ionTypeInfoRange.Name, ionTypeInfoRange.Formula, ionTypeInfoRange.Volume, ionTypeInfoRange.Min, ionTypeInfoRange.Max, ionTypeInfoRange.Color)
    {
    }
}

public class Scheme
{
    public enum RangeScheme
    {
        Left,
        Half,
        Quarter
    }

    public static RangeScheme DetermineRangeScheme(double leftDistance, double rightDistance, double criteria)
    {
        if (leftDistance >= criteria) return RangeScheme.Left;
        else if (leftDistance >= 0.9d && rightDistance >= 0.9d) return RangeScheme.Half;
        else return RangeScheme.Quarter;
    }
}