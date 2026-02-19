using Cameca.CustomAnalysis.Interface;
using IronXL;
using System.Windows.Forms;
using Microsoft.Win32;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using static Grpc.Core.Metadata;

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
            WorkBook workBook = WorkBook.Create(ExcelFileFormat.XLSX);

            List<string> tabNames = new List<string> { "Parameters", "RangesTable", "MassHistogram", "IonicComposition", "DecomposedComposition", 
                "MultihitInformation", "SeperationPlots"};

            WorkSheet[] workSheet = new WorkSheet[tabNames.Count];
            int counter = 0;
            foreach (string tab in tabNames)
            {
                workSheet[counter++] = workBook.CreateWorkSheet(tab);
            }

            int w = 0;
            PropertyInfo[] parametersProperties = typeof(Parameters).GetProperties();
            counter = 0;
            foreach (PropertyInfo property in parametersProperties)
            {
                if (property.Name.Contains("Upper") || property.Name.Contains("Lower")) continue;
                workSheet[w].SetCellValue(counter, 0, property.Name);
                var value = property.GetValue(parameters);
                if (value is Enum) workSheet[w].SetCellValue(counter++, 1, value.ToString());
                else workSheet[w].SetCellValue(counter++, 1, value);
            }

            w = 1;
            workSheet[w].SetCellValue(0, 0, "Multi");
            workSheet[w].SetCellValue(0, 1, "Color");
            workSheet[w].SetCellValue(0, 2, "Ion");
            workSheet[w].SetCellValue(0, 3, "Peak(Da)");
            workSheet[w].SetCellValue(0, 4, "Min(Da)");
            workSheet[w].SetCellValue(0, 5, "Max(Da)");
            workSheet[w].SetCellValue(0, 6, "Counts");
            workSheet[w].SetCellValue(0, 7, "Scheme");
            workSheet[w].SetCellValue(0, 8, "TailCounts");
            counter = 1;
            foreach (RangesTableEntries entry in rangesTable)
            {
                workSheet[w].SetCellValue(counter, 0, entry.MultiUse);
                workSheet[w].SetCellValue(counter, 1, entry.Color.ToString());
                workSheet[w].SetCellValue(counter, 2, entry.Name);
                workSheet[w].SetCellValue(counter, 3, entry.Pos);
                workSheet[w].SetCellValue(counter, 4, entry.Min);
                workSheet[w].SetCellValue(counter, 5, entry.Max);
                workSheet[w].SetCellValue(counter, 6, entry.Counts);
                workSheet[w].SetCellValue(counter, 7, entry.Scheme.ToString());
                workSheet[w].SetCellValue(counter++, 8, entry.Tail);
            }

            w = 2;
            workSheet[w].SetCellValue(0, 0, "MassToChargeRatio(Da)");
            workSheet[w].SetCellValue(0, 1, "Counts");
            if (values != null)
            {
                for (int i = 0; i < values.Values.Length; i++)
                {
                    workSheet[w].SetCellValue(i + 1, 0, values.Values[i].X);
                    workSheet[w].SetCellValue(i + 1, 1, values.Values[i].Y);
                }
            }

            w = 3;
            workSheet[w].SetCellValue(0, 0, "Ion");
            workSheet[w].SetCellValue(0, 1, "Composition");
            workSheet[w].SetCellValue(0, 2, "Sigma/DT(95%CL)");
            workSheet[w].SetCellValue(0, 3, "Counts");
            workSheet[w].SetCellValue(0, 4, "Background");
            workSheet[w].SetCellValue(0, 5, "Net");
            workSheet[w].SetCellValue(0, 6, "Tail");
            counter = 1;
            foreach (CompositionTableEntries entry in ionicCompositionTable)
            {
                workSheet[w].SetCellValue(counter, 0, entry.Name);
                workSheet[w].SetCellValue(counter, 1, entry.Composition);
                workSheet[w].SetCellValue(counter, 2, entry.SigmaString);
                workSheet[w].SetCellValue(counter, 3, entry.Counts);
                workSheet[w].SetCellValue(counter, 4, entry.Bgd);
                workSheet[w].SetCellValue(counter, 5, entry.Net);
                workSheet[w].SetCellValue(counter++, 6, entry.Tail);
            }
            workSheet[w].SetCellValue(counter, 0, ionicCompositionTotals.Name);
            workSheet[w].SetCellValue(counter, 1, ionicCompositionTotals.Composition);
            workSheet[w].SetCellValue(counter, 2, "NA");
            workSheet[w].SetCellValue(counter, 3, ionicCompositionTotals.Counts);
            workSheet[w].SetCellValue(counter, 4, ionicCompositionTotals.Bgd);
            workSheet[w].SetCellValue(counter, 5, ionicCompositionTotals.Net);
            workSheet[w].SetCellValue(counter, 6, ionicCompositionTotals.Tail);

            w = 4;
            workSheet[w].SetCellValue(0, 0, "Element");
            workSheet[w].SetCellValue(0, 1, "Composition");
            workSheet[w].SetCellValue(0, 2, "Sigma/DT(95%CL)");
            workSheet[w].SetCellValue(0, 3, "Counts");
            workSheet[w].SetCellValue(0, 4, "Background");
            workSheet[w].SetCellValue(0, 5, "Net");
            workSheet[w].SetCellValue(0, 6, "Tail");
            counter = 1;
            foreach (CompositionTableEntries entry in decomposedCompositionTable)
            {
                workSheet[w].SetCellValue(counter, 0, entry.Name);
                workSheet[w].SetCellValue(counter, 1, entry.Composition);
                workSheet[w].SetCellValue(counter, 2, entry.SigmaString);
                workSheet[w].SetCellValue(counter, 3, entry.Counts);
                workSheet[w].SetCellValue(counter, 4, entry.Bgd);
                workSheet[w].SetCellValue(counter, 5, entry.Net);
                workSheet[w].SetCellValue(counter++, 6, entry.Tail);
            }
            workSheet[w].SetCellValue(counter, 0, decomposedCompositionTotals.Name);
            workSheet[w].SetCellValue(counter, 1, decomposedCompositionTotals.Composition);
            workSheet[w].SetCellValue(counter, 2, "NA");
            workSheet[w].SetCellValue(counter, 3, decomposedCompositionTotals.Counts);
            workSheet[w].SetCellValue(counter, 4, decomposedCompositionTotals.Bgd);
            workSheet[w].SetCellValue(counter, 5, decomposedCompositionTotals.Net);
            workSheet[w].SetCellValue(counter, 6, decomposedCompositionTotals.Tail);

            w = 5;
            char[] delimeterChars = { '\n' };
            string[] lines = multisInformation.Split(delimeterChars);
            for (int i = 0; i < lines.Length; i++)
                workSheet[w].SetCellValue(i, 0, lines[i]);
            /*
            char[] delimeterChar = { ' ' };
            for (int i=0; i<lines.Length; i++)
            {
                string[] words = lines[i].Split(delimeterChar, StringSplitOptions.RemoveEmptyEntries);
                for (int j = 0; j < words.Length; j++)
                {
                    if (words[j][0].Equals('=')) words[j] = "'" + words[j];
                    workSheet[w].SetCellValue(i, j, words[j]);
                }
            }
            */

            w = 6;
            counter = 0;
            foreach (Vector2[] plot in savedPlots)
            {
                workSheet[w].SetCellValue(0, 2 * counter, "Separation Distance (nm or mm)");
                workSheet[w].SetCellValue(0, 2 * counter + 1, savedLegends[counter]);
                for (int i = 0; i < plot.Length; i++)
                {
                    workSheet[w].SetCellValue(i + 1, 2*counter, plot[i].X);
                    workSheet[w].SetCellValue(i + 1, 2*counter+1, plot[i].Y);
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
