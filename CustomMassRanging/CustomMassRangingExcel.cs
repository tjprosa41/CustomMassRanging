//using IronXL;
using NanoXLSX;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Reflection;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace CustomMassRanging
{
    public class CustomMassRangingExcel
    {
        public void SaveExcelFile(MyRanging? values,
            Parameters parameters,
            ObservableCollection<RangesTableEntries> rangesTable,
            ObservableCollection<CompositionTableEntries> ionicCompositionTable,
            CompositionTableTotals ionicCompositionTotals,
            ObservableCollection<CompositionTableEntries> decomposedCompositionTable,
            CompositionTableTotals decomposedCompositionTotals,
            string multisInformation,
            List<Vector2[]> savedPlots, List<string> savedLegends,
            string saveFileName)
        {
            // Create a new Excel Workbook
            Workbook workBook = new Workbook(saveFileName, "Parameters"); //0
            PropertyInfo[] parametersProperties = typeof(Parameters).GetProperties();          
            int counter = 0;
            foreach (PropertyInfo property in parametersProperties)
            {
                if (property.Name.Contains("Upper") || property.Name.Contains("Lower")) continue;
                workBook.CurrentWorksheet.AddCell(property.Name,0, counter);
                var value = property.GetValue(parameters);
                if (value is Enum) workBook.CurrentWorksheet.AddCell(value.ToString(), 1, counter++);
                else workBook.CurrentWorksheet.AddCell(value, 1, counter++);
            }

            workBook.AddWorksheet("RangesTable"); //1
            workBook.CurrentWorksheet.AddCell("Multi", 0, 0);
            workBook.CurrentWorksheet.AddCell("Color", 1, 0);
            workBook.CurrentWorksheet.AddCell("Ion", 2, 0);
            workBook.CurrentWorksheet.AddCell("Peak(Da)", 3, 0);
            workBook.CurrentWorksheet.AddCell("Min(Da)", 4, 0);
            workBook.CurrentWorksheet.AddCell("Max(Da)", 5, 0);
            workBook.CurrentWorksheet.AddCell("Counts", 6, 0);
            workBook.CurrentWorksheet.AddCell("Scheme", 7, 0);
            workBook.CurrentWorksheet.AddCell("Bgd", 8, 0);
            workBook.CurrentWorksheet.AddCell("TailCounts", 9, 0);
            counter = 1;
            foreach (RangesTableEntries entry in rangesTable)
            {
                workBook.CurrentWorksheet.AddCell(entry.MultiUse, 0, counter);
                workBook.CurrentWorksheet.AddCell(entry.Color.ToString(), 1, counter);
                workBook.CurrentWorksheet.AddCell(entry.Name, 2, counter);
                workBook.CurrentWorksheet.AddCell(entry.Pos, 3, counter);
                workBook.CurrentWorksheet.AddCell(entry.Min, 4, counter);
                workBook.CurrentWorksheet.AddCell(entry.Max, 5, counter);
                workBook.CurrentWorksheet.AddCell(entry.Counts, 6, counter);
                workBook.CurrentWorksheet.AddCell(entry.Scheme.ToString(), 7, counter);
                workBook.CurrentWorksheet.AddCell(entry.Bgd, 8, counter);
                workBook.CurrentWorksheet.AddCell(entry.Tail, 9, counter++);
            }

            workBook.AddWorksheet("MassHistogram"); //2
            workBook.CurrentWorksheet.AddCell("MassToChargeRatio(Da)", 0, 0);
            workBook.CurrentWorksheet.AddCell("Counts", 1, 0);
            if (values != null)
            {
                for (int i = 0; i < values.Values.Length; i++)
                {
                    workBook.CurrentWorksheet.AddCell(values.Values[i].X, 0, i + 1);
                    workBook.CurrentWorksheet.AddCell(values.Values[i].Y, 1, i + 1);
                }
            }

            workBook.AddWorksheet("IonicComposition"); //3
            workBook.CurrentWorksheet.AddCell("Ion", 0, 0);
            workBook.CurrentWorksheet.AddCell("Composition", 1, 0);
            workBook.CurrentWorksheet.AddCell("Sigma/DT(95%CL)", 2, 0);
            workBook.CurrentWorksheet.AddCell("Counts", 3, 0);
            workBook.CurrentWorksheet.AddCell("Background", 4, 0);
            workBook.CurrentWorksheet.AddCell("Net", 5, 0);
            workBook.CurrentWorksheet.AddCell("Tail", 6, 0);
            workBook.CurrentWorksheet.AddCell("Missing", 7, 0);
            workBook.CurrentWorksheet.AddCell("CorrectedComposition", 8, 0);
            workBook.CurrentWorksheet.AddCell("Sigma/DT(95%CL)", 9, 0);
            counter = 1;
            foreach (CompositionTableEntries entry in ionicCompositionTable)
            {
                workBook.CurrentWorksheet.AddCell(entry.Name, 0, counter);
                workBook.CurrentWorksheet.AddCell(entry.Composition, 1, counter);
                workBook.CurrentWorksheet.AddCell(entry.SigmaString, 2, counter);
                workBook.CurrentWorksheet.AddCell(entry.Counts, 3, counter);
                workBook.CurrentWorksheet.AddCell(entry.Bgd, 4, counter);
                workBook.CurrentWorksheet.AddCell(entry.Net, 5, counter);
                workBook.CurrentWorksheet.AddCell(entry.Tail, 6, counter);
                workBook.CurrentWorksheet.AddCell(entry.Missing, 7, counter);
                workBook.CurrentWorksheet.AddCell(entry.CompositionMissing, 8, counter);
                workBook.CurrentWorksheet.AddCell(entry.SigmaMissingString, 9, counter++);
            }
            workBook.CurrentWorksheet.AddCell(ionicCompositionTotals.Name, 0, counter);
            workBook.CurrentWorksheet.AddCell(ionicCompositionTotals.Composition, 1, counter);
            workBook.CurrentWorksheet.AddCell("NA", 2, counter);
            workBook.CurrentWorksheet.AddCell(ionicCompositionTotals.Counts, 3, counter);
            workBook.CurrentWorksheet.AddCell(ionicCompositionTotals.Bgd, 4, counter);
            workBook.CurrentWorksheet.AddCell(ionicCompositionTotals.Net, 5, counter);
            workBook.CurrentWorksheet.AddCell(ionicCompositionTotals.Tail, 6, counter);
            workBook.CurrentWorksheet.AddCell(ionicCompositionTotals.Missing, 7, counter);
            workBook.CurrentWorksheet.AddCell(ionicCompositionTotals.CompositionMissing, 8, counter);
            workBook.CurrentWorksheet.AddCell("NA", 9, counter);

            workBook.AddWorksheet("DecomposedComposition"); //4
            workBook.CurrentWorksheet.AddCell("Element", 0, 0);
            workBook.CurrentWorksheet.AddCell("Composition", 1, 0);
            workBook.CurrentWorksheet.AddCell("Sigma/DT(95%CL)", 2, 0);
            workBook.CurrentWorksheet.AddCell("Counts", 3, 0);
            workBook.CurrentWorksheet.AddCell("Background", 4, 0);
            workBook.CurrentWorksheet.AddCell("Net", 5, 0);
            workBook.CurrentWorksheet.AddCell("Tail", 6, 0);
            workBook.CurrentWorksheet.AddCell("Missing", 7, 0);
            workBook.CurrentWorksheet.AddCell("CorrectedComposition", 8, 0);
            workBook.CurrentWorksheet.AddCell("Sigma/DT(95%CL)", 9, 0);
            counter = 1;
            foreach (CompositionTableEntries entry in decomposedCompositionTable)
            {
                workBook.CurrentWorksheet.AddCell(entry.Name, 0, counter);
                workBook.CurrentWorksheet.AddCell(entry.Composition, 1, counter);
                workBook.CurrentWorksheet.AddCell(entry.SigmaString, 2, counter);
                workBook.CurrentWorksheet.AddCell(entry.Counts, 3, counter);
                workBook.CurrentWorksheet.AddCell(entry.Bgd, 4, counter);
                workBook.CurrentWorksheet.AddCell(entry.Net, 5, counter);
                workBook.CurrentWorksheet.AddCell(entry.Tail, 6, counter);
                workBook.CurrentWorksheet.AddCell(entry.Missing, 7, counter);
                workBook.CurrentWorksheet.AddCell(entry.CompositionMissing, 8, counter);
                workBook.CurrentWorksheet.AddCell(entry.SigmaMissingString, 9, counter++);
            }
            workBook.CurrentWorksheet.AddCell(decomposedCompositionTotals.Name, 0, counter);
            workBook.CurrentWorksheet.AddCell(decomposedCompositionTotals.Composition, 1, counter);
            workBook.CurrentWorksheet.AddCell("NA", 2, counter);
            workBook.CurrentWorksheet.AddCell(decomposedCompositionTotals.Counts, 3, counter);
            workBook.CurrentWorksheet.AddCell(decomposedCompositionTotals.Bgd, 4, counter);
            workBook.CurrentWorksheet.AddCell(decomposedCompositionTotals.Net, 5, counter);
            workBook.CurrentWorksheet.AddCell(decomposedCompositionTotals.Tail, 6, counter);
            workBook.CurrentWorksheet.AddCell(decomposedCompositionTotals.Missing, 7, counter);
            workBook.CurrentWorksheet.AddCell(decomposedCompositionTotals.CompositionMissing, 8, counter);
            workBook.CurrentWorksheet.AddCell("NA", 9, counter);

            workBook.AddWorksheet("MultihitInformation"); //5
            char[] delimeterChars = { '\n' };
            string[] lines = multisInformation.Split(delimeterChars);
            for (int i = 0; i < lines.Length; i++)
                workBook.CurrentWorksheet.AddCell(lines[i], 0, i);

            workBook.AddWorksheet("SeparationPlots"); //6
            counter = 0;
            foreach (Vector2[] plot in savedPlots)
            {
                workBook.CurrentWorksheet.AddCell("Separation Distance (nm or mm)", 2 * counter, 0);
                workBook.CurrentWorksheet.AddCell(savedLegends[counter], 2 * counter + 1, 0);
                for (int i = 0; i < plot.Length; i++)
                {
                    string X = $"{plot[i].X:N3}"; 
                    workBook.CurrentWorksheet.AddCell(float.Parse(X), 2 * counter, i + 1);
                    workBook.CurrentWorksheet.AddCell(plot[i].Y, 2 * counter + 1, i + 1);
                }
                counter++;
            }

            System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog();
            saveFileDialog.Filter = "Excel Files (*.xlsx)|*.xlsx";
            saveFileDialog.Title = "Save an Excel File";
            saveFileDialog.FileName = $"{saveFileName}.xlsx";

            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string path = saveFileDialog.FileName;
                workBook.SaveAs(path); // Save using the chosen path [11]
            }

            // Save the Excel file
            //workBook.SaveAs(@"C:\Users\tjprosa\OneDrive - The University of Alabama\Desktop\Output.xlsx");
        }
    }
}
