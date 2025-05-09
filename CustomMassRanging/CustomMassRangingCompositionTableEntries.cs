using System.ComponentModel.DataAnnotations;
using Cameca.CustomAnalysis.Interface;

namespace CustomMassRanging
{
    public class CompositionTableEntries
    {
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
        public double DT { get; set; } = 0.0d;

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
        public CompositionTableEntries(RangesTableEntries rangesTableEntries)
        {
            Name = rangesTableEntries.Name;
            Formula = rangesTableEntries.Formula;
            Composition = 0.0d;
            Sigma = 0.0d;
            DT = 0.0d;
            Counts = rangesTableEntries.Counts;
            Net = rangesTableEntries.Net;
            Bgd = rangesTableEntries.Bgd;
            Tail = rangesTableEntries.Tail;
            BgdSigma2 = rangesTableEntries.BgdSigma2;
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
        }
    }
}
