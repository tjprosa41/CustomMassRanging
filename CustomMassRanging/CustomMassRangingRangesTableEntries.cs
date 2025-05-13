using System.Numerics;
using Cameca.CustomAnalysis.Interface;

namespace CustomMassRanging
{
    public class RangesTableEntries : IonTypeInfoRange
    {
        // Name -> Column name
        // AutoGenerateField -> If 'false', then this public property will not have a generated column in the table (default is true)
        // Description -> Tool tip text if hovering over the column
        // GroupName -> I don't use this often, but can be used group columns
        // DataFormatString -> Formats the numeric value.
        // See https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings or
        // https://learn.microsoft.com/en-us/dotnet/standard/base-types/custom-numeric-format-strings
        // https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/how-to-add-tables-and-columns-to-the-windows-forms-datagrid-control?view=netframeworkdesktop-4.8

        public double Pos { get; set; } = 0.0d;
        public double Bgd { get; set; } = 0.0d;
        public double BgdSigma2 { get; set; } = 0.0d;
        public double Net { get; set; } = 0.0d;
        public double Counts { get; set; } = 0.0d;
        public RangeScheme? Scheme { get; set; } = null;
        public double Tail { get; set; } = 0.0d;
        public const int POINTS = 20;
        public Vector3[] LineCoordinates { get; set; } = new Vector3[POINTS];

        //Need to keep track of left and right backgroud seperately to make background lines
        public RangesTableEntries(string name, double pos, double net, double counts, IonFormula ionFormula, double volume, double min, double max,
            RangeScheme? scheme, System.Windows.Media.Color color, double leftBgd, double rightBgd, double leftDelta = 0)
            : base(name, ionFormula, volume, min, max, color)
        {
            Pos = pos;
            Bgd = leftBgd + rightBgd;
            Net = net;
            Counts = counts;
            Scheme = scheme;
            CalculateBgdLineCoordinates(scheme, leftBgd, rightBgd, leftDelta);

            //Quarter ranges have larger background sigma for error propogation
            if (scheme == RangeScheme.Quarter)
                BgdSigma2 = 2 * Bgd;
            else
                BgdSigma2 = Bgd;
        }
        
        public RangesTableEntries(IonTypeInfoRange ionTypeInfoRange)
        : base(ionTypeInfoRange.Name, ionTypeInfoRange.Formula, ionTypeInfoRange.Volume, ionTypeInfoRange.Min, ionTypeInfoRange.Max, ionTypeInfoRange.Color)
        {
        }

