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

namespace CustomMassRanging;

[DefaultView(CustomMassRangingViewModel.UniqueId, typeof(CustomMassRangingViewModel))]
internal partial class CustomMassRanging : BasicCustomAnalysisBase<CustomMassRangingProperties>
{
    public const string UniqueId = "CustomMassRanging.CustomMassRanging";
    public static INodeDisplayInfo DisplayInfo { get; } = new NodeDisplayInfo("Custom Mass Ranging");
    
    public ObservableCollection<IRenderData> HistogramData { get; } = new();
    public ObservableCollection<DisplayRangesTable> RangesTable { get; } = new();
    public MyValues2? values;
    public List<IonTypeInfoRange> newRanges = new List<IonTypeInfoRange>();
    //public double minFraction = 0.01;

    // Bound to the PropertyGrid
    public Parameters Parameters { get; } = new();

    // Add a line to the constructor to wire up the event handler to the Properties object PropertyChanged event
    public CustomMassRanging(IStandardAnalysisFilterNodeBaseServices services, ResourceFactory resourceFactory)
    : base(services, resourceFactory)
    {
        //Changed for first implimentataion to allow for PropertyChanged (otherwise nothing here)
        Parameters.PropertyChanged += OnParametersChanged;
    }

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
        if (Resources.RangeManager is not { } rangeManger)
        {
            return false;
        }

        // Example of getting the mass spectrum data
        var massHist = Resources.GetMassSpectrum()?.GetData<IMassSpectrumData>()?.MassHistogram;

        // Use the ViewBuilder to display a chart, in this cas
        if (massHist is not null)
        {
            double maxIntensity = 0.0d;
            int maxPos = 0;
            for (int i = 0; i < massHist.Values.Length; i++)
            {
                if (massHist.Values.Span[i]>maxIntensity)
                {
                    maxIntensity = massHist.Values.Span[i];
                    maxPos = i;
                }
            }
            int leftFWHM = 0;
            for (int i = maxPos; i >=0; i--)
            {
                if (massHist.Values.Span[i] <= 0.5 * maxIntensity)
                {
                    leftFWHM = i;
                    break;
                }
            }
            int rightFWHM = massHist.Values.Length-1;
            for (int i = maxPos; i < massHist.Values.Length; i++)
            {
                if (massHist.Values.Span[i] <= 0.5 * maxIntensity)
                {
                    rightFWHM = i;
                    break;
                }
            }
            int Bins = (rightFWHM - leftFWHM);
            Properties.DMaxPeakPosition = (double)(massHist.Start + maxPos * massHist.BinWidth);
            Properties.DMaxPeakFWHM = (double)(Bins) * massHist.BinWidth;
            Properties.DMaxPeakMRP = Properties.DMaxPeakPosition / Properties.DMaxPeakFWHM;
            //Want main peak to span 10-20 bins
            Properties.ISpectrumCoarsenFactor = 1;
            int newBins = Bins;
            while (newBins>20)
            {
                Properties.ISpectrumCoarsenFactor++;
                newBins = Bins/Properties.ISpectrumCoarsenFactor;
            }
           
            int j = Properties.ISpectrumCoarsenFactor; 
            double xShift = j > 1 ? (j / 2.0d) : 0.0d;
            int valuesLength = massHist.Values.Length == 0 ? 0 : (massHist.Values.Length-1) / j + 1;
            values = new MyValues2(valuesLength, (float)massHist.Start, (float)massHist.BinWidth*(float)j );
            for (int i = 0; i < massHist.Values.Length; i+=j)
            {
                float tempTotal = 0;
                for (int k = 0; k < j && i + k < massHist.Values.Length; k++) tempTotal += (float)massHist.Values.Span[i + k];
                values.Values[i/j].X = (float)(massHist.Start + ((double)i + xShift) * massHist.BinWidth);
                values.Values[i/j].Y = tempTotal;
            }

            // Get the current ranges
            var currentRanges = rangeManger.GetIonRanges();

            // Add the current ranges to the table rows binding to be displayed in the view
            foreach (var range in currentRanges)
            {
                DisplayRangesTable displayRangesTable = new(range);
                RangesTable.Add(displayRangesTable);
                RangesTable.Last().Pos = values.FindLocalMax(range.Min, range.Max);
            }

            // Show ranges
            var rangesDisplay = rangeManger.GetIonRanges()
                .Select(r => (IChart2DSlice)(new Chart2DSlice((float)r.Min, (float)r.Max, r.Color)))
                .ToList();

            Properties.SMaxPeakName = "Not Ranged";
            foreach (var range in currentRanges)
            {
                if (range.Min <= Properties.DMaxPeakPosition && range.Max >= Properties.DMaxPeakPosition)
                {
                    Properties.SMaxPeakName = range.Name;
                    break;
                }
            }

            // Create the histogram data to be added to the chart in the view
            var histogramRenderData = Resources.ChartObjects
                .CreateHistogram(values.Values, Colors.Black, verticalSlices: rangesDisplay, name: "Mass Spectrum");
            // Adding to the HistogramData ObservableCollection notifies the bound Chart2D in the view to update with the plot data
            HistogramData.Add(histogramRenderData);
        }

