using Cameca.CustomAnalysis.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace CustomMassRanging;

//CustomMassRangingProperties is saved with savedanalysis
//Will use this as the list of valid properties/parameters
//Parameters is the editable table to customize the mass ranging
//Need to copy from this to Parameters at start (in case savedanalysis)
//When applied need to copy from Parameters to this
public partial class CustomMassRangingProperties : ObservableObject
{
    [field: Display(Name = "Max Peak Name")]
    [ObservableProperty]
    public string? sMaxPeakName = null;

    [field: Display(Name = "Max Peak Position (Da)")]
    [ObservableProperty]
    public double dMaxPeakPosition = -1.0d;

    [field: Display(Name = "Max Peak FWHunM (Da)")]
    [ObservableProperty]
    public double dMaxPeakFWHunM = -1.0d;

    [field: Display(Name = "Max Peak MRP")]
    [field: DisplayFormat(DataFormatString = "n1")]
    [ObservableProperty]
    public double dMaxPeakMRP = -1.0d;

    [field: Display(Name = "Spectrum Coarsen Factor")]
    [ObservableProperty]
    public int iSpectrumCoarsenFactor = -1;

    [field: Display(Name = "Fixed Ranging Width Factor")]
    [ObservableProperty]
    public double dRangingWidthFactor = 2.5d;

    [field: Display(Name = "Min Width Factor")]
    [ObservableProperty]
    public double dMinWidthFactor = 1.0d;

    [field: Display(Name = "Left Range Criteria")]
    [ObservableProperty]
    public double dLeftRangeCriteria = 5.0d;

    [field: Display(Name = "Use Fixed Ranging Width")]
    [ObservableProperty]
    public bool bUseFixedRangingWidth = false;

    //First lettter needs to be lower case for Observable because capital case version is generated

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
        BUseFixedRangingWidth = parameters.BUseFixedRangingWidth;
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
        copy.BUseFixedRangingWidth = BUseFixedRangingWidth;
        return copy;
    }
}

public partial class Parameters : ObservableObject
{
    /* 
     * [ObservableProperty] makes the associated field (must be lower case) generate a public property (with upper case) that includes code for event notifications when changes are made
     * Other attributes should have "field:" prefix to tell the [ObservableProperty] generator that the attribute that usually can only be used on properties can be set on this field
     *   The generated public property will use these attribute as usual
     * All properties with the same GroupName will be included in collected groups
     */
    [ObservableProperty]
    [field: Display(Name = "Max Peak Name", Description = "Most intense peak in spectruum.", GroupName = "Parameters (also Properties)")]
    public string? sMaxPeakName;

    [ObservableProperty]
    [field: Display(Name = "Max Peak Position", Description = "Most intense peak in spectrum position.", GroupName = "Parameters (also Properties)")]
    public double dMaxPeakPosition;

    [ObservableProperty]
    [field: Display(Name = "Max Peak FWHunM (Da)", Description = "Most intense peak in spectrum FWHunM (Da).", GroupName = "Parameters (also Properties)")]
    public double dMaxPeakFWHunM;
    
    [ObservableProperty]
    [field: Display(Name = "Max Peak MRP", Description = "Most intense peak in spectrum Mass Resolving Power.", GroupName = "Parameters (also Properties)")]
    [field: DisplayFormat(DataFormatString = "n1")]
    public double dMaxPeakMRP;

    [ObservableProperty]
    [field: Display(Name = "Spectrum Coarsen Factor", Description = "Original spectrum resolution coarsened by this value.", GroupName = "Parameters (also Properties)")]
    public int iSpectrumCoarsenFactor;

    [ObservableProperty]
    [field: Display(Name = "Ranging Width Factor", Description = "Set range widths to this.factor*FWHunM.", GroupName = "Parameters (also Properties)")]
    public double dRangingWidthFactor;

    [ObservableProperty]
    [field: Display(Name = "Min Width Factor", Description = "Set range minimum width to this.factor*FWHunM.", GroupName = "Parameters (also Properties)")]
    public double dMinWidthFactor;

    [ObservableProperty]
    [field: Display(Name = "Left Range Criteria", Description = "Use left ranging scheme when nearest peak is farther than this to the left.", GroupName = "Parameters (also Properties)")]
    public double dLeftRangeCriteria;

    [ObservableProperty]
    [field: Display(Name = "Use Fixed Ranging Width", Description = "Use same width for all ranges.", GroupName = "Parameters (also Properties)")]
    public bool bUseFixedRangingWidth;

    /*
    //See enum example below
    [ObservableProperty]
    [field: Display(Name = "Enum Property (type ExampleEnum)", GroupName = "Parameters (also Properties)")]
    private ExampleEnum parameter4;
    */

    public void ResetParametersObservablesToPropertiesObservables(CustomMassRangingProperties properties)
    {
        //Caps are the Observable versions
        SMaxPeakName = properties.SMaxPeakName;
        DMaxPeakPosition = properties.DMaxPeakPosition;
        DMaxPeakFWHunM = properties.DMaxPeakFWHunM;
        DMaxPeakMRP = properties.DMaxPeakMRP;
        ISpectrumCoarsenFactor = properties.ISpectrumCoarsenFactor;
        DRangingWidthFactor = properties.DRangingWidthFactor;
        DMinWidthFactor = properties.DMinWidthFactor;
        DLeftRangeCriteria = properties.DLeftRangeCriteria;
        BUseFixedRangingWidth = properties.BUseFixedRangingWidth;
    }
}

// Example of using an enum to define options that can populate a dropdown box.
public enum ExampleEnum
{
    [Display(Name = "Selection 1")]
    Selection1,
    [Display(Name = "Selection 2")]
    Selection2,
    [Display(Name = "Selection 3")]
    Selection3,
}