using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using System.Runtime.InteropServices;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Runtime.CompilerServices;
using System.Data;
using static CustomMassRanging.MultiHits;
using System.Windows.Markup;
using static System.Net.Mime.MediaTypeNames;
using System.Runtime.Intrinsics.Arm;
using System.Windows.Controls;
using System.Collections.ObjectModel;

namespace CustomMassRanging
{
    internal class MultiHits
    {
        // Parameters eventually
        int keyRange = 1;
        bool useReconCoordinates = true; //true = nm, false = mm
        float critSep = 8.0f;
        public int DPMax = 5;

        public const int HREGMax = 5;
        public const int NDistBins = 1000;
        public const int DPBins = 1000;
        public const float DistRes = 0.2f; //1000*0.2 = 200 nm or mm

        bool lastLastWasSingle = false;
        MultiStuff lastLastSingleMultiStuff = null!;
        double pulseFirst = 0d;
        double pulseLast = 0d;

        public int N;                           //Number of ranges to track
        public int NTotal;                      //Total number of defined ranges
        public float massSpecturmRes;
        public int[] rangeMassSpectrum = null!; //Conversion array bin --> range number

        public int eventPulses;                 //Total pulses with at least 1 event
        public int totKeyRangeCount;            //Totals to compute averages
        public double totToF, totVolt;          //Totals to compute averages (need larger number type double)
        public double totToFSq, totVoltSq;
        public float aveToF, aveVolt, aveDR;    //Average for representative range
        public float stdevToF, stdevVolt;       

        public string[] rangeNames = null!;         //Names to be use for table headers
        public float[] rangeMins = null!;           //Saved range mins and maxs
        public float[] rangeMaxs = null!;   
        public int[,] hreg = null!;                 //Similar to root hreg, so multi-1. [multi 1, 2, 3, etc][0=all ranged only, 1=all]
        public string[] hregNames = { "singles", "doubles", "triples", "quads", "quints", "sexts", "septs", "octs", "nanos", "decs" };
        public int[] dpHistogram = null!;
        public int[] singles = null!;               //singles[range, range+1 is other ranges, range+2 is unranged, range+3 is total], 
        public int[] totIonCounts = null!;          //totIonCounts[range as above]
        public int[] multiEventPulsesParticipant = null!;   //multiEventPulsesParticipant[range as above] for PCME calculations.
                                                            //An ion gets credit when range participates in a correlated multi event (2 for double, etc.)?
        public int[,,] dpMultis = null!, dpCorMultis = null!, dpUncMultis = null!; //dpMultis[range1][r2>=r1][dp so DPMax+1]
                                                                                   //dp=0 then dp=1 to including DPMax
        public int[,,,] dpDistanceCorrelations = null!;     //distanceCorrelations[range1][dp][type 0=all, 1=non-same-same, 2=same-same][NDISTBINS]
                                                            //also consider if all ranged or all selected or all ions period

        public class MultiStuff
        {
            public int range;
            public double realPulse;
            public Vector3 coordinate;
        }

        public EIons useSepPlots = EIons.Selected;