        Parameters.ResetParametersObservablesToPropertiesObservables(Properties); //Properties is an enherited observable from CustomMassRangignProperties

        // Return true as the update completed successfully and the data state of the analysis should be considered valid
        return true;
    }

    // An ApplyRangeChangesCommand property is generated that can be used to bind this action to the view
    [RelayCommand]
    public async Task ApplyRangeChanges()
    {
        await Task.Yield();

        // Check that we have a valid RangeManager (an analysis set with no mass spectrum node will return a null range manager)
        if (Resources.RangeManager is not { } rangeManger)
        {
            MessageBox.Show(
                "This analysis requires a Mass Spectrum to be present to apply modified ranges",
                "Custom Analysis Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }
        
        /*
        var MinSortedRangesTable = RangesTable.OrderBy(o => o.Min).ToList();

        var first = MinSortedRangesTable.FirstOrDefault();
        var last = MinSortedRangesTable.LastOrDefault();
        List<RangeScheme> schemes = new List<RangeScheme>();
        int rangesCount = MinSortedRangesTable.Count();
        if (rangesCount == 1)
            schemes.Add(RangeScheme.Left);
        else if (rangesCount == 2)
        {
            schemes.Add(RangeScheme.Left);
            var diff = MinSortedRangesTable[1].Min - MinSortedRangesTable[0].Min;
            if ( diff > 5.0d )
                schemes.Add(RangeScheme.Left);
            else if ( diff > 0.9d )
                schemes.Add(RangeScheme.Half);
            else
                schemes.Add(RangeScheme.Quarter);
        }
        else
        {

        }

        foreach (var range in MinSortedRangesTable)
        {
            //Find max pos and max bin for each (use to determine nearest neighbor)
            //If no peak to the left for LeftDa, then use left_range - 0
            //If no peak within +-0.9 Da, then half_range - 1
            //Otherwise quarter_range - 2
            //Could do this before apply and list them with ranges...
            if (range.Equals(first))
            {
                var previous = range;
                schemes.Add(RangeScheme.Left);
            }
            else if (range.Equals(last))
            {

            }
            else
            {

            }
        }
        */

        /*
        // Example of adding some ranges
        if (Resources.ElementData?.Elements is { } elements)
        {
            List<string> ionList = new List<string>() { "Si", "SiO2", "Unknown" };
            foreach (var ion in ionList)
            {
                var newRange = CreateRanges(ion);
                foreach (var range in newRange) { newRanges.Add(range); };
            }

            // Set the ranges
            await Resources.RangeManager.SetIonRanges(newRanges);
        }
        */
    }

    /*
    private List<IonTypeInfoRange> CreateRanges(string name)
    {
        var elements = Resources.ElementData?.Elements ?? throw new InvalidOperationException("Could not resolve element data");
        var formula = (IonFormulaEx.TryParse(name, out IonFormula parsedFormula) ? parsedFormula : IonFormula.Unknown);
        var isotopes = Resources.FormulaIsotopeCalculator.GetIsotopes(formula, new IonFormulaIsotopeOptions { ElementDataSetId = Resources.ElementData.Id });
        isotopes.DefaultIfEmpty();
        List<IonTypeInfoRange> returnRanges = new List<IonTypeInfoRange>();

        double volume = 0;
        foreach (var element in formula)
        {
            var valueOrDefault = (Resources.ElementData?.Elements.FirstOrDefault((IElement e) => e.Symbol == element.Key)?.MolarVolume).GetValueOrDefault();
            volume += (double)element.Value * valueOrDefault;
        }

        
        //Test changing Properties
        Properties.RangeOffset = 0.4d;
        foreach (var peak in isotopes)
        {
            if (peak.Abundance > minFraction)
            {
                double rangeMin = peak.Mass - Properties.RangeOffset;
                double rangeMax = peak.Mass + Properties.RangeOffset;
                var addRange = Resources.CreateRange(name, rangeMin, rangeMax, formula, volume);
                returnRanges.Add(addRange);
            }
        }

        return returnRanges;
    }
    */
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

    [Display(Name = "Volume (nm^3)", AutoGenerateField = true, Description = "Ionic Volume from Range Manager", GroupName = "Ranges")]
    [DisplayFormat(DataFormatString = "n4")]
    public new double Volume { get => base.Volume; }

    [Display(Name = "Min (Da)", AutoGenerateField = true, Description = "Left Range Edge", GroupName = "Ranges")]
    [DisplayFormat(DataFormatString = "n3")]
    public new double Min { get => base.Min; }

    [Display(Name = "Max (Da)", AutoGenerateField = true, Description = "Right Range Edge", GroupName = "Ranges")]
    [DisplayFormat(DataFormatString = "n3")]
    public new double Max { get => base.Max; }

    [Display(Name = "Color", AutoGenerateField = true, Description = "Display Color", GroupName = "Ranges")]
    public new System.Windows.Media.Color Color { get => base.Color; }

    public DisplayRangesTable(string name, IonFormula ionFormula, double volume, double min, double max, System.Windows.Media.Color color) 
        : base(name, ionFormula, volume, min, max, color)
    {
    }
    public DisplayRangesTable(IonTypeInfoRange ionTypeInfoRange)
    : base(ionTypeInfoRange.Name, ionTypeInfoRange.Formula, ionTypeInfoRange.Volume, ionTypeInfoRange.Min, ionTypeInfoRange.Max, ionTypeInfoRange.Color)
    {
    }
}

public enum RangeScheme
{
    Left,
    Half,
    Quarter
}

public class MyValues2
{
    public Vector2[]? Values;
    public int Length;
    public float StartPos;
    public float BinWidth;

    public MyValues2(int length, float startPos, float binWidth)
    {
        Values = new Vector2[length];
        Length = length;
        StartPos = startPos;
        BinWidth = binWidth;
    }

    public float GetPos(int index)
    {
        return (StartPos + (float)index * BinWidth);
    }

    public int GetIndex(float pos)
    {
        return (int)Math.Round((pos - StartPos) / BinWidth);
    }

    public float FindLocalMax(double min, double max)
    {
        int start = GetIndex((float)min);
        int stop = GetIndex((float)max);
        float maxValue = 0.0f;
        float maxPos = 0.0f;
        for (int i=start; i<=stop; i++)
        {
            if (Values[i].Y > maxValue)
            {
                maxValue = Values[i].Y;
                maxPos = Values[i].X;
            }
        }
        return maxPos;
    }

}