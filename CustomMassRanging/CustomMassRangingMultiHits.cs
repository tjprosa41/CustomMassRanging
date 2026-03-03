using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;

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
        public int[,,] dpMultis = null!, dpCorMultis = null!, dpUncMultis = null!; //dpMultis[range1][r2>=r1][dp so DPMax+2]
                                                                                   //dp=0 then dp=1 to including DPMax and DPMax+1 has totals
        public int[,,,] dpDistanceCorrelations = null!;     //distanceCorrelations[range1][dp][type 0=all, 1=non-same-same, 2=same-same][NDISTBINS]
                                                            //also consider if all ranged or all selected or all ions period
        public int[,] multiIonTrueCounts = null!;              //multiIonTrueCounts[range][0=correlated, 1=uncorrelated]
                                                               //non-double counted multi-ions
        public int[] missingCounts = null!;
        public string[] missingPairs = null!;
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
            for (int i = 0; i < values.Length; i++) rangeMassSpectrum[i] = N + 1;
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
                rangeNames[j] = $"{(10.0d * range.Pos / 3.0d) * 3f / 10f:N1}-{range.Name}";
                rangeMins[j] = (float)range.Min;
                rangeMaxs[j] = (float)range.Max;
                j++;
            }

            //Parameters now implimented
            keyRange = 0;
            for (int iKeyRange = 0; iKeyRange < useRanges.Count; iKeyRange++)
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
            dpMultis = new int[N + 3, N + 3, DPMax + 2];
            dpCorMultis = new int[N + 3, N + 3, DPMax + 2];
            dpUncMultis = new int[N + 3, N + 3, DPMax + 2];
            dpDistanceCorrelations = new int[N + 3, DPMax + 2, 3, NDistBins];
            multiIonTrueCounts = new int[N + 3, 2];
            for (int range1 = 0; range1 < N + 3; range1++)
            {
                singles[range1] = 0;
                totIonCounts[range1] = 0;
                multiIonTrueCounts[range1, 0] = 0;
                multiIonTrueCounts[range1, 1] = 0;
                for (int range2 = 0; range2 < N + 3; range2++)
                {
                    for (int dp = 0; dp < DPMax + 2; dp++)
                    {
                        dpMultis[range1, range2, dp] = 0;
                        dpCorMultis[range1, range2, dp] = 0;
                        dpUncMultis[range1, range2, dp] = 0;
                    }
                }
                for (int dp = 0; dp < DPMax + 2; dp++)
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
            foreach (var chunk in ionData.CreateSectionDataEnumerable("pulse", "pulseDelta", "Mass", "Voltage", "Epos ToF", "Position", "Detector Coordinates"))
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

                        dpMultis[i, j, DPMax + 1] += dpMultis[i, j, dp];
                        dpCorMultis[i, j, DPMax + 1] += dpCorMultis[i, j, dp];
                        dpUncMultis[i, j, DPMax + 1] += dpUncMultis[i, j, dp];

                        dpMultis[i, N + 2, DPMax + 1] += dpMultis[i, j, dp];
                        dpCorMultis[i, N + 2, DPMax + 1] += dpCorMultis[i, j, dp];
                        dpUncMultis[i, N + 2, DPMax + 1] += dpUncMultis[i, j, dp];

                        dpMultis[N + 2, j, DPMax + 1] += dpMultis[i, j, dp];
                        dpCorMultis[N + 2, j, DPMax + 1] += dpCorMultis[i, j, dp];
                        dpUncMultis[N + 2, j, DPMax + 1] += dpUncMultis[i, j, dp];
                    }
                    dpMultis[N + 2, N + 2, dp] += dpMultis[i, N + 2, dp]; // N+2,N+2 is the entire table total
                    dpCorMultis[N + 2, N + 2, dp] += dpCorMultis[i, N + 2, dp];
                    dpUncMultis[N + 2, N + 2, dp] += dpUncMultis[i, N + 2, dp];

                    dpMultis[N + 2, N + 2, DPMax + 1] += dpMultis[i, N + 2, dp]; // N+2,N+2 is the entire table total
                    dpCorMultis[N + 2, N + 2, DPMax + 1] += dpCorMultis[i, N + 2, dp];
                    dpUncMultis[N + 2, N + 2, DPMax + 1] += dpUncMultis[i, N + 2, dp];
                }
            }
            for (int range = 0; range < N + 2; range++)
            {
                singles[N + 2] += singles[range];
                totIonCounts[N + 2] += totIonCounts[range];
                multiIonTrueCounts[N + 2, 0] += multiIonTrueCounts[range, 0];
                multiIonTrueCounts[N + 2, 1] += multiIonTrueCounts[range, 1];
            }

            aveToF = (float)(totToF / (double)totKeyRangeCount);
            aveVolt = (float)(totVolt / (double)totKeyRangeCount);
            stdevToF = (float)(Math.Sqrt((totToFSq / (double)totKeyRangeCount - (double)aveToF * (double)aveToF)));
            stdevVolt = (float)(Math.Sqrt((totVoltSq / (double)totKeyRangeCount - (double)aveVolt * (double)aveVolt)));
            aveDR = (float)((double)totIonCounts[N + 2] / (pulseLast - pulseFirst)); //Based on all counts...do we want that or more toward uncorrelated events?

            fillMissing();
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
        /* PCME - Probability of Correlated Multi-Event ala Oana who only considered PME - but uncorrelated are uninteresting
         *
         * If you consider an ion, what is the probability it originated from a correlated multi-event?
         * Note: PCME (correlated) and PME are approximately the same for high-multiplicity events.
         *       For well-behaved ions, it will be more significant.
         * 
         * Can compute PCME from existing tables:
         *       IonsFromCMultis / (IonsFromCMultis + RemainingIons) or (TotalIons)
         * Check math: Take the totals lines from the row and column for each table 
         *             (will double count same-same, and that is good because it represents two ions)
         *             and singles to get total ions (but there is overcounting).
         *             PCME is then based on what is in the tables...tables include other and unranged.
         *             PCME should include these, otherwise exclude what?
         *             Do I want a PCME where it becomes...
         *             
         *             If you consider a group of ions, what is the probability that an ion originated from a correlated
         *             event with another of these ions?  Sounds like the Saxey version.
         *
         * Note: All combinations of a multi-event are counted in the table: one triple becomes three doubles (6 ions implied not 3)
         *       Accounted for over counting.
         *       
         * Missing Events:
         *       Method 1) Use normalized sep plot (uncorrelated events).
         *       Method 2) Focus on same ion types - cross-correlations.  Iterative?  Correction factor for implants.
         *       Did some over counting before?  M?  Need to check old code.
         * 
         */

        private void ProcessMultiStuffFirstPass(List<MultiStuff> multis, MultiStuff multiStuff)
        {
            totIonCounts[multiStuff.range]++;
            // Start of new event, only possible if first chunk
            if (!multis.Any())
            {
                pulseFirst = multiStuff.realPulse;
                eventPulses++;
                multis.Add(multiStuff);
            }
            // Continuation from previous chunk
            else
            {
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
                        filldpDistanceCorrelations(multi.range, multiStuff.range, 0, sep, useSepPlots);
                    }
                    multis.Add(multiStuff);
                }
                else // Start of new event
                {
                    //Previous was a Single
                    if (multis.Count() == 1)
                    {
                        //Previous was a single and so was one before -- a pseudo pair
                        if (lastLastWasSingle)
                        {
                            int dp = (int)(multis.Last().realPulse - lastLastSingleMultiStuff.realPulse);
                            if (dp < DPBins)
                                dpHistogram[dp]++;
                            else
                                dpHistogram[DPBins - 1]++;

                            //Pseudo double, ignore >DPMax
                            if (dp <= DPMax)
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
                    //Previous was last of multi
                    else
                    {
                        dpHistogram[0] += multis.Count();

                        lastLastWasSingle = false;
                        if (multis.Count() > HREGMax)
                            hreg[HREGMax - 1, 1]++; //All, last is HREGMax and bigger
                        else
                            hreg[multis.Count() - 1, 1]++; //All

                        // Re-process entire multi group for PCME counts
                        // multiIonTrueCounts only counts each multi-ion once
                        int ranged = 0;
                        foreach (var multi1 in multis)
                        {
                            if (multi1.range < N) ranged++;
                            bool notCor = true;
                            foreach (var multi2 in multis)
                            {
                                if (multi1 != multi2)
                                {
                                    bool cor = false;
                                    int sep = getBinSeparation(multi1.coordinate, multi2.coordinate, ref cor);
                                    if (cor)
                                    {
                                        multiIonTrueCounts[multi1.range, 0]++;
                                        notCor = false;
                                        break;
                                    }
                                }
                            }
                            if (notCor) multiIonTrueCounts[multi1.range, 1]++;
                        }

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
                // This loop creates a table entry for each combination - overcounting
                // Note: A multi (triple) could have both correlated and uncorrelated pairs.
                //       These will be placed within the proper tables.
                //       Need to track the true number of ions from correlated events for PCME
                //       Will be correlated with any other event (ranged or not)
                //       Same-same will have two (ok).  Non-same-same will have one.
                //       Sum of multiIonTrueCounts[][] is ions from correlated and uncorrelated events (no overcounting).
                //       Breaking the tie:  If an ion participates in both types of events, only include in correlated TrueCounts.
                //       This means an uncorrelated TrueCount is not correlated with any other ion.
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

                    // Re-process entire multi group for PCME counts
                    int ranged = 0;
                    foreach (var multi1 in multis)
                    {
                        if (multi1.range < N) ranged++;
                        bool notCor = true;
                        foreach (var multi2 in multis)
                        {
                            if (multi1 != multi2)
                            {
                                bool cor = false;
                                int sep = getBinSeparation(multi1.coordinate, multi2.coordinate, ref cor);
                                if (cor)
                                {
                                    multiIonTrueCounts[multi1.range, 0]++;
                                    notCor = false;
                                    break;
                                }
                            }
                        }
                        if (notCor) multiIonTrueCounts[multi1.range, 1]++;
                    }

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
            //Previous was a Single
            if (multis.Count() == 1)
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
            //Previous was last of multi
            else
            {
                dpHistogram[0] += multis.Count();

                if (multis.Count() > HREGMax)
                    hreg[HREGMax - 1, 1]++; //All, last is HREGMax and bigger
                else
                    hreg[multis.Count() - 1, 1]++; //All

                // Re-process entire multi group for PCME counts
                int ranged = 0;
                foreach (var multi1 in multis)
                {
                    if (multi1.range < N) ranged++;
                    bool notCor = true;
                    foreach (var multi2 in multis)
                    {
                        if (multi1 != multi2)
                        {
                            bool cor = false;
                            int sep = getBinSeparation(multi1.coordinate, multi2.coordinate, ref cor);
                            if (cor)
                            {
                                multiIonTrueCounts[multi1.range, 0]++;
                                notCor = false;
                                break;
                            }
                        }
                    }
                    if (notCor) multiIonTrueCounts[multi1.range, 1]++;
                }

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
            double separation = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2) + Math.Pow(p2.Z - p1.Z, 2));
            if (separation <= critSep) cor = true;
            return (int)(separation / DistRes);
        }

        public string MultisSummaryString(Parameters Parameters)
        {
            string Overview = "Overview:\n";
            {
                Overview += "  Occurances are tracked for various groups of ions:\n";
                Overview += "  Considered:   Specific ranges to include in summary table.\n";
                Overview += "  Key Range:    Range to track computation of average values for ToF and Voltage.\n";
                Overview += "  Other:        All other defined ranges (in ranges table--discovered only if user indicates).\n";
                Overview += "  Unranged:     All ions between any defined ranges.\n";
                Overview += "  Correlated:   Multi-hit ions that have seprations smaller than the critical value.\n";
                Overview += "  Uncorrelated: Multi-hit ions that have sepratation larger than the critical value.\n";
                Overview += "  Pseudo-multi: Consecutive sigle-ion events only, tracked for delta pulse (dp) values\n";
                Overview += "                out to some maximum dp time/pulse separation value.\n";
                Overview += "\n";
                Overview += $"  Note: Double events in tables represent any pair of events--there is over representation\n";
                Overview += $"    because all multi-events considered are converted to interaction pairs or doubles\n";
                Overview += $"    (e.g., triples converted to three pairs in multis tables).\n";
                Overview += $"    Overcounting not possible with pseudo-double definitions.\n";
                Overview += $"\n";
            }

            string SimpleDescriptors = "Simple Multi-Hit Descriptors:\n";
            {
                SimpleDescriptors += $"{"Oana PME (event probability):",52}";
                SimpleDescriptors += $"{(double)(eventPulses - singles[N + 2]) / (double)eventPulses,13:P2}\n";
                SimpleDescriptors += $"{"All PME (ion origin event probability, ~2xOana PME):",52}";
                SimpleDescriptors += $"{(double)(multiIonTrueCounts[N + 2, 0] + multiIonTrueCounts[N + 2, 1]) / (double)totIonCounts[N + 2],13:P2}\n";
                SimpleDescriptors += $"{"All PCME (ion origin event probability):",52}";
                SimpleDescriptors += $"{(double)multiIonTrueCounts[N + 2, 0] / (double)totIonCounts[N + 2],13:P2}\n";
                SimpleDescriptors += $"{"All PCME Cor.(ion origin event probability):",52}";
                int missingCountsTotal = 0;
                for (int i = 0; i < N; i++) missingCountsTotal += missingCounts[i];
                SimpleDescriptors += $"{(double)(multiIonTrueCounts[N + 2, 0] + missingCountsTotal) / (double)(totIonCounts[N + 2] + missingCountsTotal),13:P2}\n";

                SimpleDescriptors += $"\n";
            }

            string Summary = "Summary:\n";
            {
                string ConsideredRanges = $"Considered Ranges:     {N,5:N0}\n";
                for (int i = 0; i < rangeMins.Count(); i++)
                    ConsideredRanges += $"                           {i} {rangeNames[i],7}: {rangeMins[i],7:N3} - {rangeMaxs[i],7:N3}\n";

                Summary += $"  Total Defined Ranges:  {NTotal,5:N0}\n";
                Summary += $"  Key Range:                   {rangeNames[keyRange],7}: {rangeMins[keyRange],7:N3} - {rangeMaxs[keyRange],7:N3}\n";
                Summary += $"  {ConsideredRanges}";
                Summary += $"  Separation Critical Value: {critSep:N1}\n";
                Summary += $"  Pseudo-Multi Max dp:       {DPMax:N0}\n";
                Summary += $"\n";
                Summary += $"  Multi Events:          {(double)(eventPulses - singles[N + 2]) / (double)(eventPulses),17:P1}\n";
                Summary += $"  Multi Ions:            {(double)(totIonCounts[N + 2] - singles[N + 2]) / (double)(eventPulses),17:P1}\n";
                Summary += $"  Total Event Pulses:    {eventPulses,17:N0}\n";
                Summary += $"  Total Ions:            {totIonCounts[N + 2],17:N0}\n";
                Summary += $"  Total Multi Ions:      {dpHistogram[0],17:N0}\n";
                Summary += $"  Total Multis Table:    {dpMultis[N + 2, N + 2, 0],17:N0}\n";

                //hreg 0 = ranged, 1 = all
                string HregSummary = "\nMulti-dp=0 Distribution:   ";
                {
                    for (int i = 0; i < HREGMax - 1; i++)
                        HregSummary += $"{hregNames[i],13}";
                    HregSummary += $"{"higher",13}"; //hreg should have single, doubles, ... HREGMax-1 contains HREGMax and larger multiples
                    HregSummary += $"{"total",13}";

                    HregSummary += "\n      All Events:          ";
                    for (int i = 0; i < HREGMax; i++)
                        HregSummary += $"{hreg[i, 1],13:N0}";
                    HregSummary += $"{eventPulses,13:N0}";

                    HregSummary += "\n      All Weighted:        ";
                    for (int i = 0; i < HREGMax - 1; i++)
                        HregSummary += $"{hreg[i, 1] * (i + 1),13:N0}";
                    int totalWeighted = 0;
                    for (int i = 1; i < HREGMax - 1; i++) totalWeighted += hreg[i, 1] * (i + 1);
                    //dpHistogram[0] is total number of dp=0 multis
                    HregSummary += $"{dpHistogram[0] - totalWeighted,13:N0}";
                    HregSummary += $"{hreg[0, 1] + dpHistogram[0],13:N0}";

                    float norm = (float)(hreg[0, 1] + dpHistogram[0]);
                    HregSummary += "\n      All Weighted:        ";
                    for (int i = 0; i < HREGMax - 1; i++)
                        HregSummary += $"{(float)(hreg[i, 1] * (i + 1)) / norm,13:P2}";
                    HregSummary += $"{(float)(dpHistogram[0] - totalWeighted) / norm,13:P2}";
                    HregSummary += $"{(float)(hreg[0, 1] + dpHistogram[0]) / norm,13:P0}";

                    HregSummary += "\n      Considered Events:   ";
                    for (int i = 0; i < HREGMax; i++)
                        HregSummary += $"{hreg[i, 0],13:N0}";
                    HregSummary += $"{totIonCounts[N + 2] - totIonCounts[N + 1] - totIonCounts[N],13:N0}";
                    HregSummary += "\n";
                }

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
                Summary += $"Pseudo-Doubles dp=1...{DPMax:N0}:\n";
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

                Summary += "Looking for trends with Pseudo Multis:\n";
                float SS0 = (float)(getSSConsideredTotal(dpCorMultis, 0));
                float SSp0 = (float)(getSSpConsideredTotal(dpCorMultis, 0));
                float SSp1 = (float)(getSSpConsideredTotal(dpCorMultis, 1));
                float SS1 = (float)(getSSConsideredTotal(dpCorMultis, 1));
                float SS0FractionDetected = SS0 / SSp0 * SSp1 / SS1;
                Summary += "  S=Same, S'=Not Same, 0: dp=0 or same pulse, 1: dp=1 or adjacent pulses\n";
                Summary += $"  SS0:  {SS0,13:N0}\n";
                Summary += $"  SS'0: {SSp0,13:N0}\n";
                Summary += $"  SS1:  {SS1,13:N0}\n";
                Summary += $"  SS'1: {SSp1,13:N0}\n";
                Summary += $"  Corr: SS0/SS'0 / SS1/SS'1 = {SS0FractionDetected:P2}\n";
                Summary += $"  Correlated same-same/not-same ratio for same-pulse multis vs. psudo-multis\n";
                Summary += $"  (Deadtime affected/not deadtime affected same-pulse vs. same ratio with no deadtime effect)\n\n";

                SS0 = (float)(getSSConsideredTotal(dpUncMultis, 0));
                SSp0 = (float)(getSSpConsideredTotal(dpUncMultis, 0));
                SSp1 = (float)(getSSpConsideredTotal(dpUncMultis, 1));
                SS1 = (float)(getSSConsideredTotal(dpUncMultis, 1));
                SS0FractionDetected = SS0 / SSp0 * SSp1 / SS1;
                Summary += $"  SS0:  {SS0,13:N0}\n";
                Summary += $"  SS'0: {SSp0,13:N0}\n";
                Summary += $"  SS1:  {SS1,13:N0}\n";
                Summary += $"  SS'1: {SSp1,13:N0}\n";
                Summary += $"  Uncorr: SS0/SS'0 / SS1/SS'1 = {SS0FractionDetected:P2}\n";
                Summary += $"  Uncorrelated same-same/not-same ratio for same-pulse multis vs. psudo-multis\n";
                Summary += $"  (mostly unaffected/not deadtime affected same-pulse vs. same ratio with no deadtime effect)\n";
                Summary += $"  (these are approximately predictable, governed mainly by Poisson statistics --> 100%)\n";
                Summary += "\n";
            }

            double[] DeadCor = new double[N];
            string PCMETable = "Information by Ion Type:\n";
            {
                PCMETable += $"{"",15}";
                for (int i = 0; i < N + 3; i++)
                    PCMETable += $"{rangeNames[i],13}";
                PCMETable += $"\n";

                PCMETable += $"{"PCME:",15}";
                for (int i = 0; i < N + 3; i++)
                    PCMETable += $"{(double)multiIonTrueCounts[i, 0] / (double)(totIonCounts[i]),13:P1}";
                PCMETable += $"\n";

                PCMETable += $"{"PCME Corrected:",15}";
                for (int i = 0; i < N; i++)
                    PCMETable += $"{(double)(multiIonTrueCounts[i, 0] + missingCounts[i]) / (double)(totIonCounts[i] + missingCounts[i]),13:P1}";
                for (int i = N; i < N + 3; i++)
                    PCMETable += $"{(double)multiIonTrueCounts[i, 0] / (double)(totIonCounts[i]),13:P1}";
                PCMETable += $"\n";

                PCMETable += $"{"Deadtime:",15}";
                for (int i = 0; i < N; i++)
                    PCMETable += $"{(double)missingCounts[i] / (double)(missingCounts[i] + totIonCounts[i]),13:P1}";
                PCMETable += $"\n";

                PCMETable += $"{"Pair:",15}";
                for (int i = 0; i < N; i++)
                    PCMETable += $"{missingPairs[i],13}";
                PCMETable += $"\n";
                PCMETable += $"\n";

                PCMETable += $"{"Singles:",15}";
                for (int i = 0; i < N + 3; i++)
                    PCMETable += $"{singles[i],13:N0}";
                PCMETable += $"\n";

                PCMETable += $"{"Correlated:",15}";
                for (int i = 0; i < N + 3; i++)
                    PCMETable += $"{multiIonTrueCounts[i, 0],13:N0}";
                PCMETable += $"\n";

                PCMETable += $"{"Uncorrelated:",15}";
                for (int i = 0; i < N + 3; i++)
                    PCMETable += $"{multiIonTrueCounts[i, 1],13:N0}";
                PCMETable += $"\n";

                PCMETable += $"{"Ion Count:",15}";
                for (int i = 0; i < N + 3; i++)
                    PCMETable += $"{totIonCounts[i],13:N0}";
                PCMETable += $"\n";

                PCMETable += $"{"Missing:",15}";
                for (int i = 0; i < N; i++)
                    PCMETable += $"{missingCounts[i],13:N0}";
                PCMETable += $"\n";
                PCMETable += $"\n";

                double[] Raw = new double[N];
                int totSelected = 0;
                for (int i = 0; i < N; i++) totSelected += totIonCounts[i];
                PCMETable += $"{"Raw Comp.:",15}";
                for (int i = 0; i < N; i++)
                {
                    Raw[i] = (double)totIonCounts[i] / (double)totSelected;
                    PCMETable += $"{Raw[i],13:P4}";
                }
                PCMETable += $"\n";

                totSelected = 0;
                for (int i = 0; i < N; i++) totSelected += totIonCounts[i] + missingCounts[i];
                PCMETable += $"{"Deadtime Cor.:",15}";
                for (int i = 0; i < N; i++)
                {
                    DeadCor[i] = (double)(totIonCounts[i] + missingCounts[i]) / (double)totSelected;
                    PCMETable += $"{DeadCor[i],13:P4}";
                }
                PCMETable += $"\n";

                PCMETable += $"{"Cor. Factor:",15}";
                for (int i = 0; i < N; i++)
                    PCMETable += $"{((double)(totIonCounts[i] + missingCounts[i]) / (double)totSelected) / Raw[i],13:N3}";
                PCMETable += $"\n";
                PCMETable += $"\n";
            }

            string CorrelatedTable = $"Correlated Multis Table All Multi-Hit Pairs: dpMultis[First Ion,Second Ion,dp=0]\n{"",13}";
            {
                for (int i = 0; i < N + 3; i++)
                    CorrelatedTable += $"{rangeNames[i],13}";
                CorrelatedTable += "\n";
                for (int i = 0; i < N + 3; i++)
                {
                    CorrelatedTable += $"{rangeNames[i],13}";
                    for (int j = 0; j < N + 3; j++)
                        CorrelatedTable += $"{dpCorMultis[i, j, 0],13:N0}";
                    CorrelatedTable += "\n";
                }
                CorrelatedTable += $"{"Missing:",13}";
                for (int i = 0; i < N; i++)
                    CorrelatedTable += $"{missingCounts[i],13:N0}";
                CorrelatedTable += "\n";
                CorrelatedTable += "\n";
            }

            int totSelectedCorrelatedTableAndMissing = 0;
            for (int i = 0; i < N; i++)
            {
                totSelectedCorrelatedTableAndMissing += missingCounts[i];
                for (int j = 0; j < N; j++)
                    totSelectedCorrelatedTableAndMissing += dpCorMultis[i, j, 0];
            }

            double totTerm = 0d;
            string CorrelatedFixedNormalizedTable = $"Ratio: Correlated Multis + Missing : Composition Weighted, Same Total Multis Expected\n{"",13}";
            {
                for (int i = 0; i < N; i++)
                    CorrelatedFixedNormalizedTable += $"{rangeNames[i],13}";
                CorrelatedFixedNormalizedTable += "\n";
                for (int i = 0; i < N; i++)
                {
                    CorrelatedFixedNormalizedTable += $"{rangeNames[i],13}";
                    for (int j = 0; j < N; j++)
                    {
                        if (j < i) CorrelatedFixedNormalizedTable += $"{"",13}";
                        else if (i != j)
                        {
                            double term = (double)(dpCorMultis[i, j, 0] + dpCorMultis[j, i, 0]) / (double)(totSelectedCorrelatedTableAndMissing) / (2d * DeadCor[i] * DeadCor[j]);
                            totTerm += term;
                            CorrelatedFixedNormalizedTable += $"{term,13:N2}";
                        }
                        else
                        {
                            double term = (double)(dpCorMultis[i, j, 0] + missingCounts[i]) / (double)(totSelectedCorrelatedTableAndMissing) / (DeadCor[i] * DeadCor[j]);
                            totTerm += term;
                            CorrelatedFixedNormalizedTable += $"{term,13:N2}";
                        }
                    }
                    CorrelatedFixedNormalizedTable += "\n";
                }
                //CorrelatedFixedNormalizedTable += $"Table Total: {totTerm,13:N3}\n";
                CorrelatedFixedNormalizedTable += "\n";
            }
            //a la Saxey
            CorrelatedFixedNormalizedTable += $"Saxey: #Stdevs of (Correlated Multis + Missing - Composition Weighted, Same Total Multis Expected): Saxey Correlated Multis + Missing\n{"",13}";
            CorrelatedFixedNormalizedTable += $"Saxey based his expected on random, but I don't see that he predicted via detection rate, so mayble like mine-ish?\n{"",13}";
            {
                {
                    totTerm = 0d;
                    for (int i = 0; i < N; i++)
                        CorrelatedFixedNormalizedTable += $"{rangeNames[i],13}";
                    CorrelatedFixedNormalizedTable += "\n";
                    for (int i = 0; i < N; i++)
                    {
                        CorrelatedFixedNormalizedTable += $"{rangeNames[i],13}";
                        for (int j = 0; j < N; j++)
                        {
                            if (j < i) CorrelatedFixedNormalizedTable += $"{"",13}";
                            else if (i != j)
                            {
                                double eij = (2d * DeadCor[i] * DeadCor[j] * (double)totSelectedCorrelatedTableAndMissing);
                                double term = (double)(dpCorMultis[i, j, 0] + dpCorMultis[j, i, 0] - (int)eij) / Math.Sqrt(eij);
                                totTerm += term;
                                CorrelatedFixedNormalizedTable += $"{term,13:N1}";
                            }
                            else
                            {
                                double eij = DeadCor[i] * DeadCor[j] * (double)totSelectedCorrelatedTableAndMissing;
                                double term = (double)(dpCorMultis[i, j, 0] + missingCounts[i] - (int)eij) / Math.Sqrt(eij);
                                totTerm += term;
                                CorrelatedFixedNormalizedTable += $"{term,13:N1}";
                            }
                        }
                        CorrelatedFixedNormalizedTable += "\n";
                    }
                    //CorrelatedFixedNormalizedTable += $"Table Total: {totTerm,13:N3}\n";
                    CorrelatedFixedNormalizedTable += "\n";
                }
            }

            string UncorrelatedTable = $"Uncorrelated Multis Table All Multi-Hit Pairs: dpMultis[First Ion,Second Ion,dp=0]\n{"",13}";
            {
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
            }

            string UncorrelatedPseudoTable = $"Uncorrelated Pseudo-Multis Table All: dpMultis[First Ion,Second Ion,dp=1...{Parameters.IPseudoMultiMaxdp}]\n{"",13}";
            {
                for (int i = 0; i < N + 3; i++)
                    UncorrelatedPseudoTable += $"{rangeNames[i],13}";
                UncorrelatedPseudoTable += "\n";
                for (int i = 0; i < N + 3; i++)
                {
                    UncorrelatedPseudoTable += $"{rangeNames[i],13}";
                    for (int j = 0; j < N + 3; j++)
                        UncorrelatedPseudoTable += $"{dpUncMultis[i, j, Parameters.IPseudoMultiMaxdp + 1],13:N0}";
                    UncorrelatedPseudoTable += "\n";
                }
                UncorrelatedPseudoTable += "\n";
            }

            string CorrelatedPseudoTable = $"Correlated Pseudo-Multis Table All: dpMultis[First Ion,Second Ion,dp=1...{Parameters.IPseudoMultiMaxdp}]\n{"",13}";
            {
                for (int i = 0; i < N + 3; i++)
                    CorrelatedPseudoTable += $"{rangeNames[i],13}";
                CorrelatedPseudoTable += "\n";
                for (int i = 0; i < N + 3; i++)
                {
                    CorrelatedPseudoTable += $"{rangeNames[i],13}";
                    for (int j = 0; j < N + 3; j++)
                        CorrelatedPseudoTable += $"{dpCorMultis[i, j, Parameters.IPseudoMultiMaxdp + 1],13:N0}";
                    CorrelatedPseudoTable += "\n";
                }
                CorrelatedPseudoTable += "\n";
            }

            return Overview + SimpleDescriptors + PCMETable + CorrelatedTable + CorrelatedFixedNormalizedTable + UncorrelatedTable + CorrelatedPseudoTable + UncorrelatedPseudoTable + Summary;
        }

        public int getConsideredTotal(int[,,] array, int dp)
        {
            int other = array[N, N + 2, dp] + array[N + 2, N, dp] - array[N, N, dp];
            int unranged = array[N + 1, N + 2, dp] + array[N + 2, N + 1, dp] - array[N + 1, N + 1, dp];
            int correct = array[N, N + 1, dp] + array[N + 1, N, dp];
            return array[N + 2, N + 2, dp] - other - unranged + correct;
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
            return getConsideredTotal(array, dp) - getSSConsideredTotal(array, dp);
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
                if (dp > 0)
                {
                    dpDistanceCorrelations[range1, DPMax + 1, 0, sep]++;
                    dpDistanceCorrelations[range2, DPMax + 1, 0, sep]++;
                }
                //type not-same-same=1
                if (range1 != range2)
                {
                    dpDistanceCorrelations[range1, dp, 1, sep]++;
                    dpDistanceCorrelations[range2, dp, 1, sep]++;
                    if (dp > 0)
                    {
                        dpDistanceCorrelations[range1, DPMax + 1, 1, sep]++;
                        dpDistanceCorrelations[range2, DPMax + 1, 1, sep]++;
                    }
                }
                //type same-same=2
                else
                {
                    dpDistanceCorrelations[range1, dp, 2, sep]++;
                    if (dp > 0)
                        dpDistanceCorrelations[range1, DPMax + 1, 2, sep]++;
                    //Do not double count
                    //dpDistanceCorrelations[range2, dp, 2, sep]++;
                }
            }
            return;
        }

        public void fillMissing()
        {
            /* Do pair-wise deadtime correction
             *      Same element/molecule - same charge state
             *      Same element/molecule - different charge states
             *      Highest statistics
             *      Note: When one has already been calulated, do not allow it to change     
             */
            missingCounts = new int[N];
            missingPairs = new string[N];
            bool[] done = new bool[N];
            for (int i = 0; i < N; i++)
            {
                missingCounts[i] = 0;
                done[i] = false;
            }

            while (true)
            {
                //Go in order of most counts
                int max = 0;
                int maxItem = -1;
                int maxItem2 = -1;
                for (int i = 0; i < N; i++)
                {
                    if (!done[i] && totIonCounts[i] > max)
                    {
                        maxItem = i;
                        max = totIonCounts[i];
                    }
                }
                // -1 only when everything is done
                if (maxItem == -1) break;
                //Most counts left determined - find matches
                List<int> matches = new();
                //Try for same element same charge state
                for (int i = 0; i < N; i++)
                    if (i != maxItem && sameElement(rangeNames[maxItem], rangeNames[i]) && sameChargeState(rangeNames[maxItem], rangeNames[i]))
                        matches.Add(i);
                //Found match same element same charge state
                if (matches.Count() > 0)
                {
                    max = 0;
                    maxItem2 = -1;
                    foreach (int i in matches)
                    {
                        if (totIonCounts[i] > max)
                        {
                            maxItem2 = i;
                            max = totIonCounts[i];
                        }
                    }
                }
                //Try for same element different charge state
                else
                {
                    for (int i = 0; i < N; i++)
                        if (i != maxItem && sameElement(rangeNames[maxItem], rangeNames[i]))
                            matches.Add(i);
                    //Found match same element
                    if (matches.Count() > 0)
                    {
                        max = 0;
                        maxItem2 = -1;
                        foreach (int i in matches)
                        {
                            if (totIonCounts[i] > max)
                            {
                                maxItem2 = i;
                                max = totIonCounts[i];
                            }
                        }
                    }
                    //Use strongest
                    else
                    {
                        max = 0;
                        maxItem2 = -1;
                        for (int i = 0; i < N; i++)
                        {
                            if (i != maxItem && totIonCounts[i] > max)
                            {
                                maxItem2 = i;
                                max = totIonCounts[i];
                            }
                        }
                    }
                }
                determineMissingCounts(maxItem, maxItem2);
                missingPairs[maxItem] = rangeNames[maxItem2];
                done[maxItem] = true;
            }
        }

        public bool sameChargeState(string rangeName1, string rangeName2)
        {
            // Same charge state -- >5.5/1.33 and <5.5*1.33
            string[] name1 = rangeName1.Split('-');
            double dpos1 = 0d;
            Double.TryParse(name1[0], out dpos1);

            string[] name2 = rangeName2.Split('-');
            double dpos2 = 0d;
            Double.TryParse(name2[0], out dpos2);

            if (dpos2 > (dpos1 / 1.33d) && dpos2 < (1.33d * dpos1)) return true;
            return false;
        }

        public bool sameElement(string rangeName1, string rangeName2)
        {
            // 5.5-Si
            string[] name1 = rangeName1.Split('-');
            string[] name2 = rangeName2.Split('-');
            string first = name1[name1.Length - 1];
            string second = name2[name2.Length - 1];

            if (first.Equals(second)) return true;
            return false;
        }

        public void determineMissingCounts(int i, int j)
        {
            // This is called to fix any AB correlation with sufficient counts
            // M assumes some overcounting -- not implimented
            // Adjust ith only, jth may or may not have been adjusted already

            //double M = double(tot_doubles_ions - 2 * tot_doubles_unc) / double(tot_doubles_cor);
            double M = 2.0; //Use if not breaking higher multis overcounting doubles
            int I = totIonCounts[i];
            int J = totIonCounts[j] + missingCounts[j];

            //Total counts
            int Total_Counts = I + J;

            double A = (double)I / (double)Total_Counts;
            double B = 1d - A;

            int M_AA = dpMultis[i, i, 0];
            int M_AB = dpMultis[i, j, 0] + dpMultis[j, i, 0];
            int M_BB = dpMultis[j, j, 0];
            double f = 2.0 * Math.Sqrt((double)(M_AA * M_BB)) / (double)M_AB;
            double N_C = (double)M_AB / (2d * A * B);

            //Assume 1 of missing doubles has been detected, so M/2
            int mcAA = (int)(M / 2.0 * (N_C * A * A - (double)M_AA));
            int mcBB = (int)(M / 2.0 * (N_C * B * B - (double)M_BB));

            if (mcAA >= 0 && mcBB >= 0)
                missingCounts[i] = mcAA;
        }
    }
}