        //Initialize
        public MultiHits(IIonData ionData, Vector2[]? values, ObservableCollection<RangesTableEntries> useRanges, ObservableCollection<RangesTableEntries> allRanges, Parameters Parameters)
        {
            if (values == null || useRanges == null || allRanges == null)
                return;

            useSepPlots = Parameters.ESepPlots;

            N = useRanges.Count;
            NTotal = allRanges.Count;

            //Initialize range conversion spectrum: 0..N-1 is range of interest, N is other range, N+1 is unranged
            massSpecturmRes = values[1].X - values[0].X;
            rangeMassSpectrum = new int[values.Length];
            for (int i = 0; i < values.Length; i++) rangeMassSpectrum[i] = N+1;
            foreach (var range in allRanges)
                for (int i = (int)(range.Min / massSpecturmRes); i < (int)(range.Max / massSpecturmRes); i++) rangeMassSpectrum[i] = N;
            int j = 0;
            foreach (var range in useRanges)
            {
                for (int i = (int)(range.Min / massSpecturmRes); i < (int)(range.Max / massSpecturmRes); i++) rangeMassSpectrum[i] = j;
                j++;
            }

            eventPulses = 0;
            totToF = 0d; totVolt = 0d;
            totToFSq = 0d; totVoltSq = 0d;

            //Infer range names
            rangeNames = new string[N + 3];
            rangeNames[N] = "Other";
            rangeNames[N + 1] = "Unranged";
            rangeNames[N + 2] = "Total";
            rangeMins = new float[N];
            rangeMaxs = new float[N];
            j = 0;
            foreach (var range in useRanges)
            {
                rangeNames[j]=$"{(10.0d * range.Pos / 3.0d) * 3f / 10f:N1}-{range.Name}";
                rangeMins[j] = (float)range.Min;
                rangeMaxs[j] = (float)range.Max;
                j++;
            }

            //Parameters now implimented
            keyRange = 0;
            for (int iKeyRange = 0; iKeyRange<useRanges.Count; iKeyRange++)
            {
                if (rangeNames[iKeyRange] == Parameters.SKeyRange)
                {
                    keyRange = iKeyRange;
                    break;
                }
            }
            Parameters.SKeyRange = rangeNames[keyRange];
            critSep = (float)Parameters.DSeparationCriteria; 
            useReconCoordinates = !Parameters.BUseDetectorSeparations;
            DPMax = Parameters.IPseudoMultiMaxdp;

            //Delcare and initialize remaining arrays
            hreg = new int[HREGMax, 2];
            for (int i = 0; i < HREGMax; i++) { hreg[i, 0] = 0; hreg[i, 1] = 0; }

            dpHistogram = new int[DPBins]; //DPBin-1 is overflow
            singles = new int[N + 3];
            totIonCounts = new int[N + 3];
            multiEventPulsesParticipant = new int[N + 3];                            
            dpMultis = new int[N + 3, N + 3, DPMax+1];
            dpCorMultis = new int[N + 3, N + 3, DPMax + 1];
            dpUncMultis = new int[N + 3, N + 3, DPMax + 1];
            dpDistanceCorrelations = new int[N + 3, DPMax+1, 3, NDistBins];
            for (int range1 = 0; range1 < N + 3; range1++)
            {
                singles[range1] = 0;
                totIonCounts[range1] = 0;
                multiEventPulsesParticipant[range1] = 0;
                for (int range2 = 0; range2 < N + 3; range2++)
                {
                    for (int dp = 0; dp < DPMax + 1; dp++)
                    {
                        dpMultis[range1, range2, dp] = 0;
                        dpCorMultis[range1, range2, dp] = 0;
                        dpUncMultis[range1, range2, dp] = 0;
                    }
                }
                for (int dp = 0; dp < DPMax + 1; dp++)
                {
                    for (int distBin = 0; distBin < NDistBins; distBin++)
                    {
                        dpDistanceCorrelations[range1, dp, 0, distBin] = 0;
                        dpDistanceCorrelations[range1, dp, 1, distBin] = 0;
                        dpDistanceCorrelations[range1, dp, 2, distBin] = 0;
                    }
                }
            }
            FillMultisArrays(ionData);
        }