        private void CalculateBgdLineCoordinates(RangeScheme? scheme, double leftBgd, double rightBgd, double leftDelta)
        {
            //This is the width of the range, so background width / 2
            double bgdWidth = (Max - Min) / 2.0d;
            switch (Scheme)
            {
                //Quarter is half the half
                case RangeScheme.Quarter:
                    bgdWidth /= 2.0d;
                    //Quarter has Bgd doubled, so cut back to 1X here
                    leftBgd /= 2.0d;
                    rightBgd /= 2.0d;
                    break;

                case RangeScheme.Half:
                    break;

                case RangeScheme.Left or RangeScheme.LeftTail:
                    bgdWidth *= 2.0d;
                    break;
            }
            //Min and Max are the range edges
            double leftMin = Min - bgdWidth;
            double leftMax = Min;
            double rightMin = Max;
            double rightMax = Max + bgdWidth;
            double slope = ((rightBgd / bgdWidth) - (leftBgd / bgdWidth)) / (rightMin - leftMin);

            //Log scale cannot deal with negative values!
            if (scheme == RangeScheme.Left || scheme == RangeScheme.LeftTail)
            {
                if (Min - leftDelta < 0) leftDelta = Min;
                LineCoordinates[0].X = (float)(Min - leftDelta);
                LineCoordinates[0].Y = -1.0f; //In front
                LineCoordinates[0].Z = (float)(leftBgd / bgdWidth);

                LineCoordinates[POINTS - 1].X = (float)(Min - leftDelta + bgdWidth);
                LineCoordinates[POINTS - 1].Y = -1.0f;
                LineCoordinates[POINTS - 1].Z = (float)(leftBgd / bgdWidth);

                slope = 0.0d;
            }
            else
            {
                LineCoordinates[0].X = (float)(leftMin);
                LineCoordinates[0].Y = -1.0f;
                LineCoordinates[0].Z = (float)(leftBgd / bgdWidth - (bgdWidth / 2.0d) * slope);
                //Move point until z value 50 -- it is scaled again by binWidth later which is typicall around 0.001-ish
                if (LineCoordinates[0].Z < 50.0f)
                {
                    LineCoordinates[0].X += (50.0f - LineCoordinates[0].Z) / (float)slope;
                    LineCoordinates[0].Z = 50.0f;
                }

                LineCoordinates[POINTS - 1].X = (float)(rightMax);
                LineCoordinates[POINTS - 1].Y = -1.0f;
                LineCoordinates[POINTS - 1].Z = (float)(rightBgd / bgdWidth + (bgdWidth / 2.0d) * slope);
                //Move point until z value 50 -- it is scaled again by binWidth later which is typicall around 0.001-ish
                if (LineCoordinates[POINTS - 1].Z < 50.0f)
                {
                    LineCoordinates[POINTS - 1].X += (50.0f - LineCoordinates[POINTS - 1].Z) / (float)slope;
                    LineCoordinates[POINTS - 1].Z = 50.0f;
                }
            }

            float deltaX = (LineCoordinates[POINTS - 1].X - LineCoordinates[0].X) / (float)(POINTS);
            float deltaZ = (float)slope * deltaX;
            float startX = LineCoordinates[0].X;
            float startZ = LineCoordinates[0].Z;
            for (int i = 1; i < POINTS - 1; i++)
            {
                startX += deltaX;
                startZ += deltaZ;
                LineCoordinates[i].X = startX;
                LineCoordinates[i].Y = -1.0f;
                LineCoordinates[i].Z = startZ;
            }
        }
    }
}

/* Old version to get tool tips with default table type.  To get that with above, need to different XAML

Replace a line like this:
<DataGridTextColumn Header="Name" Binding="{Binding IonTypeInfoRange.Name, Mode=OneWay}" />

With an addition of defining a HeaderStyle like this. Add your tooltip text to the value property.
<DataGridTextColumn Header="Name" Binding="{Binding IonTypeInfoRange.Name, Mode=OneWay}">
  <DataGridTextColumn.HeaderStyle>
    <Style TargetType="{x:Type DataGridColumnHeader}">
      <Setter Property="ToolTip" Value="Put your tooltip text here" />
    </Style>
  </DataGridTextColumn.HeaderStyle>
</DataGridTextColumn>
 
public class RangesTableEntries : IonTypeInfoRange
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

    [Display(Name = "Scheme", AutoGenerateField = true, Description = "Background Type", GroupName = "Ranges")]
    public RangeScheme? Scheme { get; set; } = null;

    //[Display(Name = "Color", AutoGenerateField = true, Description = "Display Color", GroupName = "Ranges")]
    [Display(AutoGenerateField = false)]
    public new System.Windows.Media.Color Color { get => base.Color; }

    public RangesTableEntries(string name, double pos, double net, double counts, IonFormula ionFormula, double volume, double min, double max, RangeScheme? scheme, System.Windows.Media.Color color)
        : base(name, ionFormula, volume, min, max, color)
    {
        Pos = pos;
        Net = net;
        Counts = counts;
        Scheme = scheme;
    }
    public RangesTableEntries(IonTypeInfoRange ionTypeInfoRange)
    : base(ionTypeInfoRange.Name, ionTypeInfoRange.Formula, ionTypeInfoRange.Volume, ionTypeInfoRange.Min, ionTypeInfoRange.Max, ionTypeInfoRange.Color)
    {
    }
}

*/