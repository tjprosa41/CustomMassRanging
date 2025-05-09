using System.ComponentModel.DataAnnotations;
using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CustomMassRanging
{
    public partial class Parameters : ObservableObject
    {
        /* 
         * [ObservableProperty] makes the associated field (must be lower case) generate a public property (with upper case) that includes code for event notifications when changes are made
         * Other attributes should have "field:" prefix to tell the [ObservableProperty] generator that the attribute that usually can only be used on properties can be set on this field
         *   The generated public property will use these attribute as usual
         * All properties with the same GroupName will be included in collected groups
         */
        [ObservableProperty]
        [field: Display(Name = "Max Peak Name", GroupName = "Histogram Information",
            Description = "Ion range name of the most\n" +
                          "intense peak in the mass spectrum.\n" +
                          "Informational.")]
        public string? sMaxPeakName;

        [ObservableProperty]
        [field: Display(Name = "Max Peak Position (Da)", GroupName = "Histogram Information",
            Description = "Most intense m/z location (bin) of Max Peak.\n" +
                          "All ranging widths are scaled from this peak location.")]
        public double dMaxPeakPosition;

        [ObservableProperty]
        [field: Display(Name = "Max Peak FWHunM (Da)", GroupName = "Histogram Information", 
            Description = "FWHunM (Da) width of Max Peak.\n"+
                          "This is the reference width used for determining\n"+
                          "minimum width and fixed width criteria.")]
        public double dMaxPeakFWHunM;

        [ObservableProperty]
        [field: Display(Name = "Max Peak MRP", GroupName = "Histogram Information", 
            Description = "Mass Resolving Power (MRP) of Max Peak.\n"+
                          "Informational.")]
        //[field: DisplayFormat(DataFormatString = "n1")]
        public double dMaxPeakMRP;

        [ObservableProperty]
        [field: Display(Name = "Spectrum Coarsen Factor", GroupName = "Histogram Information", 
            Description = "Original spectrum resolution coarsened by this value.\n"+
                          "Chosen to result in FWHunM of Max Peak to posses\n"+
                          "between 15 and 30 bins.")]
        public int iSpectrumCoarsenFactor;

        [ObservableProperty]
        [field: Display(Name = "Fixed Ranging Width Factor", GroupName = "Ranging Parameters", 
            Description = "Set all range widths to this*FWHunM\n"+
                          "(and scaled by sqrt(m/z)).\n"+
                          "Also used to specify LEFT scheme criteria range size.")]
        public double dRangingWidthFactor;

        [ObservableProperty]
        [field: Display(Name = "Min Width Factor", GroupName = "Ranging Parameters", 
            Description = "Set range minimum width to this*FWHunM\n"+
                          "(and scaled by sqrt(m/z)).")]
        public double dMinWidthFactor;

        [ObservableProperty]
        [field: Display(Name = "Left Range Criteria", GroupName = "Ranging Parameters",
            Description = "Use LEFT ranging scheme when no other peaks exists\n" +
                          "within THIS-MANY Da to the LEFT of given peak.")]
        public double dLeftRangeCriteria;

        [ObservableProperty]
        [field: Display(Name = "Left Range Delta", GroupName = "Ranging Parameters", 
            Description = "Target region THIS-MANY Da to the LEFT for background determination.")]
        public double dLeftRangeDelta;

        [ObservableProperty]
        [field: Display(Name = "Use Fixed Ranging Width", GroupName = "Ranging Parameters", 
            Description = "Use same width for all ranges (and scaled by sqrt(m/z)).")]
        public bool bUseFixedRangingWidth;

        [ObservableProperty]
        [field: Display(Name = "Considered Tail Range (Da)", GroupName = "Tail Parameters", 
            Description = "Region (in Da) past range maximum to fit exponential tail.")]
        public double dConsideredTailRange;

        [ObservableProperty]
        [field: Display(Name = "Tail Estimate Uncertainty", GroupName = "Tail Parameters", 
            Description = "User estimated uncertainty for tail estimate."), DisplayFormat(DataFormatString = "P1")]
        public double dTailEstimateUncertainty;

        [ObservableProperty]
        [field: Display(Name = "Tail Range Maximum", GroupName = "Tail Parameters", 
            Description = "Maximum tail length alowed before triggering fit error.")]
        public double dTailRangeMaximum;

        [ObservableProperty]
        [field: Display(Name = "Sensitivity", GroupName = "Peak Discovery Parameters", 
            Description = "Value of 1 means 99% CL signal detected for a range of minimum range width\n"+
                          "(must be less than 1, e.g., 0.5 will double the requred net counts for detection).")]
        public double dSensitivity = 0.5d;

        [ObservableProperty]
        [field: Display(Name = "Min Bin Pairs", GroupName = "Peak Discovery Parameters", 
            Description = "Two times this number is minimum bin width used for testing if peak\n"+
                          "is found (needed for low m/z peaks, otherwise they are too narrow).")]
        public int iMinBinPairs = 6;

        [ObservableProperty]
        [field: Display(Name = "Min Counts for Peak Max Bin", GroupName = "Peak Discovery Parameters", 
            Description = "When a peak is detected, counts in max bin count must\n"+
                          "exceed this (good for filtering peaks tail of histogram).")]
        public int iMinPeakMaxCounts = 3;

        [ObservableProperty]
        [field: Display(Name = "Viewport Lower Vector2", AutoGenerateField = false, GroupName = "Display Properties")]
        public Vector2 viewportLower;

        [ObservableProperty]
        [field: Display(Name = "Viewport Upper Vector2", AutoGenerateField = false, GroupName = "Display Properties")]
        public Vector2 viewportUpper;

        [ObservableProperty]
        [field: Display(Name = "Viewport Lower X", AutoGenerateField = false, GroupName = "Display Properties")]
        public float lowerX;

        [ObservableProperty]
        [field: Display(Name = "Viewport Lower Y", AutoGenerateField = false, GroupName = "Display Properties")]
        public float lowerY;

        [ObservableProperty]
        [field: Display(Name = "Viewport Upper X", AutoGenerateField = false, GroupName = "Display Properties")]
        public float upperX;

        [ObservableProperty]
        [field: Display(Name = "Viewport Upper Y", AutoGenerateField = false, GroupName = "Display Properties")]
        public float upperY;

        /*
        //See enum example below
        [ObservableProperty]
        [field: Display(Name = "Enum Property (type ExampleEnum)", GroupName = "")]
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
            DLeftRangeDelta = properties.DLeftRangeDelta;
            BUseFixedRangingWidth = properties.BUseFixedRangingWidth;
            DConsideredTailRange = properties.DConsideredTailRange;
            DTailEstimateUncertainty = properties.DTailEstimateUncertainty;
            if (properties.DSensitivity < 0.01d || properties.DSensitivity > 1.0d)
                DSensitivity = 0.5d;
            else
                DSensitivity = properties.DSensitivity;
            DTailRangeMaximum = properties.DTailRangeMaximum;
            IMinBinPairs = properties.IMinBinPairs;
            IMinPeakMaxCounts = properties.IMinPeakMaxCounts;

            //Quirk if no change in viewport, then reset to unzoomed, so add a little change
            if (properties.ViewportLower == ViewportLower)
            {
                LowerX = properties.ViewportLower.X;
                LowerY = properties.ViewportLower.Y - 0.0001f;
                UpperX = properties.ViewportUpper.X;
                UpperY = properties.ViewportUpper.Y + 0.0001f;
            }
            else
            {
                LowerX = properties.ViewportLower.X;
                LowerY = properties.ViewportLower.Y;
                UpperX = properties.ViewportUpper.X;
                UpperY = properties.ViewportUpper.Y;
            }
            ViewportLower = new Vector2(LowerX, LowerY);
            ViewportUpper = new Vector2(UpperX, UpperY);
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
}