        private void FillMultisArrays(IIonData ionData)
        {
            //A reconstruction makes epos sections that take into account any cuts. e.g., a cut double becomes a single
            //A ROI just deletes lines, so a cut double could be a single with a missing record, or a dangling multi record
            //Need to track the actual pulse number to determine multis in a ROI
            //A user should save the EPOS sections as well as pulse and pulseDelta
            //pulseDelta is added for potential overflow of pulse counts in a float

            List<MultiStuff> multis = new List<MultiStuff>();
            foreach (var chunk in ionData.CreateSectionDataEnumerable("pulse","pulseDelta","Mass","Voltage","Epos ToF","Position","Detector Coordinates"))
            {
                /* Epos sections:
                    The potential names in the code have a long history. 
                    Many are holdovers for backwards compatibility with root.
                    The ones shown in IVAS cannot necessarily be relied upon.

                IVAS:
                    Aperture Voltage(V):            “Vap”
                    Detector Coordinates(mm):       “Detector Coordinates”
                    Mass to Charge State Ratio(Da): “mass”
                    Multiplicity:                   “Multiplicity”
                    Pulses since last event:        “Delta Pulse”
                    Reconstructed Position (nm):    “Position”
                    Specimen Voltage(V):             “v”
                    T0 subtracted Raw Time of Flight(ns): “Epos ToF”

                    Multiplicity and Delta Pulse are defined for the first item in a multiple.
                    The remaining events in the multiple will be 0, 0
                    But this is not reliable for IVAS ROIs

                    To get the precise pulse number for all ROI ions, the formula is:
                    double realPulse = (double)pulse + (double)pulseDelta;
                */

                //These have been checked before calling this method
                var pulses = chunk.ReadSectionData<float>("pulse");
                var pulsesDelta = chunk.ReadSectionData<short>("pulseDelta");
                var masses = chunk.ReadSectionData<float>("Mass");
                var voltages = chunk.ReadSectionData<float>("Voltage");
                var tofs = chunk.ReadSectionData<float>("Epos ToF");
                var reconCoordinates = chunk.ReadSectionData<Vector3>("Position");
                var detCoordinates = chunk.ReadSectionData<Vector2>("Detector Coordinates");

                if (useReconCoordinates)
                {
                    // Only know when any event is complete by comparing to previous
                    MultiStuff multiStuff = GetMultiStuff(pulses.Span[0], pulsesDelta.Span[0], masses.Span[0], reconCoordinates.Span[0]);
                    if (multiStuff.range == keyRange)
                    {
                        totToF += (double)tofs.Span[0]; totVolt += (double)voltages.Span[0];
                        totToFSq += Math.Pow((double)tofs.Span[0], 2); totVoltSq += Math.Pow((double)voltages.Span[0], 2); totKeyRangeCount++;
                    }
                    ProcessMultiStuffFirstPass(multis, multiStuff);

                    for (int i = 1; i < chunk.Length; i++)
                    {
                        multiStuff = GetMultiStuff(pulses.Span[i], pulsesDelta.Span[i], masses.Span[i], reconCoordinates.Span[i]);
                        if (multiStuff.range == keyRange)
                        {
                            totToF += (double)tofs.Span[i]; totVolt += (double)voltages.Span[i];
                            totToFSq += Math.Pow((double)tofs.Span[i], 2); totVoltSq += Math.Pow((double)voltages.Span[i], 2); totKeyRangeCount++;
                        }
                        ProcessMultiStuff(multis, multiStuff);
                    }
                }
                else
                {
                    Vector3[] coordinates = new Vector3[chunk.Length];
                    for (int i = 0; i < chunk.Length; i++)
                    {
                        coordinates[i].X = detCoordinates.Span[i].X;
                        coordinates[i].Y = detCoordinates.Span[i].Y;
                        coordinates[i].Z = 0f;
                    }
                    // Only know when any event is complete by comparing to previous
                    MultiStuff multiStuff = GetMultiStuff(pulses.Span[0], pulsesDelta.Span[0], masses.Span[0], coordinates[0]);
                    if (multiStuff.range == keyRange)
                    {
                        totToF += (double)tofs.Span[0]; totVolt += (double)voltages.Span[0];
                        totToFSq += Math.Pow((double)tofs.Span[0], 2); totVoltSq += Math.Pow((double)voltages.Span[0], 2); totKeyRangeCount++;
                    }
                    ProcessMultiStuffFirstPass(multis, multiStuff);

                    for (int i = 1; i < chunk.Length; i++)
                    {
                        multiStuff = GetMultiStuff(pulses.Span[i], pulsesDelta.Span[i], masses.Span[i], coordinates[i]);
                        if (multiStuff.range == keyRange)
                        {
                            totToF += (double)tofs.Span[i]; totVolt += (double)voltages.Span[i];
                            totToFSq += Math.Pow((double)tofs.Span[i], 2); totVoltSq += Math.Pow((double)voltages.Span[i], 2); totKeyRangeCount++;
                        }
                        ProcessMultiStuff(multis, multiStuff);
                    }
                }
            }
            ProcessMultiStuffLastPass(multis);

            //Add in the totals...
            //N is other, N+1 is unranged, N+2 is the totals column
            //N+2, N+2 should be total of everything
            for (int dp = 0; dp < DPMax + 1; dp++)
            {
                for (int i = 0; i < N + 2; i++)
                {
                    for (int j = 0; j < N + 2; j++)
                    {
                        dpMultis[i, N + 2, dp] += dpMultis[i, j, dp]; //i row total
                        dpMultis[N + 2, j, dp] += dpMultis[i, j, dp]; //j column total
                        dpCorMultis[i, N + 2, dp] += dpCorMultis[i, j, dp]; 
                        dpCorMultis[N + 2, j, dp] += dpCorMultis[i, j, dp];
                        dpUncMultis[i, N + 2, dp] += dpUncMultis[i, j, dp];
                        dpUncMultis[N + 2, j, dp] += dpUncMultis[i, j, dp];
                    }
                    dpMultis[N + 2, N + 2, dp] += dpMultis[i, N + 2, dp];
                    dpCorMultis[N + 2, N + 2, dp] += dpCorMultis[i, N + 2, dp];
                    dpUncMultis[N + 2, N + 2, dp] += dpUncMultis[i, N + 2, dp];
                }
            }
            for (int range = 0; range < N + 2; range++)
            {
                singles[N + 2] += singles[range];
                totIonCounts[N + 2] += totIonCounts[range];
            }

            aveToF = (float)(totToF / (double)totKeyRangeCount);
            aveVolt = (float)(totVolt / (double)totKeyRangeCount);
            stdevToF = (float)(Math.Sqrt( (totToFSq/(double)totKeyRangeCount - (double)aveToF*(double)aveToF) ) );
            stdevVolt = (float)(Math.Sqrt( (totVoltSq/(double)totKeyRangeCount - (double)aveVolt*(double)aveVolt) ) );
            aveDR = (float)((double)totIonCounts[N+2]/(pulseLast - pulseFirst)); //Based on all counts...do we want that or more toward uncorrelated events?
        }
        
