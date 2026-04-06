using Cameca.CustomAnalysis.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace CustomMassRanging
{
    public class CompositionTableTotals : ObservableObject
    {
        private string name = "Totals:";
        private double composition;
        private double compositionmissing;
        private double counts;
        private double net;
        private double bgd;
        private double tail;
        private double missing;
        public void Clear()
        {
            Composition = 0.0d;
            CompositionMissing = 0.0d;
            Counts = 0.0d;
            Net = 0.0d;
            Bgd = 0.0d;
            Tail = 0.0d;
            Missing = 0.0d;
        }

        // This private/public lower/upper case construction send the INotify with the SetProperty call--makes them observable
        public string Name
        {
            get => name;
            set => SetProperty(ref name, value);
        }
        public double Composition
        {
            get => composition;
            set => SetProperty(ref composition, value);
        }
        public double CompositionMissing
        {
            get => compositionmissing;
            set => SetProperty(ref compositionmissing, value);
        }
        public double Counts
        {
            get => counts;
            set => SetProperty(ref counts, value);
        }
        public double Net
        {
            get => net;
            set => SetProperty(ref net, value);
        }
        public double Bgd
        {
            get => bgd;
            set => SetProperty(ref bgd, value);
        }
        public double Tail
        {
            get => tail;
            set => SetProperty(ref tail, value);
        }
        public double Missing
        {
            get => missing;
            set => SetProperty(ref missing, value);
        }
    }
    public class CompositionTableEntries
    {
        //Programming note:  The below was written for a standard controls:table.  This table is now a DataGrid and all the attributes are controlled in the XAML
        //  I left in all the Display stuff as example for controls:table, but they are ingored in the actual implimentation.

        // Name -> Column name
        // AutoGenerateField -> If 'false', then this public property will not have a generated column in the table (default is true)
        // Description -> Tool tip text if hovering over the column
        // GroupName -> I don't use this often, but can be used group columns
        // DataFormatString -> Formats the numeric value.
        // See https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings or
        // https://learn.microsoft.com/en-us/dotnet/standard/base-types/custom-numeric-format-strings
        // https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/how-to-add-tables-and-columns-to-the-windows-forms-datagrid-control?view=netframeworkdesktop-4.8

        //AutoGenerateField turns on/off
        [Display(Name = "Ion", AutoGenerateField = true, Description = "Ion Name", GroupName = "Composition")]
        public string Name { get; set; } = "";

        [Display(AutoGenerateField = false)]
        public IonFormula Formula { get; set; }

        [Display(Name = "Composition", AutoGenerateField = true, Description = "Composition", GroupName = "Composition")]
        [DisplayFormat(DataFormatString = "P5")]
        public double Composition { get; set; } = 0.0d;

        [Display(Name = "Composition", AutoGenerateField = true, Description = "Composition", GroupName = "Composition")]
        public string CompositionString { get; set; } = "";

        [Display(Name = "Sigma", AutoGenerateField = true, Description = "Composition Uncertainty", GroupName = "Composition")]
        [DisplayFormat(DataFormatString = "P5")]
        public double Sigma { get; set; } = 0.0d;

        [Display(Name = "Sigma/DT (95% CL)", AutoGenerateField = true, Description = "Composition Uncertainty or 95% Confidence Level Detection Threshold when Not Detected (ND).", GroupName = "Composition")]
        public string SigmaString { get; set; } = "";

        [Display(AutoGenerateField = false)]
        //DT  = Detection Threshold
        public double DT { get; set; } = 0.0d;

        //New
        [Display(Name = "Cor. Comp.", AutoGenerateField = true, Description = "Deadtime Corrected Composition", GroupName = "Composition")]
        [DisplayFormat(DataFormatString = "P5")]
        public double CompositionMissing { get; set; } = 0.0d;

        [Display(Name = "Cor. Comp.", AutoGenerateField = true, Description = "Deadtime Corrected Composition", GroupName = "Composition")]
        public string CompositionMissingString { get; set; } = "";
        [Display(Name = "SigmaMissing", AutoGenerateField = true, Description = "Deadtime Corrected Composition Uncertainty", GroupName = "Composition")]
        [DisplayFormat(DataFormatString = "P5")]
        public double SigmaMissing { get; set; } = 0.0d;

        [Display(Name = "SigmaMissing/DT (95% CL)", AutoGenerateField = true, Description = "Deadtime Corrected Composition Uncertainty or 95% Confidence Level Detection Threshold when Not Detected (ND).", GroupName = "Composition")]
        public string SigmaMissingString { get; set; } = "";
        
        [Display(AutoGenerateField = false)]
        //DT  = Detection Threshold
        public double DTMissing { get; set; } = 0.0d;

        [Display(Name = "Counts", AutoGenerateField = true, Description = "Raw Ranged Ion Counts Total", GroupName = "Composition")]
        public double Counts { get; set; } = 0.0d;

        [Display(Name = "Net", AutoGenerateField = true, Description = "Ion Net Counts Total = Counts - Bgd", GroupName = "Composition")]
        public double Net { get; set; } = 0.0d;

        [Display(Name = "Bgd", AutoGenerateField = true, Description = "Ion Background Total", GroupName = "Composition")]
        public double Bgd { get; set; } = 0.0d;

        [Display(Name = "Tail", AutoGenerateField = true, Description = "Total estimated net counts in tail.", GroupName = "Composition")]
        public double Tail { get; set; } = 0.0d;

        [Display(Name = "BgdSigma", AutoGenerateField = true, Description = "Ion Background Sigma^2 Total", GroupName = "Composition")]
        public double BgdSigma2 { get; set; } = 0.0d;

        [Display(Name = "Missing", AutoGenerateField = true, Description = "Estimated counts missing due to deadtime effects.", GroupName = "Composition")]
        public double Missing { get; set; } = 0.0d;

        [Display(Name = "MissingSigma", AutoGenerateField = true, Description = "Missing Count Estimate Sigma^2 Total", GroupName = "Composition")]
        public double MissingSigma2 { get; set; } = 0.0d;
        
        //Unused, but maybe update and add later...
        /*
        public CompositionTableEntries()
        {
            Name = "Totals:";
            Formula = new IonFormula(Enumerable.Empty<IonFormula.Component>());
            Composition = 0.0d;
            CorComposition = 0.0d;
            Counts = 0.0d;
            Net = 0.0d;
            Bgd = 0.0d;
            Tail = 0.0d;
            Missing = 0.0d;
        }
        public CompositionTableEntries(string name, IonFormula formula, double net, double counts, double bgd, double sigma2, double tail = 0.0d)
        {
            Name = name;
            Formula = formula;
            Composition = 0.0d;
            Sigma = 0.0d;
            DT = 0.0d;
            Counts = counts;
            Net = net;
            Bgd = bgd;
            Tail = tail;
            BgdSigma2 = sigma2;
        }
        */
        
        public CompositionTableEntries(RangesTableEntries rangesTableEntries)
        {
            Name = rangesTableEntries.Name;
            Formula = rangesTableEntries.Formula;
            Composition = 0.0d;
            Sigma = 0.0d;
            DT = 0.0d;
            CompositionMissing = 0.0d;
            SigmaMissing = 0.0d;
            DTMissing = 0.0d;
            Counts = rangesTableEntries.Counts;
            Net = rangesTableEntries.Net;
            Bgd = rangesTableEntries.Bgd;
            Tail = rangesTableEntries.Tail;
            BgdSigma2 = rangesTableEntries.BgdSigma2;
            Missing = rangesTableEntries.Missing;
            MissingSigma2 = rangesTableEntries.MissingSigma2;
        }
        public void AddToThisEntry(CompositionTableEntries ionicCompositionTableEntries)
        {
            //Counts should be only counts from normal ranging
            Counts += ionicCompositionTableEntries.Counts;
            //Net will include ranges and tails
            Net += ionicCompositionTableEntries.Net;
            //Bgd is only the bgd for the ranges
            Bgd += ionicCompositionTableEntries.Bgd;
            //Tail is the total net tail
            Tail += ionicCompositionTableEntries.Tail;
            //BgdSigma2 includes total of ranges and tails
            BgdSigma2 += ionicCompositionTableEntries.BgdSigma2;
            //Missing is the total missing
            Missing += ionicCompositionTableEntries.Missing;
            //MissingSigma2 includes total of missing
            MissingSigma2 += ionicCompositionTableEntries.MissingSigma2;
        }
        public void SubtractFromThisEntry(CompositionTableEntries ionicCompositionTableEntries)
        {
            //Counts should be only counts from normal ranging
            Counts -= ionicCompositionTableEntries.Counts;
            //Net will include ranges and tails
            Net -= ionicCompositionTableEntries.Net;
            //Bgd is only the bgd for the ranges
            Bgd -= ionicCompositionTableEntries.Bgd;
            //Tail is the total net tail
            Tail -= ionicCompositionTableEntries.Tail;
            //BgdSigma2 includes total of ranges and tails
            BgdSigma2 -= ionicCompositionTableEntries.BgdSigma2;
            //Missing is the total missing
            Missing -= ionicCompositionTableEntries.Missing;
            //MissingSigma2 includes total of missing
            MissingSigma2 -= ionicCompositionTableEntries.MissingSigma2;
        }
        public void Clear()
        {
            Composition = 0.0d;
            CompositionMissing = 0.0d;
            Counts = 0.0d;
            Net = 0.0d;
            Bgd = 0.0d;
            Tail = 0.0d;
            Missing = 0.0d;
        }
    }
}
