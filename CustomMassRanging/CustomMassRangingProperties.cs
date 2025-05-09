using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CustomMassRanging;

//CustomMassRangingProperties is saved with SavedAnalysisState/Tree
//Will use this as the list of valid properties/parameters
//Parameters is the editable table to customize the mass ranging
//Need to copy from this to Parameters at start (in case savedanalysis)
//When applied need to copy from Parameters to this
public partial class CustomMassRangingProperties : ObservableObject
{
    /* 
     * [ObservableProperty] makes the associated field (must be lower case) generate a public property (with upper case) that includes code for event notifications when changes are made
     * Other attributes should have "field:" prefix to tell the [ObservableProperty] generator that the attribute that usually can only be used on properties can be set on this field
     *   The generated public property will use these attribute as usual
     * All properties with the same GroupName will be included in collected groups
     */

    //Programming note: DisplayFormat() should work, but does not in IVAS 6.3.3.51 
    //  This is a DevExpress PropertyGridControl issue (the one used for these extensions)
    //  Looking at the AP Suite source code, it seems like CAMECA has a bunch of conditional
    //  field formatting directly into the general template just for when those fields are present.
    //  e.g., [field: DisplayFormat(DataFormatString = "n0")] will not work, but IVAS may force
    //  formating that makes it seem like it is being used when it is not.

    //Properties is IVAS controlled (and I do not know how it knows this is the right class).
    //  It does not respect/use GroupName, but I'll include here as it is
    //  then exactly the same as my Parameters Table

    [ObservableProperty]
    [field: Display(Name = "Max Peak Name", GroupName = "Histogram Information",
        Description = "Ion range name of the most\n" +
                      "intense peak in the mass spectrum.\n" +
                      "Informational.")]
    public string? sMaxPeakName = null;

    [ObservableProperty]
    [field: Display(Name = "Max Peak Position (Da)", GroupName = "Histogram Information",
        Description = "Most intense m/z location (bin) of Max Peak.\n"+
                      "All ranging widths are scaled from this peak location.")]
    public double dMaxPeakPosition = -1.0d;

    [ObservableProperty]
    [field: Display(Name = "Max Peak FWHunM (Da)", GroupName = "Histogram Information",
        Description = "FWHunM (Da) width of Max Peak.\n" +
                      "This is the reference width used for determining\n" +
                      "minimum width and fixed width criteria.")]
    public double dMaxPeakFWHunM = -1.0d;

    [ObservableProperty]
    [field: Display(Name = "Max Peak MRP", GroupName = "Histogram Information",
        Description = "Mass Resolving Power (MRP) of Max Peak.\n" +
                      "Informational.")]
    public double dMaxPeakMRP = -1.0d;

    [ObservableProperty]
    [field: Display(Name = "Spectrum Coarsen Factor", GroupName = "Histogram Information",
        Description = "Original spectrum resolution coarsened by this value.\n" +
                      "Chosen to result in FWHunM of Max Peak to posses\n" +
                      "between 15 and 30 bins.")]
    public int iSpectrumCoarsenFactor = -1;

    [ObservableProperty]
    [field: Display(Name = "Fixed Ranging Width Factor", GroupName = "Ranging Parameters",
        Description = "Set all range widths to this*FWHunM\n" +
                      "(and scaled by sqrt(m/z)).\n" +
                      "Also used to specify LEFT scheme criteria range size.")]
    public double dRangingWidthFactor = 2.5d;

    [ObservableProperty]
    [field: Display(Name = "Min Width Factor", GroupName = "Ranging Parameters",
        Description = "Set range minimum width to this*FWHunM\n" +
                      "(and scaled by sqrt(m/z)).")]
    public double dMinWidthFactor = 0.4d;

    [ObservableProperty]
    [field: Display(Name = "Left Range Criteria", GroupName = "Ranging Parameters",
        Description = "Use LEFT ranging scheme when no other peaks exists\n" +
                      "within THIS-MANY Da to the LEFT of given peak.")]
    public double dLeftRangeCriteria = 3.0d;

    [ObservableProperty]
    [field: Display(Name = "Left Range Delta", GroupName = "Ranging Parameters",
        Description = "Target region THIS-MANY Da to the LEFT for background determination.")]
    public double dLeftRangeDelta = 1.5d;

    [ObservableProperty]
    [field: Display(Name = "Use Fixed Ranging Width", GroupName = "Ranging Parameters",
        Description = "Use same width for all ranges (and scaled by sqrt(m/z)).")]
    public bool bUseFixedRangingWidth = false;

    [ObservableProperty]
    [field: Display(Name = "Considered Tail Range (Da)", GroupName = "Tail Parameters",
        Description = "Region (in Da) past range maximum to fit exponential tail.")]
    public double dConsideredTailRange = 1.0d;

    [ObservableProperty]
    [field: Display(Name = "Tail Estimate Uncertainty", GroupName = "Tail Parameters",
        Description = "User estimated uncertainty for tail estimate."), DisplayFormat(DataFormatString = "P1")]
    public double dTailEstimateUncertainty = 0.05d;