        private MultiStuff GetMultiStuff(float pulse, short pulseDelta, float mass, Vector3 coordinate)
        {
            MultiStuff multiStuff = new MultiStuff();
            int bin = (int)(mass / massSpecturmRes);
            multiStuff.range = rangeMassSpectrum[bin];
            multiStuff.realPulse = (double)pulse + (double)pulseDelta;
            multiStuff.coordinate = coordinate;
        
            return multiStuff;
        }
        
        private void ProcessMultiStuffFirstPass(List<MultiStuff> multis, MultiStuff multiStuff)
        {
            totIonCounts[multiStuff.range]++;
            if (!multis.Any()) // Start of new event, only possible if first chunk
            {
                pulseFirst = multiStuff.realPulse;
                eventPulses++;
                multis.Add(multiStuff);
            }
            else // Continuation from previous chunk
            {
                if (multis.Last().realPulse == multiStuff.realPulse) // At least 2nd of multi
                {
                    foreach (var multi in multis)
                    {
                        // Add dp=0 multi pair
                        dpMultis[multi.range, multiStuff.range, 0]++;

                        // Add dp=0 cor or uncor multi pair
                        bool cor = false;
                        int sep = getBinSeparation(multi.coordinate, multiStuff.coordinate, ref cor);
                        if (cor) dpCorMultis[multi.range, multiStuff.range, 0]++;
                        else dpUncMultis[multi.range, multiStuff.range, 0]++;

                        // Add to dp=0 sep plot (all, not-same-same and same-same)
                        //[range1, dp, type, distBin]
                        filldpDistanceCorrelations(multi.range, multiStuff.range, 0, sep, useSepPlots);
                    }
                    multis.Add(multiStuff);
                }
                else // Start of new event
                {
                    if (multis.Count() == 1) //Previous was a Single
                    {
                        if (lastLastWasSingle) //Previous was a single and so was one before -- a pseudo pair
                        {
                            int dp = (int)(multis.Last().realPulse - lastLastSingleMultiStuff.realPulse);
                            if (dp < DPBins)
                                dpHistogram[dp]++;
                            else
                                dpHistogram[DPBins - 1]++;

                            if (dp <= DPMax) //Pseudo double, ignore >DPMax
                            {
                                // Add dp multi pair
                                bool lastLastSmall = lastLastSingleMultiStuff.range < multis.Last().range;
                                if (lastLastSmall)
                                    dpMultis[lastLastSingleMultiStuff.range, multis.Last().range, dp]++;
                                else
                                    dpMultis[multis.Last().range, lastLastSingleMultiStuff.range, dp]++;

                                // Add dp cor or uncor multi pair
                                bool cor = false;
                                int sep = getBinSeparation(lastLastSingleMultiStuff.coordinate, multis.Last().coordinate, ref cor);
                                if (lastLastSmall)
                                {
                                    if (cor) dpCorMultis[lastLastSingleMultiStuff.range, multis.Last().range, dp]++;
                                    else dpUncMultis[lastLastSingleMultiStuff.range, multis.Last().range, dp]++;
                                }
                                else
                                {
                                    if (cor) dpCorMultis[multis.Last().range, lastLastSingleMultiStuff.range, dp]++;
                                    else dpUncMultis[multis.Last().range, lastLastSingleMultiStuff.range, dp]++;
                                }

                                // Add to dp sep plot (all, not-same-same and same-same)
                                //[range1, dp, type, distBin]
                                //type all=0
                                filldpDistanceCorrelations(lastLastSingleMultiStuff.range, multis.Last().range, dp, sep, useSepPlots);
                            }
                        }
                        lastLastWasSingle = true;
                        lastLastSingleMultiStuff = multis.Last();
                        singles[multis.Last().range]++;
                        hreg[0, 1]++; //All single
                        if (multis.Last().range < N) hreg[0, 0]++; //Ranged single
                    }
                    else //Previous was last of multi
                    {
                        dpHistogram[0] += multis.Count();

                        lastLastWasSingle = false;
                        if (multis.Count() > HREGMax)
                            hreg[HREGMax - 1, 1]++; //All, last is HREGMax and bigger
                        else
                            hreg[multis.Count() - 1, 1]++; //All
                        int ranged = 0;
                        foreach (var multi in multis) if (multi.range < N) ranged++;
                        if (ranged > 0)
                        {
                            if (ranged > HREGMax)
                                hreg[HREGMax - 1, 0]++; //Ranged
                            else
                                hreg[ranged - 1, 0]++; //Ranged
                        }
                    }
                    multis.Clear();
                    eventPulses++;
                    multis.Add(multiStuff);
                }
            }
        }
        
        private void ProcessMultiStuff(List<MultiStuff> multis, MultiStuff multiStuff)
        {
            totIonCounts[multiStuff.range]++;
            // At least 2nd of multi
            if (multis.Last().realPulse == multiStuff.realPulse) 
            {
                foreach (var multi in multis)
                {
                    // Add dp=0 multi pair
                    dpMultis[multi.range, multiStuff.range, 0]++;

                    // Add dp=0 cor or uncor multi pair
                    bool cor = false;
                    int sep = getBinSeparation(multi.coordinate, multiStuff.coordinate, ref cor);
                    if (cor) dpCorMultis[multi.range, multiStuff.range, 0]++;
                    else dpUncMultis[multi.range, multiStuff.range, 0]++;

                    // Add to dp=0 sep plot (all, not-same-same and same-same)
                    //[range1, dp, type, distBin]
                    //type all=0
                    filldpDistanceCorrelations(multi.range, multiStuff.range, 0, sep, useSepPlots);
                }
                multis.Add(multiStuff);
            }
            // Start of new event, process multis list
            else
            {
                //Previous was a Single
                if (multis.Count() == 1) 
                {
                    int multisLastRange = multis.Last().range;
                    if (lastLastWasSingle) //Previous was a single and so was one before -- a pseudo pair
                    {
                        int dp = (int)(multis.Last().realPulse - lastLastSingleMultiStuff.realPulse);
                        if (dp < DPBins)
                            dpHistogram[dp]++;
                        else
                            dpHistogram[DPBins - 1]++;

                        if (dp <= DPMax) //Pseudo double, ignore >DPMax
                        {
                            // Add dp multi pair
                            bool lastLastSmall = lastLastSingleMultiStuff.range < multisLastRange;
                            if (lastLastSmall)
                                dpMultis[lastLastSingleMultiStuff.range, multisLastRange, dp]++;
                            else
                                dpMultis[multisLastRange, lastLastSingleMultiStuff.range, dp]++;

                            // Add dp cor or uncor multi pair
                            bool cor = false;
                            int sep = getBinSeparation(lastLastSingleMultiStuff.coordinate, multis.Last().coordinate, ref cor);
                            if (lastLastSmall)
                            {
                                if (cor) dpCorMultis[lastLastSingleMultiStuff.range, multisLastRange, dp]++;
                                else dpUncMultis[lastLastSingleMultiStuff.range, multisLastRange, dp]++;
                            }
                            else
                            {
                                if (cor) dpCorMultis[multisLastRange, lastLastSingleMultiStuff.range, dp]++;
                                else dpUncMultis[multisLastRange, lastLastSingleMultiStuff.range, dp]++;
                            }

                            // Add to dp sep plot (all, not-same-same and same-same)
                            //[range1, dp, type, distBin]
                            //type all=0
                            filldpDistanceCorrelations(lastLastSingleMultiStuff.range, multisLastRange, dp, sep, useSepPlots);
                        }
                    }
                    lastLastWasSingle = true;
                    lastLastSingleMultiStuff = multis.Last();
                    singles[multisLastRange]++;
                    hreg[0, 1]++; //All single
                    if (multisLastRange < N) hreg[0, 0]++; //Ranged single
                }
                //Previous was last of multi
                else
                {
                    // Add to the dp=0 part of dpHistogram
                    dpHistogram[0] += multis.Count();

                    // Not a single so set lastLastSingle to false
                    lastLastWasSingle = false;

                    // Add to hreg all and ranged
                    if (multis.Count() > HREGMax)
                        hreg[HREGMax - 1, 1]++; //All, last is HREGMax and bigger
                    else
                        hreg[multis.Count() - 1, 1]++; //All
                    int ranged = 0;
                    foreach (var multi in multis) if (multi.range < N) ranged++;
                    if (ranged > 0)
                    {
                        if (ranged > HREGMax)
                            hreg[HREGMax - 1, 0]++; //Ranged
                        else
                            hreg[ranged - 1, 0]++; //Ranged
                    }
                }
                multis.Clear();
                eventPulses++;
                multis.Add(multiStuff);
            }
        }
        
        private void ProcessMultiStuffLastPass(List<MultiStuff> multis)
        {
            // Last event, all calcs for multis already done, no calcs for single
            if (multis.Count() == 1) //Previous was a Single
            {
                int dp = (int)(multis.Last().realPulse - lastLastSingleMultiStuff.realPulse);
                if (dp < DPBins)
                    dpHistogram[dp]++;
                else
                    dpHistogram[DPBins - 1]++;

                singles[multis.Last().range]++;
                hreg[0, 1]++; //All
                if (multis.Last().range < N) hreg[0, 0]++; //Ranged
            }
            else //Previous was last of multi
            {
                dpHistogram[0] += multis.Count();

                if (multis.Count() > HREGMax)
                    hreg[HREGMax - 1, 1]++; //All, last is HREGMax and bigger
                else
                    hreg[multis.Count() - 1, 1]++; //All
                int ranged = 0;
                foreach (var multi in multis) if (multi.range < N) ranged++;
                if (ranged > 0)
                {
                    if (ranged > HREGMax)
                        hreg[HREGMax - 1, 0]++; //Ranged
                    else
                        hreg[ranged - 1, 0]++; //Ranged
                }
            }
            pulseLast = multis.Last().realPulse;
            multis.Clear();
        }
        