    [ObservableProperty]
    [field: Display(Name = "Tail Range Maximum", GroupName = "Tail Parameters",
        Description = "Maximum tail length alowed before triggering fit error.")]
    public double dTailRangeMaximum = 5.0d;

    [ObservableProperty]
    [field: Display(Name = "Sensitivity", GroupName = "Peak Discovery Parameters",
        Description = "Value of 1 means 99% CL signal detected for a range of minimum range width\n" +
                      "(must be less than 1, e.g., 0.5 will double the requred net counts for detection).")]
    public double dSensitivity = 0.5d;

    [ObservableProperty]
    [field: Display(Name = "Min Bin Pairs", GroupName = "Peak Discovery Parameters",
        Description = "Two times this number is minimum bin width used for testing if peak\n" +
                      "is found (needed for low m/z peaks, otherwise they are too narrow).")]
    public int iMinBinPairs = 6;

    [ObservableProperty]
    [field: Display(Name = "Min Counts for Peak Max Bin", GroupName = "Peak Discovery Parameters",
        Description = "When a peak is detected, counts in max bin count must\n" +
                      "exceed this (good for filtering peaks tail of histogram).")]
    public int iMinPeakMaxCounts = 3;

    public List<RangeList> RangesList = new();

    //Starts in log scale, so defalut Y needs to be >0
    [ObservableProperty]
    [field: Display(Name = "Viewport Lower", AutoGenerateField = false, GroupName = "Display Properties")]
    public Vector2 viewportLower = new Vector2(0.0f, 1.0f);

    [ObservableProperty]
    [field: Display(Name = "Viewport Upper", AutoGenerateField = false, GroupName = "Display Properties")]
    public Vector2 viewportUpper = new Vector2(0.0f, 1.0f);

    public void UpdatePropertiesObservablesToParametersObservables(Parameters parameters)
    {
        SMaxPeakName = parameters.SMaxPeakName;
        DMaxPeakPosition = parameters.DMaxPeakPosition;
        DMaxPeakFWHunM = parameters.DMaxPeakFWHunM;
        DMaxPeakMRP = parameters.DMaxPeakMRP;
        ISpectrumCoarsenFactor = parameters.ISpectrumCoarsenFactor;
        DRangingWidthFactor = parameters.DRangingWidthFactor;
        DMinWidthFactor = parameters.DMinWidthFactor;
        DLeftRangeCriteria = parameters.DLeftRangeCriteria;
        DLeftRangeDelta = parameters.DLeftRangeDelta;
        BUseFixedRangingWidth = parameters.BUseFixedRangingWidth;
        DConsideredTailRange = parameters.DConsideredTailRange;
        DTailEstimateUncertainty = parameters.DTailEstimateUncertainty;
        dTailRangeMaximum = parameters.dTailRangeMaximum;
        DSensitivity = parameters.DSensitivity;
        IMinBinPairs = parameters.IMinBinPairs;
        IMinPeakMaxCounts = parameters.IMinPeakMaxCounts;
        ViewportUpper = parameters.ViewportUpper;
        ViewportLower = parameters.ViewportLower;
    }

    public void UpdatePropertiesSchemesToProperties(ObservableCollection<RangesTableEntries> rangesTable)
    {
        RangesList.Clear();
        foreach (var range in rangesTable)
            RangesList.Add(new RangeList(range.Pos, range.Scheme));
    }

    public Parameters CopyPropertiesObservablesToParametersObservables()
    {
        Parameters copy = new();
        copy.SMaxPeakName = SMaxPeakName;
        copy.DMaxPeakPosition = DMaxPeakPosition;
        copy.DMaxPeakFWHunM = DMaxPeakFWHunM;
        copy.DMaxPeakMRP = DMaxPeakMRP;
        copy.ISpectrumCoarsenFactor = ISpectrumCoarsenFactor;
        copy.DRangingWidthFactor = DRangingWidthFactor;
        copy.DMinWidthFactor = DMinWidthFactor;
        copy.DLeftRangeCriteria = DLeftRangeCriteria;
        copy.DLeftRangeDelta = DLeftRangeDelta;
        copy.BUseFixedRangingWidth = BUseFixedRangingWidth;
        copy.DConsideredTailRange = DConsideredTailRange;
        copy.DTailEstimateUncertainty = DTailEstimateUncertainty;
        copy.DTailRangeMaximum = DTailRangeMaximum;
        copy.DSensitivity = DSensitivity;
        copy.IMinBinPairs = IMinBinPairs;
        copy.IMinPeakMaxCounts = IMinPeakMaxCounts;
        copy.ViewportUpper = ViewportUpper;
        copy.ViewportLower = ViewportLower;
        return copy;
    }
}

//Need this to make serializable -- need RangeList() defined
public class RangeList
{
    public double Pos;
    public RangeScheme? Scheme;

    public RangeList()
    {
        Pos = 0.0d;
        Scheme = null;
    }
    public RangeList(double pos, RangeScheme? scheme)
    {
        Pos = pos;
        Scheme = scheme;
    }
}