        private int getBinSeparation(Vector3 p1, Vector3 p2, ref bool cor)
        {
            cor = false;
            double seperation = Math.Sqrt( Math.Pow(p2.X-p1.X,2) + Math.Pow(p2.Y - p1.Y, 2) + Math.Pow(p2.Z - p1.Z, 2));
            if (seperation <= critSep) cor = true;
            return (int)(seperation / DistRes);
        }

        public string MultisSummaryString(Parameters Parameters)
        {
            string  Overview =  "Statistics are tracked for various groups of ions:\n";
                    Overview += "  Considered:   Specific ranges to include in summary table.\n";
                    Overview += "  Key Range:    Range to track computation of average values for ToF and Voltage.\n";
                    Overview += "  Other:        All other defined ranges (including Discovered).\n";
                    Overview += "  Unranged:     All ions between any defined ranges.\n";
                    Overview += "  Correlated:   Multi-hit ions that have seprations smaller than the critical value.\n";
                    Overview += "  Uncorrelated: Multi-hit ions that have sepratation larger than the critical value.\n";
                    Overview += "  Pseudo-multi: Consecutive sigle-ion events only, tracked for delta pulse (dp) values\n";
                    Overview += "                out to some maximum dp time/pulse separation value.\n";
                    Overview += "\n";

            string UncorrelatedTable = $"Uncorrelated Multis Table All: dpMultis[First Ion,Second Ion,dp=0]\n{"",13}";
            for (int i = 0; i < N + 3; i++)
                UncorrelatedTable += $"{rangeNames[i],13}";
            UncorrelatedTable += "\n";
            for (int i = 0; i < N + 3; i++)
            {
                UncorrelatedTable += $"{rangeNames[i],13}";
                for (int j = 0; j < N + 3; j++)
                    UncorrelatedTable += $"{dpUncMultis[i, j, 0],13:N0}";
                UncorrelatedTable += "\n";
            }
            UncorrelatedTable += "\n";

            string CorrelatedTable = $"Correlated Multis Table All: dpMultis[First Ion,Second Ion,dp=0]\n{"",13}";
            for (int i = 0; i < N+3; i++)
                CorrelatedTable += $"{rangeNames[i],13}";
            CorrelatedTable += "\n";
            for (int i = 0; i < N + 3; i++)
            {
                CorrelatedTable += $"{rangeNames[i],13}";
                for (int j = 0; j < N + 3; j++)
                    CorrelatedTable += $"{dpCorMultis[i,j,0],13:N0}";
                CorrelatedTable += "\n";
            }
            CorrelatedTable += "\n";

            string ConsideredRanges = $"Considered Ranges:         {N,5:N0}\n";
            for (int i = 0; i<rangeMins.Count(); i++)
                ConsideredRanges += $"                       {i} {rangeNames[i],7}: {rangeMins[i],7:N3} - {rangeMaxs[i],7:N3}\n";

            string  Summary =  $"Total Defined Ranges:      {NTotal,5:N0}\n";
                    Summary += $"Key Range:                 {rangeNames[keyRange],7}: {rangeMins[keyRange],7:N3} - {rangeMaxs[keyRange],7:N3}\n";
                    Summary += $"{ConsideredRanges}";
                    Summary += $"Separation Critical Value:   {critSep:N1}\n";
                    Summary += $"Pseudo-Multi Max dp:           {DPMax:N0}\n";
                    Summary += $"\n";

            //hreg 0 = ranged, 1 = all
            string HregSummary = "\nMulti-dp=0 Distribution:   ";
            for (int i = 0; i < HREGMax-1; i++)
                HregSummary += $"{hregNames[i],13}";
            HregSummary += $"{"higher",13}"; //hreg should have single, doubles, ... HREGMax-1 contains HREGMax and larger multiples
            HregSummary += $"{"total",13}";
            
            HregSummary +=     "\n      All Events:          ";
            for (int i = 0; i < HREGMax; i++)
                HregSummary += $"{hreg[i, 1],13:N0}";
            HregSummary += $"{eventPulses,13:N0}";
            
            HregSummary +=     "\n      All Weighted:        ";
            for (int i = 0; i < HREGMax-1; i++)
                HregSummary += $"{hreg[i, 1]*(i+1),13:N0}";
            int totalWeighted = 0;
            for (int i = 1; i < HREGMax - 1; i++) totalWeighted += hreg[i, 1] * (i + 1);
            //dpHistogram[0] is total number of dp=0 multis
            HregSummary += $"{dpHistogram[0] - totalWeighted,13:N0}";
            HregSummary += $"{hreg[0,1] + dpHistogram[0],13:N0}";

            float norm = (float)(hreg[0, 1] + dpHistogram[0]);
            HregSummary += "\n      All Weighted:        ";
            for (int i = 0; i < HREGMax - 1; i++)
                HregSummary += $"{(float)(hreg[i, 1] * (i + 1))/norm,13:P2}";
            HregSummary += $"{(float)(dpHistogram[0] - totalWeighted)/norm,13:P2}";
            HregSummary += $"{(float)(hreg[0, 1] + dpHistogram[0])/norm,13:P0}";

            HregSummary += "\n      Considered Events:   ";
            for (int i = 0; i < HREGMax; i++)
                HregSummary += $"{hreg[i, 0],13:N0}";
            HregSummary += $"{totIonCounts[N + 2] - totIonCounts[N + 1] - totIonCounts[N],13:N0}";
            HregSummary +=     "\n";

            Summary += $"Total Event Pulses:    {eventPulses,17:N0}\n";
            Summary += $"Total Ions:            {totIonCounts[N+2],17:N0}\n";
            Summary += $"Total Multi Ions:      {dpHistogram[0],17:N0}\n";
            Summary += $"Total Multis Table:    {dpMultis[N + 2, N + 2, 0],17:N0}\n";
            Summary += $"{HregSummary}";
            Summary += "\n";

            //dpMultis[N+3][N+3][DPMax+1]
            //dpMultis[range1][r2>=r1][dp so DPMax+1] 0, 1, ... DPMax
            Summary += $"Multis dp=0:\n";
            Summary += $"  All:                     {dpMultis[N + 2, N + 2, 0],13:N0}\n";
            Summary += $"  Considered:              {getConsideredTotal(dpMultis, 0),13:N0}\n";
            Summary += $"  Considered & Correlated: {getConsideredTotal(dpCorMultis, 0),13:N0}\n";
            Summary += $"  Considered & Uncorr:     {getConsideredTotal(dpUncMultis, 0),13:N0}\n";
            Summary += "\n";
            Summary +=  $"Pseudo-Doubles dp=1...{DPMax:N0}:\n";
            int sum = 0; for (int dp = 1; dp <= DPMax; dp++) sum += dpMultis[N + 2, N + 2, dp];
            Summary += $"  All:                     {sum,13:N0}\n";
            sum = 0; for (int dp = 1; dp <= DPMax; dp++) sum += (getConsideredTotal(dpMultis, dp));
            Summary += $"  Considered:              {sum,13:N0}\n";
            sum = 0; for (int dp = 1; dp <= DPMax; dp++) sum += (getConsideredTotal(dpCorMultis, dp));
            Summary += $"  Considered & Correlated: {sum,13:N0}\n";
            sum = 0; for (int dp = 1; dp <= DPMax; dp++) sum += (getConsideredTotal(dpUncMultis, dp));
            Summary += $"  Considered & UnCorr:     {sum,13:N0}\n";
            Summary += "\n";
            Summary += $"DR:          {aveDR:P3}\n";
            Summary += $"ToF:       {aveToF:N0} \u00B1 {stdevToF:N0} ns\n";
            Summary += $"Voltage: {aveVolt:N0} \u00B1 {stdevVolt:N0} V\n";
            Summary += "\n";

            float SS0 = (float)(getSSConsideredTotal(dpCorMultis, 0));
            float SSp0 = (float)(getSSpConsideredTotal(dpCorMultis, 0));
            float SSp1 = (float)(getSSpConsideredTotal(dpCorMultis, 1));
            float SS1 = (float)(getSSConsideredTotal(dpCorMultis, 1));
            float SS0FractionDetected = SS0 / SSp0 * SSp1 / SS1;
            Summary += "S=Same, S'=Not Same, 0: dp=0 or same pulse, 1: dp=1 or adjacent pulses\n";
            Summary += $"SS0:  {SS0,13:N0}\n";
            Summary += $"SS'0: {SSp0,13:N0}\n";
            Summary += $"SS1:  {SS1,13:N0}\n";
            Summary += $"SS'1: {SSp1,13:N0}\n";
            Summary += $"Corr: SS0/SS'0 / SS1/SS'1 = {SS0FractionDetected:P2}\n";
            Summary += $"Correlated same-same/not-same ratio for same-pulse multis vs. psudo-multis\n";
            Summary += $"(Deadtime affected/not deadtime affected same-pulse vs. same ratio with no deadtime effect)\n\n";

            SS0 = (float)(getSSConsideredTotal(dpUncMultis, 0));
            SSp0 = (float)(getSSpConsideredTotal(dpUncMultis, 0));
            SSp1 = (float)(getSSpConsideredTotal(dpUncMultis, 1));
            SS1 = (float)(getSSConsideredTotal(dpUncMultis, 1));
            SS0FractionDetected = SS0 / SSp0 * SSp1 / SS1;
            Summary += $"SS0:  {SS0,13:N0}\n";
            Summary += $"SS'0: {SSp0,13:N0}\n";
            Summary += $"SS1:  {SS1,13:N0}\n";
            Summary += $"SS'1: {SSp1,13:N0}\n";
            Summary += $"Uncorr: SS0/SS'0 / SS1/SS'1 = {SS0FractionDetected:P2}\n";
            Summary += $"Uncorrelated same-same/not-same ratio for same-pulse multis vs. psudo-multis\n";
            Summary += $"(mostly unaffected/not deadtime affected same-pulse vs. same ratio with no deadtime effect)\n";
            Summary += $"(these are approximately predictable, governed mainly by Poisson statistics --> 100%)\n";
            Summary += "\n";

            string UncorrelatedPseudoTable = $"Uncorrelated Pseudo-Multis Table All: dpMultis[First Ion,Second Ion,dp=1...{Parameters.IPseudoMultiMaxdp}]\n{"",13}";
            for (int i = 0; i < N + 3; i++)
                UncorrelatedPseudoTable += $"{rangeNames[i],13}";
            UncorrelatedPseudoTable += "\n";
            for (int i = 0; i < N + 3; i++)
            {
                UncorrelatedPseudoTable += $"{rangeNames[i],13}";
                for (int j = 0; j < N + 3; j++)
                    UncorrelatedPseudoTable += $"{dpUncMultis[i, j, Parameters.IPseudoMultiMaxdp],13:N0}";
                UncorrelatedPseudoTable += "\n";
            }
            UncorrelatedPseudoTable += "\n";

            string CorrelatedPseudoTable = $"Correlated Pseudo-Multis Table All: dpMultis[First Ion,Second Ion,dp=1...{Parameters.IPseudoMultiMaxdp}]\n{"",13}";
            for (int i = 0; i < N + 3; i++)
                CorrelatedPseudoTable += $"{rangeNames[i],13}";
            CorrelatedPseudoTable += "\n";
            for (int i = 0; i < N + 3; i++)
            {
                CorrelatedPseudoTable += $"{rangeNames[i],13}";
                for (int j = 0; j < N + 3; j++)
                    CorrelatedPseudoTable += $"{dpCorMultis[i, j, Parameters.IPseudoMultiMaxdp],13:N0}";
                CorrelatedPseudoTable += "\n";
            }
            CorrelatedPseudoTable += "\n";

            return Overview + UncorrelatedTable + CorrelatedTable + Summary + UncorrelatedPseudoTable + CorrelatedPseudoTable;
        }
    
        public int getConsideredTotal(int[,,] array, int dp)
        {
            int other = array[N, N + 2, dp] + array[N + 2, N, dp] - array[N, N, dp];
            int unranged = array[N + 1, N + 2, dp] + array[N + 2, N + 1, dp] - array[N + 1, N + 1,dp];
            int correct = array[N, N + 1, dp] + array[N + 1, N, dp];
            return array[N + 2, N + 2,dp] - other - unranged + correct;
        }

        public int getSSConsideredTotal(int[,,] array, int dp)
        {
            int sum = 0;
            for (int i = 0; i < N; i++)
                sum += array[i, i, dp];
            return sum;
        }

        public int getSSpConsideredTotal(int[,,] array, int dp)
        {
            return getConsideredTotal(array,dp) - getSSConsideredTotal(array,dp);
        }
        
        public bool includeInSepPlot(int range1, int range2, EIons useSepPlots)
        {
            if (useSepPlots.Equals(EIons.All))
            {
                return true;
            }
            else if (useSepPlots.Equals(EIons.Selected))
            {
                if (range1 < N && range2 < N) return true;
            }
            else if (useSepPlots.Equals(EIons.SelectedAndOthers))
            {
                if (range1 < N + 1 && range2 < N + 1) return true;
            }

            return false;
        }
        
        public void filldpDistanceCorrelations(int range1, int range2, int dp, int sep, EIons useSepPlots)
        {
            if (includeInSepPlot(range1, range2, useSepPlots))
            {
                //type all=0
                dpDistanceCorrelations[range1, dp, 0, sep]++;
                dpDistanceCorrelations[range2, dp, 0, sep]++;
                //type not-same-same=1
                if (range1 != range2)
                {
                    dpDistanceCorrelations[range1, dp, 1, sep]++;
                    dpDistanceCorrelations[range2, dp, 1, sep]++;
                }
                //type same-same=2
                else
                {
                    dpDistanceCorrelations[range1, dp, 2, sep]++;
                    //Do not double count
                    //dpDistanceCorrelations[range2, dp, 2, sep]++;
                }
            }
            return;
        }
    }
}