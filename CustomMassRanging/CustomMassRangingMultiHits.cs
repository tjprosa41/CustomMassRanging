using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using Microsoft.Windows.Themes;
using NanoXLSX.Styles;
using Polly.Caching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography.Pkcs;
using System.Transactions;
using System.Windows.Controls.Ribbon;
using System.Windows.Documents;
using System.Windows.Input;
using System.Xml;
using static System.Net.Mime.MediaTypeNames;

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
        public float DistRes = 0.20f; //1000*0.2 = 200 nm or mm

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
        public int[] rangeBgd = null!;              
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
                                                               //non-double-counted multi-ions
        public double[] P = null!; //COR fraction or probability
        public int[] missingCounts = null!;
        //public int[] previousMissingCounts = null!;
        public double[,] WijMatrix = null!;      
        public int[] missingSigma2 = null!;
        public string[] missingPairs = null!;
        //public int[] Nc = null!;
        public int Ncor = 0;
        public double NcSigma2 = 0d;
        public int NcWeightedAve = 0;
        public int iterations = 0;
        public double[] iterationChi2 = null!;
        public class MultiStuff
        {
            public int range;
            public double realPulse;
            public Vector3 coordinate;
        }

        public EIons useSepPlots = EIons.Selected;
        public EMultiPiCalc multiPiCalcUse = EMultiPiCalc.All;
        public bool useNatAbundances = false;
        public List<(string Name, double Fraction)> isotopeList = new List<(string Name, double Fraction)>();

        //Initialize
        public MultiHits(IIonData ionData, Vector2[]? values, ObservableCollection<RangesTableEntries> useRanges, ObservableCollection<RangesTableEntries> allRanges, Parameters Parameters)
        {
            if (values == null || useRanges == null || allRanges == null)
                return;

//The percent natural abundance data is from the 1997 report of the IUPAC Subcommittee for Isotopic
//Abundance Measurements by K.J.R.Rosman, P.D.P.Taylor Pure Appl.Chem. 1999, 71, 1593 - 1607.
string data = @"1.0-H,1,4.0-He,1,2.0-He,1
6.0-Li,0.0759,3.0-Li,0.0759,2.0-Li,0.0759
7.0-Li,0.9241,3.5-Li,0.9241,2.3-Li,0.9241
9.0-Be,1,4.5-Be,1,3.0-Be,1
10.0-B,0.199,5.0-B,0.199,3.3-B,0.199
11.0-B,0.801,5.5-B,0.801,3.7-B,0.801
12.0-C,0.9893,6.0-C,0.9893,4.0-C,0.9893
13.0-C,0.0107,6.5-C,0.0107,4.3-C,0.0107
24.0-C2,0.97871449,12.0-C2,0.97871449,8.0-C2,0.97871449
25.0-C2,0.02117102,12.5-C2,0.02117102,8.3-C2,0.02117102
26.0-C2,0.00011449,13.0-C2,0.00011449,8.7-C2,0.00011449
36.0-C3,0.968242245,18.0-C3,0.968242245,12.0-C3,0.968242245
37.0-C3,0.031416735,18.5-C3,0.031416735,12.3-C3,0.031416735
38.0-C3,0.000339795,19.0-C3,0.000339795,12.7-C3,0.000339795
39.0-C3,1.22504E-06,13.0-C3,1.22504E-06,13.0-C3,1.22504E-06
14.0-N,0.99632,7.0-N,0.99632,4.7-N,0.99632
15.0-N,0.00368,7.5-N,0.00368,5.0-N,0.00368
16.0-O,0.99757,8.0-O,0.99757,5.3-O,0.99757
17.0-O,0.00038,8.5-O,0.00038,5.7-O,0.00038
18.0-O,0.00205,9.0-O,0.00205,6.0-O,0.00205
19.0-F,1,9.5-F,1,6.3-F,1
20.0-Ne,0.9048,10.0-Ne,0.9048,6.7-Ne,0.9048
21.0-Ne,0.0027,10.5-Ne,0.0027,7.0-Ne,0.0027
22.0-Ne,0.0925,11.0-Ne,0.0925,7.3-Ne,0.0925
23.0-Na,1,11.5-Na,1,7.7-Na,1
24.0-Mg,0.7899,12.0-Mg,0.7899,8.0-Mg,0.7899
25.0-Mg,0.1,12.5-Mg,0.1,8.3-Mg,0.1
26.0-Mg,0.1101,13.0-Mg,0.1101,8.7-Mg,0.1101
27.0-Al,1,13.5-Al,1,9.0-Al,1
28.0-Si,0.922297,14.0-Si,0.922297,9.3-Si,0.922297
29.0-Si,0.046832,14.5-Si,0.046832,9.7-Si,0.046832
30.0-Si,0.030872,15.0-Si,0.030872,10.0-Si,0.030872
31.0-P,1,15.5-P,1,10.3-P,1
32.0-S,0.9493,16.0-S,0.9493,10.7-S,0.9493
33.0-S,0.0076,16.5-S,0.0076,11.0-S,0.0076
34.0-S,0.0429,17.0-S,0.0429,11.3-S,0.0429
36.0-S,0.0002,18.0-S,0.0002,12.0-S,0.0002
35.0-Cl,0.7578,17.5-Cl,0.7578,11.7-Cl,0.7578
37.0-Cl,0.2422,18.5-Cl,0.2422,12.3-Cl,0.2422
36.0-Ar,0.003365,18.0-Ar,0.003365,12.0-Ar,0.003365
38.0-Ar,0.000632,19.0-Ar,0.000632,12.7-Ar,0.000632
40.0-Ar,0.996003,20.0-Ar,0.996003,13.3-Ar,0.996003
39.0-K,0.932581,19.5-K,0.932581,13.0-K,0.932581
40.0-K,0.000117,20.0-K,0.000117,13.3-K,0.000117
41.0-K,0.067302,20.5-K,0.067302,13.7-K,0.067302
40.0-Ca,0.96941,20.0-Ca,0.96941,13.3-Ca,0.96941
42.0-Ca,0.00647,21.0-Ca,0.00647,14.0-Ca,0.00647
43.0-Ca,0.00135,21.5-Ca,0.00135,14.3-Ca,0.00135
44.0-Ca,0.02086,22.0-Ca,0.02086,14.7-Ca,0.02086
46.0-Ca,0.00004,23.0-Ca,0.00004,15.3-Ca,0.00004
48.0-Ca,0.00187,24.0-Ca,0.00187,16.0-Ca,0.00187
45.0-Sc,1,22.5-Sc,1,15.0-Sc,1
46.0-Ti,0.0825,23.0-Ti,0.0825,15.3-Ti,0.0825
47.0-Ti,0.0744,23.5-Ti,0.0744,15.7-Ti,0.0744
47.9-Ti,0.7372,24.0-Ti,0.7372,16.0-Ti,0.7372
48.9-Ti,0.0541,24.5-Ti,0.0541,16.3-Ti,0.0541
49.9-Ti,0.0518,25.0-Ti,0.0518,16.6-Ti,0.0518
49.9-V,0.0025,25.0-V,0.0025,16.6-V,0.0025
50.9-V,0.9975,25.5-V,0.9975,17.0-V,0.9975
49.9-Cr,0.04345,25.0-Cr,0.04345,16.6-Cr,0.04345
51.9-Cr,0.83789,26.0-Cr,0.83789,17.3-Cr,0.83789
52.9-Cr,0.09501,26.5-Cr,0.09501,17.6-Cr,0.09501
53.9-Cr,0.02365,27.0-Cr,0.02365,18.0-Cr,0.02365
54.9-Mn,1,27.5-Mn,1,18.3-Mn,1
53.9-Fe,0.05845,27.0-Fe,0.05845,18.0-Fe,0.05845
55.9-Fe,0.91754,28.0-Fe,0.91754,18.6-Fe,0.91754
56.9-Fe,0.02119,28.5-Fe,0.02119,19.0-Fe,0.02119
57.9-Fe,0.00282,29.0-Fe,0.00282,19.3-Fe,0.00282
58.9-Co,1,29.5-Co,1,19.6-Co,1
57.9-Ni,0.680769,29.0-Ni,0.680769,19.3-Ni,0.680769
59.9-Ni,0.262231,30.0-Ni,0.262231,20.0-Ni,0.262231
60.9-Ni,0.011399,30.5-Ni,0.011399,20.3-Ni,0.011399
61.9-Ni,0.036345,31.0-Ni,0.036345,20.6-Ni,0.036345
63.9-Ni,0.009256,32.0-Ni,0.009256,21.3-Ni,0.009256
62.9-Cu,0.6917,31.5-Cu,0.6917,21.0-Cu,0.6917
64.9-Cu,0.3083,32.5-Cu,0.3083,21.6-Cu,0.3083
63.9-Zn,0.4863,32.0-Zn,0.4863,21.3-Zn,0.4863
65.9-Zn,0.279,33.0-Zn,0.279,22.0-Zn,0.279
66.9-Zn,0.041,33.5-Zn,0.041,22.3-Zn,0.041
67.9-Zn,0.1875,34.0-Zn,0.1875,22.6-Zn,0.1875
69.9-Zn,0.0062,35.0-Zn,0.0062,23.3-Zn,0.0062
68.9-Ga,0.60108,34.5-Ga,0.60108,23.0-Ga,0.60108
70.9-Ga,0.39892,35.5-Ga,0.39892,23.6-Ga,0.39892
69.9-Ge,0.2084,35.0-Ge,0.2084,23.3-Ge,0.2084
71.9-Ge,0.2754,36.0-Ge,0.2754,24.0-Ge,0.2754
72.9-Ge,0.0773,36.5-Ge,0.0773,24.3-Ge,0.0773
73.9-Ge,0.3628,37.0-Ge,0.3628,24.6-Ge,0.3628
75.9-Ge,0.0761,38.0-Ge,0.0761,25.3-Ge,0.0761
74.9-As,1,37.5-As,1,25.0-As,1
73.9-Se,0.0089,37.0-Se,0.0089,24.6-Se,0.0089
75.9-Se,0.0937,38.0-Se,0.0937,25.3-Se,0.0937
76.9-Se,0.0763,38.5-Se,0.0763,25.6-Se,0.0763
77.9-Se,0.2377,39.0-Se,0.2377,26.0-Se,0.2377
79.9-Se,0.4961,40.0-Se,0.4961,26.6-Se,0.4961
81.9-Se,0.0873,41.0-Se,0.0873,27.3-Se,0.0873
78.9-Br,0.5069,39.5-Br,0.5069,26.3-Br,0.5069
80.9-Br,0.4931,40.5-Br,0.4931,27.0-Br,0.4931
77.9-Kr,0.0035,39.0-Kr,0.0035,26.0-Kr,0.0035
79.9-Kr,0.0228,40.0-Kr,0.0228,26.6-Kr,0.0228
81.9-Kr,0.1158,41.0-Kr,0.1158,27.3-Kr,0.1158
82.9-Kr,0.1149,41.5-Kr,0.1149,27.6-Kr,0.1149
83.9-Kr,0.57,42.0-Kr,0.57,28.0-Kr,0.57
85.9-Kr,0.173,43.0-Kr,0.173,28.6-Kr,0.173
84.9-Rb,0.7217,42.5-Rb,0.7217,28.3-Rb,0.7217
86.9-Rb,0.2783,43.5-Rb,0.2783,29.0-Rb,0.2783
83.9-Sr,0.0056,42.0-Sr,0.0056,28.0-Sr,0.0056
85.9-Sr,0.0986,43.0-Sr,0.0986,28.6-Sr,0.0986
86.9-Sr,0.07,43.5-Sr,0.07,29.0-Sr,0.07
87.9-Sr,0.8258,44.0-Sr,0.8258,29.3-Sr,0.8258
88.9-Y,1,44.5-Y,1,29.6-Y,1
89.9-Zr,0.5145,45.0-Zr,0.5145,30.0-Zr,0.5145
90.9-Zr,0.1122,45.5-Zr,0.1122,30.3-Zr,0.1122
91.9-Zr,0.1715,46.0-Zr,0.1715,30.6-Zr,0.1715
93.9-Zr,0.1738,47.0-Zr,0.1738,31.3-Zr,0.1738
95.9-Zr,0.028,48.0-Zr,0.028,32.0-Zr,0.028
92.9-Nb,1,46.5-Nb,1,31.0-Nb,1
91.9-Mo,0.1484,46.0-Mo,0.1484,30.6-Mo,0.1484
93.9-Mo,0.0925,47.0-Mo,0.0925,31.3-Mo,0.0925
94.9-Mo,0.1592,47.5-Mo,0.1592,31.6-Mo,0.1592
95.9-Mo,0.1668,48.0-Mo,0.1668,32.0-Mo,0.1668
96.9-Mo,0.0955,48.5-Mo,0.0955,32.3-Mo,0.0955
97.9-Mo,0.2413,49.0-Mo,0.2413,32.6-Mo,0.2413
99.9-Mo,0.0963,50.0-Mo,0.0963,33.3-Mo,0.0963
97.9-Tc,0,49.0-Tc,0,32.6-Tc,0
95.9-Ru,0.0554,48.0-Ru,0.0554,32.0-Ru,0.0554
97.9-Ru,0.0187,49.0-Ru,0.0187,32.6-Ru,0.0187
98.9-Ru,0.1276,49.5-Ru,0.1276,33.0-Ru,0.1276
99.9-Ru,0.126,50.0-Ru,0.126,33.3-Ru,0.126
100.9-Ru,0.1706,50.5-Ru,0.1706,33.6-Ru,0.1706
101.9-Ru,0.3155,51.0-Ru,0.3155,34.0-Ru,0.3155
103.9-Ru,0.1862,52.0-Ru,0.1862,34.6-Ru,0.1862
102.9-Rh,1,51.5-Rh,1,34.3-Rh,1
101.9-Pd,0.0102,51.0-Pd,0.0102,34.0-Pd,0.0102
103.9-Pd,0.1114,52.0-Pd,0.1114,34.6-Pd,0.1114
104.9-Pd,0.2233,52.5-Pd,0.2233,35.0-Pd,0.2233
105.9-Pd,0.2733,53.0-Pd,0.2733,35.3-Pd,0.2733
107.9-Pd,0.2646,54.0-Pd,0.2646,36.0-Pd,0.2646
109.9-Pd,0.1172,55.0-Pd,0.1172,36.6-Pd,0.1172
106.9-Ag,0.51839,53.5-Ag,0.51839,35.6-Ag,0.51839
108.9-Ag,0.48161,54.5-Ag,0.48161,36.3-Ag,0.48161
105.9-Cd,0.0125,53.0-Cd,0.0125,35.3-Cd,0.0125
107.9-Cd,0.0089,54.0-Cd,0.0089,36.0-Cd,0.0089
109.9-Cd,0.1249,55.0-Cd,0.1249,36.6-Cd,0.1249
110.9-Cd,0.128,55.5-Cd,0.128,37.0-Cd,0.128
111.9-Cd,0.2413,56.0-Cd,0.2413,37.3-Cd,0.2413
112.9-Cd,0.1222,56.5-Cd,0.1222,37.6-Cd,0.1222
113.9-Cd,0.2873,57.0-Cd,0.2873,38.0-Cd,0.2873
115.9-Cd,0.0749,58.0-Cd,0.0749,38.6-Cd,0.0749
112.9-In,0.0429,56.5-In,0.0429,37.6-In,0.0429
114.9-In,0.9571,57.5-In,0.9571,38.3-In,0.9571
111.9-Sn,0.0097,56.0-Sn,0.0097,37.3-Sn,0.0097
113.9-Sn,0.0066,57.0-Sn,0.0066,38.0-Sn,0.0066
114.9-Sn,0.0034,57.5-Sn,0.0034,38.3-Sn,0.0034
115.9-Sn,0.1454,58.0-Sn,0.1454,38.6-Sn,0.1454
116.9-Sn,0.0768,58.5-Sn,0.0768,39.0-Sn,0.0768
117.9-Sn,0.2422,59.0-Sn,0.2422,39.3-Sn,0.2422
118.9-Sn,0.0859,59.5-Sn,0.0859,39.6-Sn,0.0859
119.9-Sn,0.3258,60.0-Sn,0.3258,40.0-Sn,0.3258
121.9-Sn,0.0463,61.0-Sn,0.0463,40.6-Sn,0.0463
123.9-Sn,0.0579,62.0-Sn,0.0579,41.3-Sn,0.0579
120.9-Sb,0.5721,60.5-Sb,0.5721,40.3-Sb,0.5721
122.9-Sb,0.4279,61.5-Sb,0.4279,41.0-Sb,0.4279
119.9-Te,0.0009,60.0-Te,0.0009,40.0-Te,0.0009
121.9-Te,0.0255,61.0-Te,0.0255,40.6-Te,0.0255
122.9-Te,0.0089,61.5-Te,0.0089,41.0-Te,0.0089
123.9-Te,0.0474,62.0-Te,0.0474,41.3-Te,0.0474
124.9-Te,0.0707,62.5-Te,0.0707,41.6-Te,0.0707
125.9-Te,0.1884,63.0-Te,0.1884,42.0-Te,0.1884
127.9-Te,0.3174,64.0-Te,0.3174,42.6-Te,0.3174
129.9-Te,0.3408,65.0-Te,0.3408,43.3-Te,0.3408
126.9-I,1,63.5-I,1,42.3-I,1
123.9-Xe,0.0009,62.0-Xe,0.0009,41.3-Xe,0.0009
125.9-Xe,0.0009,63.0-Xe,0.0009,42.0-Xe,0.0009
127.9-Xe,0.0192,64.0-Xe,0.0192,42.6-Xe,0.0192
128.9-Xe,0.2644,64.5-Xe,0.2644,43.0-Xe,0.2644
129.9-Xe,0.0408,65.0-Xe,0.0408,43.3-Xe,0.0408
130.9-Xe,0.2118,65.5-Xe,0.2118,43.6-Xe,0.2118
131.9-Xe,0.2689,66.0-Xe,0.2689,44.0-Xe,0.2689
133.9-Xe,0.1044,67.0-Xe,0.1044,44.6-Xe,0.1044
135.9-Xe,0.0887,68.0-Xe,0.0887,45.3-Xe,0.0887
132.9-Cs,1,66.5-Cs,1,44.3-Cs,1
129.9-Ba,0.00106,65.0-Ba,0.00106,43.3-Ba,0.00106
131.9-Ba,0.00101,66.0-Ba,0.00101,44.0-Ba,0.00101
133.9-Ba,0.02417,67.0-Ba,0.02417,44.6-Ba,0.02417
134.9-Ba,0.06592,67.5-Ba,0.06592,45.0-Ba,0.06592
135.9-Ba,0.07854,68.0-Ba,0.07854,45.3-Ba,0.07854
136.9-Ba,0.11232,68.5-Ba,0.11232,45.6-Ba,0.11232
137.9-Ba,0.71698,69.0-Ba,0.71698,46.0-Ba,0.71698
137.9-La,0.0009,69.0-La,0.0009,46.0-La,0.0009
138.9-La,0.9991,69.5-La,0.9991,46.3-La,0.9991
135.9-Ce,0.00185,68.0-Ce,0.00185,45.3-Ce,0.00185
137.9-Ce,0.00251,69.0-Ce,0.00251,46.0-Ce,0.00251
139.9-Ce,0.8845,70.0-Ce,0.8845,46.6-Ce,0.8845
141.9-Ce,0.11114,71.0-Ce,0.11114,47.3-Ce,0.11114
140.9-Pr,1,70.5-Pr,1,47.0-Pr,1
141.9-Nd,0.272,71.0-Nd,0.272,47.3-Nd,0.272
142.9-Nd,0.122,71.5-Nd,0.122,47.6-Nd,0.122
143.9-Nd,0.238,72.0-Nd,0.238,48.0-Nd,0.238
144.9-Nd,0.083,72.5-Nd,0.083,48.3-Nd,0.083
145.9-Nd,0.172,73.0-Nd,0.172,48.6-Nd,0.172
147.9-Nd,0.057,74.0-Nd,0.057,49.3-Nd,0.057
149.9-Nd,0.056,75.0-Nd,0.056,50.0-Nd,0.056
144.9-Pm,0,72.5-Pm,0,48.3-Pm,0
143.9-Sm,0.0307,72.0-Sm,0.0307,48.0-Sm,0.0307
146.9-Sm,0.1499,73.5-Sm,0.1499,49.0-Sm,0.1499
147.9-Sm,0.1124,74.0-Sm,0.1124,49.3-Sm,0.1124
148.9-Sm,0.1382,74.5-Sm,0.1382,49.6-Sm,0.1382
149.9-Sm,0.0738,75.0-Sm,0.0738,50.0-Sm,0.0738
151.9-Sm,0.2675,76.0-Sm,0.2675,50.6-Sm,0.2675
153.9-Sm,0.2275,77.0-Sm,0.2275,51.3-Sm,0.2275
150.9-Eu,0.4781,75.5-Eu,0.4781,50.3-Eu,0.4781
152.9-Eu,0.5219,76.5-Eu,0.5219,51.0-Eu,0.5219
151.9-Gd,0.002,76.0-Gd,0.002,50.6-Gd,0.002
153.9-Gd,0.0218,77.0-Gd,0.0218,51.3-Gd,0.0218
154.9-Gd,0.148,77.5-Gd,0.148,51.6-Gd,0.148
155.9-Gd,0.2047,78.0-Gd,0.2047,52.0-Gd,0.2047
156.9-Gd,0.1565,78.5-Gd,0.1565,52.3-Gd,0.1565
157.9-Gd,0.2484,79.0-Gd,0.2484,52.6-Gd,0.2484
159.9-Gd,0.2186,80.0-Gd,0.2186,53.3-Gd,0.2186
158.9-Tb,1,79.5-Tb,1,53.0-Tb,1
155.9-Dy,0.0006,78.0-Dy,0.0006,52.0-Dy,0.0006
157.9-Dy,0.001,79.0-Dy,0.001,52.6-Dy,0.001
159.9-Dy,0.0234,80.0-Dy,0.0234,53.3-Dy,0.0234
160.9-Dy,0.1891,80.5-Dy,0.1891,53.6-Dy,0.1891
161.9-Dy,0.2551,81.0-Dy,0.2551,54.0-Dy,0.2551
162.9-Dy,0.249,81.5-Dy,0.249,54.3-Dy,0.249
163.9-Dy,0.2818,82.0-Dy,0.2818,54.6-Dy,0.2818
164.9-Ho,1,82.5-Ho,1,55.0-Ho,1
161.9-Er,0.0014,81.0-Er,0.0014,54.0-Er,0.0014
163.9-Er,0.0161,82.0-Er,0.0161,54.6-Er,0.0161
165.9-Er,0.3361,83.0-Er,0.3361,55.3-Er,0.3361
166.9-Er,0.2293,83.5-Er,0.2293,55.6-Er,0.2293
167.9-Er,0.2678,84.0-Er,0.2678,56.0-Er,0.2678
169.9-Er,0.1493,85.0-Er,0.1493,56.6-Er,0.1493
168.9-Tm,1,84.5-Tm,1,56.3-Tm,1
167.9-Yb,0.0013,84.0-Yb,0.0013,56.0-Yb,0.0013
169.9-Yb,0.0304,85.0-Yb,0.0304,56.6-Yb,0.0304
170.9-Yb,0.1428,85.5-Yb,0.1428,57.0-Yb,0.1428
171.9-Yb,0.2183,86.0-Yb,0.2183,57.3-Yb,0.2183
172.9-Yb,0.1613,86.5-Yb,0.1613,57.6-Yb,0.1613
173.9-Yb,0.3183,87.0-Yb,0.3183,58.0-Yb,0.3183
175.9-Yb,0.1276,88.0-Yb,0.1276,58.6-Yb,0.1276
174.9-Lu,0.9741,87.5-Lu,0.9741,58.3-Lu,0.9741
175.9-Lu,0.0259,88.0-Lu,0.0259,58.6-Lu,0.0259
173.9-Hf,0.0016,87.0-Hf,0.0016,58.0-Hf,0.0016
175.9-Hf,0.0526,88.0-Hf,0.0526,58.6-Hf,0.0526
176.9-Hf,0.186,88.5-Hf,0.186,59.0-Hf,0.186
177.9-Hf,0.2728,89.0-Hf,0.2728,59.3-Hf,0.2728
178.9-Hf,0.1362,89.5-Hf,0.1362,59.6-Hf,0.1362
179.9-Hf,0.3508,90.0-Hf,0.3508,60.0-Hf,0.3508
179.9-Ta,0.00012,90.0-Ta,0.00012,60.0-Ta,0.00012
180.9-Ta,0.99988,90.5-Ta,0.99988,60.3-Ta,0.99988
179.9-W,0.0012,90.0-W,0.0012,60.0-W,0.0012
181.9-W,0.265,91.0-W,0.265,60.6-W,0.265
183.0-W,0.1431,91.5-W,0.1431,61.0-W,0.1431
184.0-W,0.3064,92.0-W,0.3064,61.3-W,0.3064
186.0-W,0.2843,93.0-W,0.2843,62.0-W,0.2843
185.0-Re,0.374,92.5-Re,0.374,61.7-Re,0.374
187.0-Re,0.626,93.5-Re,0.626,62.3-Re,0.626
184.0-Os,0.0002,92.0-Os,0.0002,61.3-Os,0.0002
186.0-Os,0.0159,93.0-Os,0.0159,62.0-Os,0.0159
187.0-Os,0.0196,93.5-Os,0.0196,62.3-Os,0.0196
188.0-Os,0.1324,94.0-Os,0.1324,62.7-Os,0.1324
189.0-Os,0.1615,94.5-Os,0.1615,63.0-Os,0.1615
190.0-Os,0.2626,95.0-Os,0.2626,63.3-Os,0.2626
192.0-Os,0.4078,96.0-Os,0.4078,64.0-Os,0.4078
191.0-Ir,0.373,95.5-Ir,0.373,63.7-Ir,0.373
193.0-Ir,0.627,96.5-Ir,0.627,64.3-Ir,0.627
190.0-Pt,0.00014,95.0-Pt,0.00014,63.3-Pt,0.00014
192.0-Pt,0.00782,96.0-Pt,0.00782,64.0-Pt,0.00782
194.0-Pt,0.32967,97.0-Pt,0.32967,64.7-Pt,0.32967
195.0-Pt,0.33832,97.5-Pt,0.33832,65.0-Pt,0.33832
196.0-Pt,0.25242,98.0-Pt,0.25242,65.3-Pt,0.25242
198.0-Pt,0.07163,99.0-Pt,0.07163,66.0-Pt,0.07163
197.0-Au,1,98.5-Au,1,65.7-Au,1
196.0-Hg,0.0015,98.0-Hg,0.0015,65.3-Hg,0.0015
198.0-Hg,0.0997,99.0-Hg,0.0997,66.0-Hg,0.0997
199.0-Hg,0.1687,99.5-Hg,0.1687,66.3-Hg,0.1687
200.0-Hg,0.231,100.0-Hg,0.231,66.7-Hg,0.231
201.0-Hg,0.1318,100.5-Hg,0.1318,67.0-Hg,0.1318
202.0-Hg,0.2986,101.0-Hg,0.2986,67.3-Hg,0.2986
204.0-Hg,0.0687,102.0-Hg,0.0687,68.0-Hg,0.0687
203.0-Tl,0.29524,101.5-Tl,0.29524,67.7-Tl,0.29524
205.0-Tl,0.70476,102.5-Tl,0.70476,68.3-Tl,0.70476
204.0-Pb,0.014,102.0-Pb,0.014,68.0-Pb,0.014
206.0-Pb,0.241,103.0-Pb,0.241,68.7-Pb,0.241
207.0-Pb,0.221,103.5-Pb,0.221,69.0-Pb,0.221
208.0-Pb,0.524,104.0-Pb,0.524,69.3-Pb,0.524
209.0-Bi,1,104.5-Bi,1,69.7-Bi,1
209.0-Po,0,104.5-Po,0,69.7-Po,0
210.0-At,0,105.0-At,0,70.0-At,0
222.0-Rn,0,111.0-Rn,0,74.0-Rn,0
223.0-Fr,0,111.5-Fr,0,74.3-Fr,0
226.0-Ra,0,113.0-Ra,0,75.3-Ra,0
227.0-Ac,0,113.5-Ac,0,75.7-Ac,0
232.0-Th,1,116.0-Th,1,77.3-Th,1
231.0-Pa,1,115.5-Pa,1,77.0-Pa,1
234.0-U,0.000055,117.0-U,0.000055,78.0-U,0.000055
235.0-U,0.0072,117.5-U,0.0072,78.3-U,0.0072
238.1-U,0.992745,119.0-U,0.992745,79.4-U,0.992745
237.0-Np,0,118.5-Np,0,79.0-Np,0
244.1-Pu,0,122.0-Pu,0,81.4-Pu,0
243.1-Am,0,121.5-Am,0,81.0-Am,0
247.1-Cm,0,123.5-Cm,0,82.4-Cm,0
247.1-Bk,0,123.5-Bk,0,82.4-Bk,0
251.1-Cf,0,125.5-Cf,0,83.7-Cf,0
252.1-Es,0,126.0-Es,0,84.0-Es,0
257.1-Fm,0,128.5-Fm,0,85.7-Fm,0
258.1-Md,0,129.0-Md,0,86.0-Md,0
259.1-No,0,129.6-No,0,86.4-No,0
262.1-Lr,0,131.1-Lr,0,87.4-Lr,0
263.1-Rf,0,131.6-Rf,0,87.7-Rf,0
262.0-Db,0,131.0-Db,0,87.3-Db,0
266.0-Sg,0,133.0-Sg,0,88.7-Sg,0
264.0-Bh,0,132.0-Bh,0,88.0-Bh,0
269.0-Hs,0,134.5-Hs,0,89.7-Hs,0
268.0-Mt,0,134.0-Mt,0,89.3-Mt,0
272.0-Uun,0,136.0-Uun,0,90.7-Uun,0
272.0-Uuu,0,136.0-Uuu,0,90.7-Uuu,0
277.0-Uub,0,138.5-Uub,0,92.3-Uub,0
289.0-Uuq,0,144.5-Uuq,0,96.3-Uuq,0
289.0-Uuh,0,144.5-Uuh,0,96.3-Uuh,0
293.0-Uuo,0,146.5-Uuo,0,97.7-Uuo,0";

string[] rows = data.Split("\r\n");
foreach (string row in rows)
{
    string[] columns = row.Split(',');
    if (columns.Length >= 6)
    {
        isotopeList.Add((columns[0], double.Parse(columns[1])));
        isotopeList.Add((columns[2], double.Parse(columns[3])));
        isotopeList.Add((columns[4], double.Parse(columns[5])));
    }
}

            if (Parameters.BUseDetectorSeparations) DistRes = 0.080f; //0.08*1000 = 80 mm separations
            useSepPlots = Parameters.ESepPlots;
            useNatAbundances = Parameters.BUseNatAbundances;

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
            rangeBgd = new int[N];
            /*
            j = 0;
            foreach (var range in useRanges)
            {
                rangeNames[j] = $"{(10.0d * range.Pos / 3.0d) * 3f / 10f:N1}-{range.Name}";
                rangeMins[j] = (float)range.Min;
                rangeMaxs[j] = (float)range.Max;
                j++;
            }
            */
            for (j = 0; j < N; j++)
                setRangeName(useRanges[j], j);

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

            void setRangeName(RangesTableEntries range, int j)
            {
                rangeNames[j] = $"{(10.0d * range.Pos / 3.0d) * 3f / 10f:N1}-{range.Name}";
                rangeMins[j] = (float)range.Min;
                rangeMaxs[j] = (float)range.Max;
                rangeBgd[j] = (int)(range.Bgd + 0.5d);
            }

            //Unused, but maybe update and add later...
            /*
            int getRangeNumberFromName(string rangeName)
            {
                for (int i=0; i<N; i++)
                    if (rangeNames[i].Equals(rangeName)) return i;
                return -1; //not found
            }
            int getRangeNumberFromPos(float pos)
            {
                for (int i = 0; i < N; i++)
                    if (pos >= rangeMins[i] && pos <= rangeMaxs[i]) return i;
                return -1; //not found
            }
            */
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
            {
                //Do not include dp=0 into dp>0 totals!
                int dp = 0;
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
                    dpMultis[N + 2, N + 2, dp] += dpMultis[i, N + 2, dp]; // N+2,N+2 is the entire table total
                    dpCorMultis[N + 2, N + 2, dp] += dpCorMultis[i, N + 2, dp];
                    dpUncMultis[N + 2, N + 2, dp] += dpUncMultis[i, N + 2, dp];
                }
            }
            for (int dp = 1; dp < DPMax + 1; dp++)
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
            fillWijMatrix();
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
            return (int)(separation / (double)DistRes + 0.5d);
        }

        public void MultisSummaryString(Parameters Parameters, MyViewableString MultisInformation)
        {
            string Overview = "";
            {
                Overview += "  Occurrences are tracked for various groups of ions:\n";
                Overview += "  Considered:   Specific ranges to include in summary table.\n";
                Overview += "  Key Range:    Range to track computation of average values for ToF and Voltage.\n";
                Overview += "  Other:        All other defined ranges (in ranges table--discovered only if user indicates).\n";
                Overview += "  Unranged:     All ions between any defined ranges.\n";
                Overview += "  Correlated:   Multi-hit ions that have separations smaller than the critical value.\n";
                Overview += "  Uncorrelated: Multi-hit ions that have separation larger than the critical value.\n";
                Overview += "  Pseudo-multi: Consecutive single-ion events only, tracked for delta pulse (dp) values\n";
                Overview += "                out to some maximum dp time/pulse separation value.\n";
                Overview += "\n";
                Overview += $"  Note: Double events in tables represent any pair of events--there is over representation\n";
                Overview += $"    because all multi-events considered are converted to interaction pairs or doubles\n";
                Overview += $"    (e.g., triples converted to three pairs in multis tables).\n";
                Overview += $"    Overcounting not possible with pseudo-double definitions.\n";
                Overview += $"\n";
            }
            MultisInformation.Overview = Overview;

            string SimpleDescriptors = "";
            {
                SimpleDescriptors += $"{"PME (event probability):",52}";
                SimpleDescriptors += $"{(double)(eventPulses - singles[N + 2]) / (double)eventPulses,13:P2}";
                SimpleDescriptors += "  Multi-Event Pulses / Total Event Pulses\n";
                SimpleDescriptors += $"{"Ion from PME (ion origin event probability):",52}";
                SimpleDescriptors += $"{(double)(multiIonTrueCounts[N + 2, 0] + multiIonTrueCounts[N + 2, 1]) / (double)totIonCounts[N + 2],13:P2}";
                SimpleDescriptors += "  Multi-Event Ions / Total Ions\n";
                SimpleDescriptors += $"{"PCME (ion origin event probability):",52}";
                SimpleDescriptors += $"{(double)multiIonTrueCounts[N + 2, 0] / (double)totIonCounts[N + 2],13:P2}";
                SimpleDescriptors += "  Correlated Multi-Event Ions / Total Ions\n";
                SimpleDescriptors += $"{"PCME Cor.(deadtime corrected PCME):",52}";
                SimpleDescriptors += $"{(double)(multiIonTrueCounts[N + 2, 0] + missingCounts[N]) / (double)(totIonCounts[N + 2] + missingCounts[N]),13:P2}";
                SimpleDescriptors += "  Deadtime Corrected PCME\n";
                SimpleDescriptors += $"{"Deadtime Est.:",52}";
                SimpleDescriptors += $"{(double)missingCounts[N] / (double)(totIonCounts[N + 2] + missingCounts[N]),13:P2}";
                SimpleDescriptors += "  Missing Ions / (Total Ions + Missing Ions)\n";
                SimpleDescriptors += $"{"Cor. Same-Same Eff.:",52}";
                int totSameSame = 0;
                for (int i = 0; i < N; i++)
                    totSameSame += dpCorMultis[i, i, 0];
                SimpleDescriptors += $"{(double)(totSameSame) / (double)(totSameSame + missingCounts[N]),13:P2}";
                SimpleDescriptors += "  Correlated Same-Same Events / (Correlated Same-Same Events + Missing Ions)\n";
                SimpleDescriptors += $"\n";
            }
            MultisInformation.Simple = SimpleDescriptors;

            double[] DeadCor = new double[N];
            string PCMETable = "";
            {
                PCMETable += $"{"",15}";
                for (int i = 0; i < N + 3; i++)
                    PCMETable += $"{rangeNames[i],13}";
                PCMETable += $"\n";

                PCMETable += $"{"PCME:",15}";
                for (int i = 0; i < N + 3; i++)
                {
                    if (totIonCounts[i]>0)
                        PCMETable += $"{(double)multiIonTrueCounts[i, 0] / (double)(totIonCounts[i]),13:P1}";
                    else
                        PCMETable += $"{"NA",13}";
                }
                PCMETable += $"\n";

                PCMETable += $"{"PCME Corrected:",15}";
                for (int i = 0; i < N; i++)
                    if (totIonCounts[i] + missingCounts[i] > 0)
                        PCMETable += $"{(double)(multiIonTrueCounts[i, 0] + missingCounts[i]) / (double)(totIonCounts[i] + missingCounts[i]),13:P1}";
                    else
                        PCMETable += $"{"NA",13}";
                for (int i = N; i < N + 3; i++)
                    if (totIonCounts[i] > 0)
                        PCMETable += $"{(double)multiIonTrueCounts[i, 0] / (double)(totIonCounts[i]),13:P1}";
                    else
                        PCMETable += $"{"NA",13}";                
                PCMETable += $"\n";

                PCMETable += $"{"Missing:",15}";
                for (int i = 0; i < N; i++)
                    if (missingCounts[i] + totIonCounts[i] > 0)
                        PCMETable += $"{(double)missingCounts[i] / (double)(missingCounts[i] + totIonCounts[i]),13:P1}";
                    else
                        PCMETable += $"{"NA",13}";
                PCMETable += $"\n";

                PCMETable += $"{"Sigma:",15}";
                for (int i = 0; i < N; i++)
                    if (missingCounts[i] + totIonCounts[i] > 0)
                        PCMETable += $"{Math.Sqrt((double)missingSigma2[i]) / (double)(missingCounts[i] + totIonCounts[i]),13:P1}";
                    else
                        PCMETable += $"{"NA",13}";
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

                PCMETable += $"{"Sigma:",15}";
                for (int i = 0; i < N; i++)
                    PCMETable += $"{(int)Math.Round(Math.Sqrt((double)missingSigma2[i])),13:N0}";
                PCMETable += $"\n";
                PCMETable += $"\n";

                double[] Raw = new double[N];
                int totSelected = 0;
                for (int i = 0; i < N; i++) totSelected += totIonCounts[i];
                PCMETable += $"{"Raw Comp.:",15}";
                for (int i = 0; i < N; i++)
                {
                    Raw[i] = (double)totIonCounts[i] / (double)totSelected;
                    PCMETable += $"{Raw[i],13:P3}";
                }
                PCMETable += $"\n";

                totSelected = 0;
                for (int i = 0; i < N; i++) totSelected += totIonCounts[i] + missingCounts[i];
                PCMETable += $"{"Correct. Comp.:",15}";
                for (int i = 0; i < N; i++)
                {
                    DeadCor[i] = (double)(totIonCounts[i] + missingCounts[i]) / (double)totSelected;
                    PCMETable += $"{DeadCor[i],13:P3}";
                }
                PCMETable += $"\n";

                PCMETable += $"{"Cor. Factor:",15}";
                for (int i = 0; i < N; i++)
                    PCMETable += $"{((double)(totIonCounts[i] + missingCounts[i]) / (double)totSelected) / Raw[i],13:N3}";
                PCMETable += $"\n";
                PCMETable += $"\n";
            }
            MultisInformation.Infobyiontype = PCMETable;

            string CorrelatedTable = $"All Multi-Hit Pairs: dpMultis[First Ion,Second Ion,dp=0]\n{"",13}";
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

                CorrelatedTable += $"{"Sigma:",13}";
                for (int i = 0; i < N; i++)
                    CorrelatedTable += $"{(int)Math.Round(Math.Sqrt((double)missingSigma2[i])),13:N0}";
                CorrelatedTable += $"\n";

                CorrelatedTable += $"{"Pair:",13}";
                for (int i = 0; i < N; i++)
                    CorrelatedTable += $"{missingPairs[i],13}";
                CorrelatedTable += "\n";

                CorrelatedTable += $"{"SS Det. Eff.:",13}";
                for (int i = 0; i < N; i++)
                {
                    int tot = dpCorMultis[i, i, 0] + missingCounts[i];
                    if (tot == 0 || missingCounts[i] <=0 || missingPairs[i].Equals("None"))
                        CorrelatedTable += $"{"NA",13}";
                    else
                        CorrelatedTable += $"{(double)dpCorMultis[i, i, 0] / (double)(tot),13:P2}";
                }
                CorrelatedTable += "\n";

                CorrelatedTable += "\n";
            }

            CorrelatedTable += $"Ncor: {Ncor,13:N0}\n";
            CorrelatedTable += $"Wij Table:  Calculated from Ncor and P[i]s based on Correlated Multis Table + 2xMissing.\n";
            CorrelatedTable += $"            Negative values indicate Mij < 16 counts.\n{"",13}";
            {
                //N-->N+2
                for (int i = 0; i < N+2; i++)
                    CorrelatedTable += $"{rangeNames[i],13}";
                CorrelatedTable += "\n";
                
                string Fracs = $"{"P[i]:",13}";
                //N-->N+2
                for (int i = 0; i < N+2; i++)
                {
                    Fracs += $"{P[i],13:N4}";
                    CorrelatedTable += $"{rangeNames[i],13}";
                    //N-->N+2
                    for (int j = 0; j < N + 2; j++)
                    {
                        if (j >= i)
                        {
                            if (dpCorMultis[i,j,0] < 16)
                                CorrelatedTable += $"{-WijMatrix[i, j],13:N2}";
                            else
                                CorrelatedTable += $"{WijMatrix[i, j],13:N2}";
                        }
                        else
                            CorrelatedTable += $"{"-",13}";
                    }
                    CorrelatedTable += "\n";
                }
                CorrelatedTable += Fracs + "\n";            
                CorrelatedTable += "\n";
            }
            
            MultisInformation.Correlatedmultistable = CorrelatedTable;

            int totSelectedCorrelatedTableAndMissing = 0;
            for (int i = 0; i < N; i++)
            {
                totSelectedCorrelatedTableAndMissing += missingCounts[i];
                for (int j = 0; j < N; j++)
                    totSelectedCorrelatedTableAndMissing += dpCorMultis[i, j, 0];
            }

            /*
            double totTerm = 0d;
            string CorrelatedFixedNormalizedTable = $"Correlated Multis + Missing : Composition Weighted, Same Total Multis Expected\n{"",13}";
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
            MultisInformation.CorrelatedmultistableNormalized = CorrelatedFixedNormalizedTable;
            */

            //a la Saxey
            string CorrelatedFixedStdevTable = $"Saxey Correlation Table: #Stdevs from expected: (Mij - eij)/sqrt(eij)\n{"",13}";
            CorrelatedFixedStdevTable +=       $"       eij = 2PiPjN or Pi^2N where Pi is bulk based and N is the total corrected correlated pairs.\n{"",13}";
            {
                {
                    double totTerm = 0d;
                    for (int i = 0; i < N; i++)
                        CorrelatedFixedStdevTable += $"{rangeNames[i],13}";
                    CorrelatedFixedStdevTable += "\n";
                    for (int i = 0; i < N; i++)
                    {
                        CorrelatedFixedStdevTable += $"{rangeNames[i],13}";
                        for (int j = 0; j < N; j++)
                        {
                            if (j < i) CorrelatedFixedStdevTable += $"{"",13}";
                            else if (i != j)
                            {
                                double eij = (2d * DeadCor[i] * DeadCor[j] * (double)totSelectedCorrelatedTableAndMissing);
                                double term = (double)(dpCorMultis[i, j, 0] + dpCorMultis[j, i, 0] - (int)eij) / Math.Sqrt(eij);
                                totTerm += term;
                                CorrelatedFixedStdevTable += $"{term,13:N1}";
                            }
                            else
                            {
                                double eij = DeadCor[i] * DeadCor[j] * (double)totSelectedCorrelatedTableAndMissing;
                                double term = (double)(dpCorMultis[i, j, 0] + missingCounts[i] - (int)eij) / Math.Sqrt(eij);
                                totTerm += term;
                                CorrelatedFixedStdevTable += $"{term,13:N1}";
                            }
                        }
                        CorrelatedFixedStdevTable += "\n";
                    }
                    //CorrelatedFixedStdevTable += $"Table Total: {totTerm,13:N3}\n";
                    CorrelatedFixedStdevTable += "\n";
                }
            }
            MultisInformation.CorrelatedmultistableStdevs = CorrelatedFixedStdevTable;

            string UncorrelatedTable = $"All Multi-Hit Pairs: dpMultis[First Ion,Second Ion,dp=0]\n{"",13}";
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
            MultisInformation.Uncorrelatedmultistable = UncorrelatedTable;

            string CorrelatedPseudoTable = $"All: dpMultis[First Ion,Second Ion,dp=1...{Parameters.IPseudoMultiMaxdp}]\n{"",13}";
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
            MultisInformation.Correlatedpseudomultistable = CorrelatedPseudoTable;
            
            string UncorrelatedPseudoTable = $"All: dpMultis[First Ion,Second Ion,dp=1...{Parameters.IPseudoMultiMaxdp}]\n{"",13}";
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
            MultisInformation.Uncorrelatedpseudomultistable = UncorrelatedPseudoTable;

            string Summary = "";
            {
                string ConsideredRanges = $"Considered Ranges:   {N,5:N0}\n";
                for (int i = 0; i < rangeMins.Count(); i++)
                    ConsideredRanges += $"                           {i} {rangeNames[i],7}: {rangeMins[i],7:N3} - {rangeMaxs[i],7:N3}\n";

                Summary += $"  Total Defined Ranges:{NTotal,5:N0}\n";
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
                Summary += $"  *Note: Multis Table includes all combinations of pairs for multi-events with >2 ions.\n";

                //hreg 0 = ranged, 1 = all
                string HregSummary = "\n  Multi-dp=0 Distribution: ";
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
                Summary += $"  Multis dp=0:\n";
                Summary += $"    All:                     {dpMultis[N + 2, N + 2, 0],13:N0}\n";
                Summary += $"    Considered:              {getConsideredTotal(dpMultis, 0),13:N0}\n";
                Summary += $"    Considered & Correlated: {getConsideredTotal(dpCorMultis, 0),13:N0}\n";
                Summary += $"    Considered & Uncorr:     {getConsideredTotal(dpUncMultis, 0),13:N0}\n";
                Summary += "\n";
                Summary += $"  Pseudo-Doubles dp=1...{DPMax:N0}:\n";
                int sum = 0; for (int dp = 1; dp <= DPMax; dp++) sum += dpMultis[N + 2, N + 2, dp];
                Summary += $"    All:                     {sum,13:N0}\n";
                sum = 0; for (int dp = 1; dp <= DPMax; dp++) sum += (getConsideredTotal(dpMultis, dp));
                Summary += $"    Considered:              {sum,13:N0}\n";
                sum = 0; for (int dp = 1; dp <= DPMax; dp++) sum += (getConsideredTotal(dpCorMultis, dp));
                Summary += $"    Considered & Correlated: {sum,13:N0}\n";
                sum = 0; for (int dp = 1; dp <= DPMax; dp++) sum += (getConsideredTotal(dpUncMultis, dp));
                Summary += $"    Considered & UnCorr:     {sum,13:N0}\n";
                Summary += "\n";
                Summary += $"  Key Range: {rangeNames[keyRange],7}: {rangeMins[keyRange],7:N3} - {rangeMaxs[keyRange],7:N3}\n";
                Summary += $"  DR:         {aveDR:P3}\n";
                Summary += $"  ToF:     {aveToF,8:N0} \u00B1 {stdevToF:N0} ns\n";
                Summary += $"  Voltage: {aveVolt,8:N0} \u00B1 {stdevVolt:N0} V\n";
                Summary += "\n";

                /*
                Summary += "  Looking for trends with Pseudo Multis from uncorrected data:\n";
                float SS0 = (float)(getSSConsideredTotal(dpCorMultis, 0));
                float SSp0 = (float)(getSSpConsideredTotal(dpCorMultis, 0));
                float SSp1 = (float)(getSSpConsideredTotal(dpCorMultis, 1));
                float SS1 = (float)(getSSConsideredTotal(dpCorMultis, 1));
                float SS0FractionDetected = SS0 / SSp0 * SSp1 / SS1;
                Summary += "    S=Same, S'=Not Same, 0: dp=0 or same pulse, 1: dp=1 or adjacent pulses\n";
                Summary += $"    SS0:  {SS0,13:N0}\n";
                Summary += $"    SS'0: {SSp0,13:N0}\n";
                Summary += $"    SS1:  {SS1,13:N0}\n";
                Summary += $"    SS'1: {SSp1,13:N0}\n";
                Summary += $"    Corr: SS0/SS'0 / SS1/SS'1 = {SS0FractionDetected:P2}\n";
                Summary += $"    Correlated same-same/not-same ratio for same-pulse multis vs. psudo-multis\n";
                Summary += $"    (Deadtime affected/not deadtime affected same-pulse vs. same ratio with no deadtime effect)\n\n";

                SS0 = (float)(getSSConsideredTotal(dpUncMultis, 0));
                SSp0 = (float)(getSSpConsideredTotal(dpUncMultis, 0));
                SSp1 = (float)(getSSpConsideredTotal(dpUncMultis, 1));
                SS1 = (float)(getSSConsideredTotal(dpUncMultis, 1));
                SS0FractionDetected = SS0 / SSp0 * SSp1 / SS1;
                Summary += $"    SS0:  {SS0,13:N0}\n";
                Summary += $"    SS'0: {SSp0,13:N0}\n";
                Summary += $"    SS1:  {SS1,13:N0}\n";
                Summary += $"    SS'1: {SSp1,13:N0}\n";
                Summary += $"    Uncorr: SS0/SS'0 / SS1/SS'1 = {SS0FractionDetected:P2}\n";
                Summary += $"    Uncorrelated same-same/not-same ratio for same-pulse multis vs. psudo-multis\n";
                Summary += $"    (mostly unaffected/not deadtime affected same-pulse vs. same ratio with no deadtime effect)\n";
                Summary += $"    (these are approximately predictable, governed mainly by Poisson statistics and DR --> 100%)\n";
                Summary += "\n";
                */
            }
            MultisInformation.Summary = Summary;

            MultisInformation.SiIsotope = getSiIsotopeString();

            MultisInformation.Value = "Overview:\n" + MultisInformation.Overview +
                "\n\nSimple Descriptors:\n" + MultisInformation.Simple +
                "\n\nPCME Table:\n" + MultisInformation.Infobyiontype +
                "\n\nCorrelated Multis Table:\n" + MultisInformation.Correlatedmultistable +
                //"\n\nCorrelated Multis Table Normalized:\n" + MultisInformation.CorrelatedmultistableNormalized +
                "\n\nCorrelated Multis Table Stdevs:\n" + MultisInformation.CorrelatedmultistableStdevs +
                "\n\nUncorrelated Multis Table:\n" + MultisInformation.Uncorrelatedmultistable +
                "\n\nCorrelated Pseudo-Multis Table:\n" + MultisInformation.Correlatedpseudomultistable +
                "\n\nUncorrelated Pseudo-Multis Table:\n" + MultisInformation.Uncorrelatedpseudomultistable +
                "\n\nSummary:\n" + MultisInformation.Summary +
                "\n\nSi Isotope Fractions:" + MultisInformation.SiIsotope;
        }
        public string getSiIsotopeString()
        {
            string s = "";
            bool yesSipp = false;
            bool yesSip = false;
            int[] Sipp = new int[] { -1, -1, -1 };
            int[] SippBgd = new int[] { -1, -1, -1 };
            int[] Sip = new int[] { -1, -1, -1 };
            int[] SipBgd = new int[] { -1, -1, -1 };
            for (int i = 0; i < rangeNames.Length; i++)
            {
                if (rangeNames[i].Equals("14.0-Si"))
                    Sipp[0] = i;
                else if (rangeNames[i].Equals("14.5-Si"))
                    Sipp[1] = i;
                else if (rangeNames[i].Equals("15.0-Si"))
                    Sipp[2] = i;
                else if (rangeNames[i].Equals("28.0-Si"))
                    Sip[0] = i;
                else if (rangeNames[i].Equals("29.0-Si"))
                    Sip[1] = i;
                else if (rangeNames[i].Equals("30.0-Si"))
                    Sip[2] = i;
            }

            if (Sipp[0] >= 0 && Sipp[1] >= 0 && Sipp[2] >=0)
                yesSipp = true;
            if (Sip[0] >= 0 && Sip[1] >= 0 && Sip[2] >= 0)
                yesSip = true;

            /*
            if (yesSip || yesSipp)
            {

                s += $"{"",20}";
                if (yesSipp)
                    for (int i = 0; i < 3; i++)
                        s += $"{rangeNames[Sipp[i]],13}";
                if (yesSip)
                    for (int i = 0; i < 3; i++)
                        s += $"{rangeNames[Sip[i]],13}";
                s += "\n";

                s += $"{"Uncorr. Cts.:",20}";
                if (yesSipp)
                    for (int i = 0; i < 3; i++)
                        s += $"{multiIonTrueCounts[Sipp[i], 1],13:N0}";
                if (yesSip)
                    for (int i = 0; i < 3; i++)
                        s += $"{multiIonTrueCounts[Sip[i], 1],13:N0}";
                s += $"\n";

                s += $"{"Missing (by pairs):",20}";
                if (yesSipp)
                    for (int i = 0; i < 3; i++)
                        s += $"{missingCounts[Sipp[i]],13:N0}";
                if (yesSip)
                    for (int i = 0; i < 3; i++)
                        s += $"{missingCounts[Sip[i]],13:N0}";
                s += $"\n";

                s += $"{"Missing (by 3s):",20}";
                double[] AA = new double[3] { -1, -1, -1 };
                if (yesSipp)
                {
                    if (dpCorMultis[Sipp[1], Sipp[2], 0] + dpCorMultis[Sipp[2], Sipp[1], 0] > 0)
                        AA[0] = 0.5d * (double)(dpCorMultis[Sipp[0], Sipp[1], 0] + dpCorMultis[Sipp[1], Sipp[0], 0])
                        * (double)(dpCorMultis[Sipp[0], Sipp[2], 0] + dpCorMultis[Sipp[2], Sipp[0], 0])
                        / (double)(dpCorMultis[Sipp[1], Sipp[2], 0] + dpCorMultis[Sipp[2], Sipp[1], 0]);
                    else
                        AA[0] = 0;
                    if (dpCorMultis[Sipp[0], Sipp[2], 0] + dpCorMultis[Sipp[2], Sipp[0], 0] > 0)
                        AA[1] = 0.5d * (double)(dpCorMultis[Sipp[0], Sipp[1], 0] + dpCorMultis[Sipp[1], Sipp[0], 0])
                            * (double)(dpCorMultis[Sipp[1], Sipp[2], 0] + dpCorMultis[Sipp[2], Sipp[1], 0])
                            / (double)(dpCorMultis[Sipp[0], Sipp[2], 0] + dpCorMultis[Sipp[2], Sipp[0], 0]);
                    else
                        AA[1] = 0;
                    if (dpCorMultis[Sipp[0], Sipp[1], 0] + dpCorMultis[Sipp[1], Sipp[0], 0] > 0)
                        AA[2] = 0.5d * (double)(dpCorMultis[Sipp[2], Sipp[1], 0] + dpCorMultis[Sipp[1], Sipp[2], 0])
                            * (double)(dpCorMultis[Sipp[0], Sipp[2], 0] + dpCorMultis[Sipp[2], Sipp[0], 0])
                            / (double)(dpCorMultis[Sipp[0], Sipp[1], 0] + dpCorMultis[Sipp[1], Sipp[0], 0]);
                    else
                        AA[2] = 0;
                }

                double[] A = new double[3] { -1, -1, -1 };
                if (yesSip)
                {
                    if (dpCorMultis[Sip[1], Sip[2], 0] + dpCorMultis[Sip[2], Sip[1], 0] > 0)
                        A[0] = 0.5d * (double)(dpCorMultis[Sip[0], Sip[1], 0] + dpCorMultis[Sip[1], Sip[0], 0])
                            * (double)(dpCorMultis[Sip[0], Sip[2], 0] + dpCorMultis[Sip[2], Sip[0], 0])
                            / (double)(dpCorMultis[Sip[1], Sip[2], 0] + dpCorMultis[Sip[2], Sip[1], 0]);
                    else
                        A[0] = 0;
                    if (dpCorMultis[Sip[0], Sip[2], 0] + dpCorMultis[Sip[2], Sip[0], 0] > 0)
                        A[1] = 0.5d * (double)(dpCorMultis[Sip[0], Sip[1], 0] + dpCorMultis[Sip[1], Sip[0], 0])
                            * (double)(dpCorMultis[Sip[1], Sip[2], 0] + dpCorMultis[Sip[2], Sip[1], 0])
                            / (double)(dpCorMultis[Sip[0], Sip[2], 0] + dpCorMultis[Sip[2], Sip[0], 0]);
                    else
                        A[1] = 0;
                    if (dpCorMultis[Sip[0], Sip[1], 0] + dpCorMultis[Sip[1], Sip[0], 0] > 0)
                        A[2] = 0.5d * (double)(dpCorMultis[Sip[2], Sip[1], 0] + dpCorMultis[Sip[1], Sip[2], 0])
                            * (double)(dpCorMultis[Sip[0], Sip[2], 0] + dpCorMultis[Sip[2], Sip[0], 0])
                            / (double)(dpCorMultis[Sip[0], Sip[1], 0] + dpCorMultis[Sip[1], Sip[0], 0]);
                    else
                        A[2] = 0;
                    if (dpCorMultis[Sip[0], Sip[1], 0] + dpCorMultis[Sip[1], Sip[0], 0] > 0)
                        A[2] = 0.5d * (double)(dpCorMultis[Sip[2], Sip[1], 0] + dpCorMultis[Sip[1], Sip[2], 0])
                            * (double)(dpCorMultis[Sip[0], Sip[2], 0] + dpCorMultis[Sip[2], Sip[0], 0])
                            / (double)(dpCorMultis[Sip[0], Sip[1], 0] + dpCorMultis[Sip[1], Sip[0], 0]);
                    else
                        A[2] = 0;
                }

                if (yesSipp)
                    for (int i = 0; i < 3; i++)
                        s += $"{(int)(AA[i] + 0.5d) - dpCorMultis[Sipp[i], Sipp[i], 0],13:N0}";
                if (yesSip)
                    for (int i = 0; i < 3; i++)
                        s += $"{(int)(A[i] + 0.5d) - dpCorMultis[Sip[i], Sip[i], 0],13:N0}";
                s += $"\n";

                s += $"{"COR Cts.:",20}";
                if (yesSipp)
                    for (int i = 0; i < 3; i++)
                        s += $"{multiIonTrueCounts[Sipp[i], 0],13:N0}";
                if (yesSip)
                    for (int i = 0; i < 3; i++)
                        s += $"{multiIonTrueCounts[Sip[i], 0],13:N0}";
                s += $"\n";

                s += $"{"All Cts.:",20}";
                if (yesSipp)
                    for (int i = 0; i < 3; i++)
                        s += $"{totIonCounts[Sipp[i]],13:N0}";
                if (yesSip)
                    for (int i = 0; i < 3; i++)
                        s += $"{totIonCounts[Sip[i]],13:N0}";
                s += $"\n";

                s += $"{"All Bgd Cts.:",20}";
                if (yesSipp)
                    for (int i = 0; i < 3; i++)
                        s += $"{rangeBgd[Sipp[i]],13:N0}";
                if (yesSip)
                    for (int i = 0; i < 3; i++)
                        s += $"{rangeBgd[Sip[i]],13:N0}";
                s += $"\n";

                s += $"{"Uncor Frac.:",20}";
                if (yesSipp)
                {
                    int total = 0;
                    for (int i = 0; i < 3; i++)
                        total += multiIonTrueCounts[Sipp[i], 1];
                    for (int i = 0; i < 3; i++)
                        s += $"{(double)multiIonTrueCounts[Sipp[i], 1] / (double)total,13:P2}";
                }
                if (yesSip)
                {
                    int total = 0;
                    for (int i = 0; i < 3; i++)
                        total += multiIonTrueCounts[Sip[i], 1];
                    for (int i = 0; i < 3; i++)
                        s += $"{(double)multiIonTrueCounts[Sip[i], 1] / (double)total,13:P2}";
                }
                s += $"\n";

                s += $"{"COR Raw Frac.:",20}";
                if (yesSipp)
                {
                    int total = 0;
                    for (int i = 0; i < 3; i++)
                        total += multiIonTrueCounts[Sipp[i], 0];
                    for (int i = 0; i < 3; i++)
                        s += $"{(double)multiIonTrueCounts[Sipp[i], 0] / (double)total,13:P2}";
                }
                if (yesSip)
                {
                    int total = 0;
                    for (int i = 0; i < 3; i++)
                        total += multiIonTrueCounts[Sip[i], 0];
                    for (int i = 0; i < 3; i++)
                        s += $"{(double)multiIonTrueCounts[Sip[i], 0] / (double)total,13:P2}";
                }
                s += $"\n";

                s += $"{"COR Frac. Pairs:",20}";
                if (yesSipp)
                {
                    int total = 0;
                    for (int i = 0; i < 3; i++)
                        total += multiIonTrueCounts[Sipp[i], 0] + 2 * missingCounts[Sipp[i]];
                    for (int i = 0; i < 3; i++)
                        s += $"{(double)(multiIonTrueCounts[Sipp[i], 0] + 2 * missingCounts[Sipp[i]]) / (double)total,13:P2}";
                }
                if (yesSip)
                {
                    int total = 0;
                    for (int i = 0; i < 3; i++)
                        total += multiIonTrueCounts[Sip[i], 0] + 2 * missingCounts[Sip[i]];
                    for (int i = 0; i < 3; i++)
                        s += $"{(double)(multiIonTrueCounts[Sip[i], 0] + 2 * missingCounts[Sip[i]]) / (double)total,13:P2}";
                }
                s += $"\n";

                s += $"{"All Frac. Pairs:",20}";
                if (yesSipp)
                {
                    int total = 0;
                    for (int i = 0; i < 3; i++)
                        total += totIonCounts[Sipp[i]] + missingCounts[Sipp[i]] - rangeBgd[Sipp[i]];
                    for (int i = 0; i < 3; i++)
                        s += $"{(double)(totIonCounts[Sipp[i]] + missingCounts[Sipp[i]] - rangeBgd[Sipp[i]]) / (double)total,13:P2}";
                }
                if (yesSip)
                {
                    int total = 0;
                    for (int i = 0; i < 3; i++)
                        total += totIonCounts[Sip[i]] + missingCounts[Sip[i]] - rangeBgd[Sip[i]];
                    for (int i = 0; i < 3; i++)
                        s += $"{(double)(totIonCounts[Sip[i]] + missingCounts[Sip[i]] - rangeBgd[Sip[i]]) / (double)total,13:P2}";
                }
                s += $"\n";

                s += $"{"COR Frac. 3s:",20}";
                if (yesSipp)
                {
                    int total = 0;
                    for (int i = 0; i < 3; i++)
                        total += multiIonTrueCounts[Sipp[i], 0] + 2 * (int)(AA[i] + 0.5d);
                    for (int i = 0; i < 3; i++)
                        s += $"{(double)(multiIonTrueCounts[Sipp[i], 0] + 2 * (int)(AA[i] + 0.5d)) / (double)total,13:P2}";
                }
                if (yesSip)
                {
                    int total = 0;
                    for (int i = 0; i < 3; i++)
                        total += multiIonTrueCounts[Sip[i], 0] + 2 * (int)(A[i] + 0.5d);
                    for (int i = 0; i < 3; i++)
                        s += $"{(double)(multiIonTrueCounts[Sip[i], 0] + 2 * (int)(A[i] + 0.5d)) / (double)total,13:P2}";
                }
                s += $"\n";

                s += $"{"All Frac. 3s:",20}";
                if (yesSipp)
                {
                    int total = 0;
                    for (int i = 0; i < 3; i++)
                        total += totIonCounts[Sipp[i]] + (int)(AA[i] + 0.5d) - rangeBgd[Sipp[i]];
                    for (int i = 0; i < 3; i++)
                        s += $"{(double)(totIonCounts[Sipp[i]] + (int)(AA[i] + 0.5d) - rangeBgd[Sipp[i]]) / (double)total,13:P2}";
                }
                if (yesSip)
                {
                    int total = 0;
                    for (int i = 0; i < 3; i++)
                        total += totIonCounts[Sip[i]] + (int)(A[i] + 0.5d) - rangeBgd[Sip[i]];
                    for (int i = 0; i < 3; i++)
                        s += $"{(double)(totIonCounts[Sip[i]] + (int)(A[i] + 0.5d) - rangeBgd[Sip[i]]) / (double)total,13:P2}";
                }
                s += $"\n";

                s += $"{"All Frac. 3s Sigma:",20}";
                if (yesSipp)
                {
                    int totalN = 0;
                    int totalBgd = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        totalN += totIonCounts[Sipp[i]] + (int)(AA[i] + 0.5d);
                        totalBgd += rangeBgd[Sipp[i]];
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        double sigma = Math.Sqrt((double)(totIonCounts[Sipp[i]] + (int)(AA[i] + 0.5d) + rangeBgd[Sipp[i]]) //(NC+BC)
                                         * Math.Pow((double)(totalN - totIonCounts[Sipp[i]] - (int)(AA[i] + 0.5d) - totalBgd + rangeBgd[Sipp[i]]), 2d)//(NC'-BC')^2
                                         + (double)(totalN - totIonCounts[Sipp[i]] - (int)(AA[i] + 0.5d) + totalBgd - rangeBgd[Sipp[i]])//(NC'+BC')
                                         * Math.Pow((double)(totIonCounts[Sipp[i]] + (int)(AA[i] + 0.5d) - rangeBgd[Sipp[i]]), 2d))//(NC-BC)^2 
                                         / Math.Pow((double)(totalN - totalBgd), 2d);//(N-B)^2
                        s += $"{sigma,13:P2}";
                    }
                }
                if (yesSip)
                {
                    int totalN = 0;
                    int totalBgd = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        totalN += totIonCounts[Sip[i]] + (int)(A[i] + 0.5d);
                        totalBgd += rangeBgd[Sip[i]];
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        double sigma = Math.Sqrt((double)(totIonCounts[Sip[i]] + (int)(A[i] + 0.5d) + rangeBgd[Sip[i]]) //(NC+BC)
                                                 * Math.Pow((double)(totalN - totIonCounts[Sip[i]] - (int)(A[i] + 0.5d) - totalBgd + rangeBgd[Sip[i]]), 2d)//(NC'-BC')^2
                                                 + (double)(totalN - totIonCounts[Sip[i]] - (int)(A[i] + 0.5d) + totalBgd - rangeBgd[Sip[i]])//(NC'+BC')
                                                 * Math.Pow((double)(totIonCounts[Sip[i]] + (int)(A[i] + 0.5d) - rangeBgd[Sip[i]]), 2d))//(NC-BC)^2 
                                                 / Math.Pow((double)(totalN - totalBgd), 2d);//(N-B)^2
                        s += $"{sigma,13:P2}";
                    }
                }
                s += $"\n\n";
            }
            */

            s += "Pseudo-Based Correction Validation (missing counts corrected, but not background corrected):\n";
            s += "Pseudo multis possess the Pi for correlated events unaffected by deadtime, but may not have sufficient counting statistics.\n";
            s += $"{"",15}";
            for (int i = 0; i < N; i++)
                s += $"{rangeNames[i],13}";
            s += $"\n";
            
            s += $"{"CSR:",15}";
            for (int i = 0; i < N; i++)
            {
                bool found = false;
                int numerator = -1;
                int denominator = -1;
                returnCSRPair(i, ref found, ref numerator, ref denominator);
                if (found)
                    s += $"{(double)(totIonCounts[numerator] + missingCounts[numerator]) / (double)(totIonCounts[denominator] + missingCounts[denominator]),13:N3}";
                else
                    s += $"{"NA",13}";
            }
            s += $"\n";
            
            s += $"{"CSRcor:",15}";
            for (int i = 0; i < N; i++)
            {
                bool found = false;
                int numerator = -1;
                int denominator = -1;
                returnCSRPair(i, ref found, ref numerator, ref denominator);
                if (found)
                    s += $"{(double)(multiIonTrueCounts[numerator, 0] + 2*missingCounts[numerator]) / (double)(multiIonTrueCounts[denominator,0] + 2*missingCounts[denominator]),13:N3}";
                else
                    s += $"{"NA",13}";
            }
            s += $"\n";

            s += $"{"CSRpcor:",15}";
            for (int i = 0; i < N; i++)
            {
                bool found = false;
                int numerator = -1;
                int denominator = -1;
                returnCSRPair(i, ref found, ref numerator, ref denominator);
                if (found)
                {
                    double M11prime = (double)dpCorMultis[numerator, numerator, DPMax + 1];
                    double M22prime = (double)dpCorMultis[denominator, denominator, DPMax + 1];
                    if (M22prime > 0d)
                        s += $"{Math.Sqrt(M11prime/M22prime),13:N3}";
                    else
                        s += $"{"NA",13}";
                }
                else
                    s += $"{"NA",13}";
            }
            s += $"\n";

            s += $"{"MissingCor:",15}";
            for (int i = 0; i < N; i++)
                    s += $"{missingCounts[i],13:N0}";
            s += $"\n";

            s += $"{"Sigma:",15}";
            for (int i = 0; i < N; i++)
                s += $"{(int)Math.Round(Math.Sqrt((double)missingSigma2[i])),13:N0}";
            s += $"\n";

            s += $"{"MissingPcorCSR:",15}";
            string sigmaString = $"{"Sigma:",15}";
            for (int i = 0; i < N; i++)
            {
                bool found = false;
                int numerator = -1;
                int denominator = -1;
                returnCSRPair(i, ref found, ref numerator, ref denominator);
                if (found)
                {
                    int j = -1;
                    if (i == numerator)
                        j = denominator;
                    else
                        j = numerator;
                    int MissingCountsi = 0;
                    int MissingCountsj = 0;
                    int MissingSigma2i = 0;
                    int MissingSigma2j = 0;
                    string MissingPairsi = "";
                    string MissingPairsj = "";
                    returnPCorMissingCounts(i, j, ref MissingCountsi, ref MissingSigma2i, ref MissingCountsj, ref MissingSigma2j, ref MissingPairsi, ref MissingPairsj);
                    s += $"{MissingCountsi,13:N0}";
                    sigmaString += $"{(int)Math.Round(Math.Sqrt((double)MissingSigma2i)),13:N0}";
                }
                else
                {
                    s += $"{"NA",13}";
                    sigmaString += $"{"NA",13}";
                }
            }
            s += $"\n";
            s += sigmaString + $"\n";

            return s;
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
        public void fillWijMatrix()
        {
            // Homogeneous:
            //        P[i] - the fraction of COR events for the ith range (including ranged, unranged and other correlated events)
            //             - this value also includes the missing counts for the range, so it is updated each iteration
            //        multiIonTrueCounts[,0] - overcounting corrected true number of correlated counts detected, 0=correlated, 1=uncorrelated
            //                           - includes ranged, unranged and other correlated events
            //        missingCounts[] - the number of missing counts
            //                        - for each Mii missing, it one missing count for composition because the other was detected as a single, uncorrelated
            //                        - for correlated compositions, P[i], then we need to add in 2x because of the missing COR pair
            //        previousMissingCounts - so that we can display the Missing Matrix computations
            //        WijMatrix[,] - the weights for each Mij = pi x pj x wij x Ncor
            //        Ncor - the number of correlated pairs implied by considering all Mijs
            //

            // Heterogeneous:
            //      Do pair-wise deadtime correction
            //      Can have different ion pairs used for each ion (A might use AB, but B might be from BC)
            //      
            //      Hierarchy:
            //        Same element/molecule - same charge state
            //        Same element/molecule - different charge states
            //        Highest statistics  
            //

            //Now other and unranged, but other*other and unranged*unraged same-same can be ignored
            P = new double[N + 2]; // Do other and unranged too
            WijMatrix = new double[N + 2, N + 2]; //Matrix will be N+2xN+2, no totals

            Ncor = dpCorMultis[N+2,N+2,0] + missingCounts[N]; //Total correlated counts, including missing, overcounted at the moment

            //Initialize
            //Note: The missing does not use correlated P[i] values, but it is instructive to calculate P[i] values after correction
            //      and infer what the weights wij were based on the total number of correlated pairs expcted
            for (int i = 0; i < N; i++)
                P[i] = 0.5d * (dpCorMultis[i, N + 2, 0] + dpCorMultis[N + 2, i, 0] + 2d * missingCounts[i])/ (double)Ncor;
            P[N] = 0.5d * (dpCorMultis[N, N + 2, 0] + dpCorMultis[N + 2, N, 0]) / (double)Ncor;
            P[N+1] = 0.5d * (dpCorMultis[N + 1, N + 2, 0] + dpCorMultis[N + 2, N + 1, 0]) / (double)Ncor;

            // What about totaltruecounts???  Reinitialize the arrays depending on order of calls to fill multis!!
            int TotalTrueCORCounts = useMultiIonTrueCounts();
            missingCounts[N + 1] = TotalTrueCORCounts;

            for (int i = 0; i < N+2; i++)
                for (int j = 0; j < N+2; j++)
                {
                    if (i == j)
                    {
                        if (i < N)
                        {
                            double wij = (double)(dpCorMultis[i, j, 0] + missingCounts[i]) / ((double)Ncor * P[i] * P[j]);
                            WijMatrix[i, j] = wij;
                        }
                        else
                        {
                            double wij = (double)(dpCorMultis[i, j, 0]) / ((double)Ncor * P[i] * P[j]);
                            WijMatrix[i, j] = wij;
                        }
                    }
                    else
                    {
                        double wij = 0.5d * (double)(dpCorMultis[i, j, 0]+ dpCorMultis[j, i, 0]) / ((double)Ncor * P[i] * P[j]);
                        WijMatrix[i, j] = wij;
                    }
                }
        }
        int useMultiIonTrueCounts()
        {
            if (multiPiCalcUse == EMultiPiCalc.All)
                return multiIonTrueCounts[N + 2, 0] + missingCounts[N] + missingCounts[N];
            else if (multiPiCalcUse == EMultiPiCalc.NotOther)
                return multiIonTrueCounts[N + 2, 0] - multiIonTrueCounts[N, 0] + missingCounts[N] + missingCounts[N];
            else if (multiPiCalcUse == EMultiPiCalc.NotUnranged)
                return multiIonTrueCounts[N + 2, 0] - multiIonTrueCounts[N + 1, 0] + missingCounts[N] + missingCounts[N];
            else //(multiPiCalcUse == EMultiPiCalc.NotEither)
                return multiIonTrueCounts[N + 2, 0] - multiIonTrueCounts[N, 0] - multiIonTrueCounts[N + 1, 0] + missingCounts[N] + missingCounts[N];
        }
        public void DeterminePisFromMissingAndTrueCORCounts()
        {
            //Determine Pis: Total = all TrueCounts + 2 COR counts for each missing count - other - unranged
            //missingCounts are based on previous iteration
            //missingCounts[N+1] is the total number true counts plus missing counts from previous iteration
            for (int i = 0; i < N + 2; i++)
            {
                if (i < N)
                    P[i] = (double)(multiIonTrueCounts[i, 0] + missingCounts[i] + missingCounts[i]) / (double)missingCounts[N + 1];
                else if (i == N)
                {
                    if (multiPiCalcUse == EMultiPiCalc.All || multiPiCalcUse == EMultiPiCalc.NotUnranged)
                        P[i] = (double)(multiIonTrueCounts[i, 0]) / (double)missingCounts[N + 1];
                    else
                        P[i] = 0d;
                }
                else //(i == N+1)
                {
                    if (multiPiCalcUse == EMultiPiCalc.All || multiPiCalcUse == EMultiPiCalc.NotOther)
                        P[i] = (double)(multiIonTrueCounts[i, 0]) / (double)missingCounts[N + 1];
                    else
                        P[i] = 0d;
                }
            }
        }
        public void fillMissing()
        {
            // Do deadtime corrections:
            //      1) 3 or more isotopes of same chemical/molecular species and same charge state
            //            M11 = p12N = ½ M12 M13/ M23 = ½(2p1p2N)(2p1p3N)/ (2p2p3N),
            //            M11 = ½ M12 M13/ M23,
            //            M22 = ½ M12 M23/ M13,
            //            Mii = ½ M1i M2i/ M12,
            //            sigmaMii = Mii√(1 / M12 + 1 / M13 + 1 / M23) for i = 1 or 2,
            //            sigmaMii = Mii√(1 / M12 + 1 / M1i + 1 / M2i).
            //      2) 2 isotopes of same chemical/molecular species and same charge state
            //            pi based on all data (rather than pi based on natural abundance)
            //            better to use non-background corrected p's
            //            M11 = ½ M12 p1/p2,
            //            M22 = ½ M12 p2/p1,
            //            sigmaMii = Mii√(1/M12 + p1(1-p1)/Nall + p2(1-p2)/Nall)
            //      3) 2 charge states of same chemical/molecular species
            //            M11 = ¼ M12 M12’/ M22’,
            //            M22 = ¼ M12 M12’/ M11’,
            //            sigmaMii = Mii√(1/M12 + 1/M12' + 1/Mjj')
            //      Ignore the rest
            //

            missingCounts = new int[N + 2]; //Only N will have missing, still totals, added based on TotalTrueCORCounts
            missingSigma2 = new int[N];
            missingPairs = new string[N];

            //Initialize
            for (int i = 0; i < N; i++)
            {
                missingCounts[i] = 0; //This is the total of all missingCounts (expected - detected)
                missingPairs[i] = "";
            }
            missingCounts[N] = 0; //Total of missingCounts array
            int TotalTrueCORCounts = useMultiIonTrueCounts();
            missingCounts[N + 1] = TotalTrueCORCounts; 

            for (int i = 0; i < N; i++)
            {
                if (!missingPairs[i].Equals("")) continue; // This item has already been paired
                List<int> matchesSameElementAndCS = new();
                List<int> matchesSameElement = new();
                matchesSameElementAndCS.Add(i);
                matchesSameElement.Add(i);
                for (int j = 0; j < N; j++)
                {
                    if (i != j && missingPairs[j].Equals("")) // Skip items already paired
                    {
                        if (sameElement(rangeNames[j], rangeNames[i]) && sameChargeState(rangeNames[j], rangeNames[i]))
                            matchesSameElementAndCS.Add(j);
                        else if (sameElement(rangeNames[j], rangeNames[i]))
                            matchesSameElement.Add(j);
                    }
                }

                if (matchesSameElementAndCS.Count >= 3)
                {
                    int biggest = -1;
                    int biggestValue = -1;
                    foreach (int j in matchesSameElementAndCS)
                    {
                        if (totIonCounts[j] > biggestValue)
                        {
                            biggestValue = totIonCounts[j];
                            biggest = j;
                        }
                    }
                    int bigger = -1;
                    int biggerValue = -1;
                    foreach (int j in matchesSameElementAndCS)
                    {
                        if (j != biggest && totIonCounts[j] > biggerValue)
                        {
                            biggerValue = totIonCounts[j];
                            bigger = j;
                        }
                    }
                    int big = -1;
                    int bigValue = -1;
                    foreach (int j in matchesSameElementAndCS)
                    {
                        if (j != biggest && j != bigger && totIonCounts[j] > bigValue)
                        {
                            bigValue = totIonCounts[j];
                            big = j;
                        }
                    }
                    missingCounts[biggest] = getMissing3s(biggest, bigger, big, ref missingSigma2[biggest]);
                    missingPairs[biggest] = rangeNames[biggest]; //Same name means it was done by 3s
                    missingCounts[bigger] = getMissing3s(bigger, biggest, big, ref missingSigma2[bigger]);
                    missingPairs[bigger] = rangeNames[bigger];
                    missingCounts[big] = getMissing3s(big, bigger, biggest, ref missingSigma2[big]);
                    missingPairs[big] = rangeNames[big];
                    foreach (int j in matchesSameElementAndCS)
                    {
                        if (j != big && j != bigger && j != biggest)
                        {
                            missingCounts[j] = getMissing3s(j, bigger, biggest, ref missingSigma2[j]);
                            missingPairs[j] = rangeNames[j]; //Same name means it was done by 3s
                        }
                    }
                }
                else if (matchesSameElementAndCS.Count == 2)
                {
                    int j = matchesSameElementAndCS[1];
                    double M12 = (double)dpCorMultis[i, j, 0] + (double)dpCorMultis[j, i, 0];
                    double NAll = (double)totIonCounts[N + 2];
                    double p1 = (double)totIonCounts[i] / NAll;
                    double p2 = (double)totIonCounts[j] / NAll;
                    if (useNatAbundances)
                    {
                        p1 = getIonFraction(rangeNames[i]);
                        p2 = getIonFraction(rangeNames[j]);
                    }
                    double M11 = 0.5d * M12 * p1 / p2;
                    double M22 = 0.5d * M12 * p2 / p1;



                    if ((int)M12!=0 && useNatAbundances)
                    {
                        missingCounts[i] = (int)Math.Round(M11) - dpCorMultis[i, i, 0];
                        //I fit some natural isotope uncertainties and the frac error ~ 0.0009^-0.67
                        missingSigma2[i] = (int)Math.Round(M11 * (M11 * (1d / M12 + 8.1e-7d * Math.Pow(p1, -1.34d) + 8.1e-7d * Math.Pow(p2, -1.34d)))) + dpCorMultis[i, i, 0];
                        missingCounts[j] = (int)Math.Round(M22) - dpCorMultis[j, j, 0];
                        missingSigma2[j] = (int)Math.Round(M22 * (M22 * (1d / M12 + 8.1e-7d * Math.Pow(p1, -1.34d) + 8.1e-7d * Math.Pow(p2, -1.34d)))) + dpCorMultis[j, j, 0];
                    }
                    else if ((int)M12==0 || totIonCounts[i]==0 || totIonCounts[j]==0)
                    {
                        missingCounts[i] = 0;
                        missingSigma2[i] = 0;
                        missingCounts[j] = 0;
                        missingSigma2[j] = 0;
                    }
                    else
                    {
                        missingCounts[i] = (int)Math.Round(M11) - dpCorMultis[i, i, 0];
                        missingSigma2[i] = (int)Math.Round(M11 * (M11 * (1d / M12 + p1 * (1d - p1) / NAll + p2 * (1d - p2) / NAll))) + dpCorMultis[i, i, 0];
                        missingCounts[j] = (int)Math.Round(M22) - dpCorMultis[j, j, 0];
                        missingSigma2[j] = (int)Math.Round(M22 * (M22 * (1d / M12 + p1 * (1d - p1) / NAll + p2 * (1d - p2) / NAll))) + dpCorMultis[j, j, 0];
                    }
                    missingPairs[i] = rangeNames[j];
                    missingPairs[j] = rangeNames[i];
                }
                else if (matchesSameElement.Count == 2) //Different CS
                {
                    int j = matchesSameElement[1];
                    returnPCorMissingCounts(i, j, ref missingCounts[i], ref missingSigma2[i], ref missingCounts[j],
                        ref missingSigma2[j], ref missingPairs[i], ref missingPairs[j]);
                    /*
                    double M12 = (double)dpCorMultis[i, j, 0] + (double)dpCorMultis[j, i, 0];
                    double M12prime = (double)dpCorMultis[i, j, DPMax+1] + (double)dpCorMultis[j, i, DPMax+1];
                    double M11prime = (double)dpCorMultis[i, i, DPMax + 1];
                    double M22prime = (double)dpCorMultis[j, j, DPMax + 1];
                    double M11 = 0.25d * M12 * M12prime / M22prime;
                    double M22 = 0.25d * M12 * M12prime / M11prime;
                    if (dpCorMultis[i, i, DPMax + 1] == 0 || dpCorMultis[j, j, DPMax + 1] == 0 || (int)M12 == 0 || (int)M12prime == 0)
                    {
                        missingCounts[i] = 0;
                        missingSigma2[i] = 0;
                        missingCounts[j] = 0;
                        missingSigma2[j] = 0;
                    }
                    else
                    {
                        missingCounts[i] = (int)Math.Round(M11) - dpCorMultis[i, i, 0];
                        missingSigma2[i] = (int)Math.Round(M11 * (M11 * (1d / M12 + 1d / M12prime + 1d / M22prime))) + dpCorMultis[i, i, 0];
                        missingCounts[j] = (int)Math.Round(M22) - dpCorMultis[j, j, 0];
                        missingSigma2[j] = (int)Math.Round(M22 * (M22 * (1d / M12 + 1d / M12prime + 1d / M11prime))) + dpCorMultis[j, j, 0];
                    }
                    missingPairs[i] = rangeNames[j];
                    missingPairs[j] = rangeNames[i];
                    */
                }
                else //No matches, use max
                {
                    missingCounts[i] = 0;
                    missingSigma2[i] = 0;
                    missingPairs[i] = "None";
                }
            }
            for (int i = 0; i < N; i++)
                missingCounts[N] += missingCounts[i];
        }       
        public void returnPCorMissingCounts(int i, int j, ref int MissingCountsi, ref int MissingSigma2i, ref int MissingCountsj, ref int MissingSigma2j, ref string MissingPairsi, ref string MissingPairsj)
        {
            double M12 = (double)dpCorMultis[i, j, 0] + (double)dpCorMultis[j, i, 0];
            double M12prime = (double)dpCorMultis[i, j, DPMax + 1] + (double)dpCorMultis[j, i, DPMax + 1];
            double M11prime = (double)dpCorMultis[i, i, DPMax + 1];
            double M22prime = (double)dpCorMultis[j, j, DPMax + 1];
            double M11 = 0.25d * M12 * M12prime / M22prime;
            double M22 = 0.25d * M12 * M12prime / M11prime;
            if (dpCorMultis[i, i, DPMax + 1] == 0 || dpCorMultis[j, j, DPMax + 1] == 0 || (int)M12 == 0 || (int)M12prime == 0)
            {
                MissingCountsi = 0;
                MissingSigma2i = 0;
                MissingCountsj = 0;
                MissingSigma2j = 0;
            }
            else
            {
                MissingCountsi = (int)Math.Round(M11) - dpCorMultis[i, i, 0];
                MissingSigma2i = (int)Math.Round(M11 * (M11 * (1d / M12 + 1d / M12prime + 1d / M22prime))) + dpCorMultis[i, i, 0];
                MissingCountsj = (int)Math.Round(M22) - dpCorMultis[j, j, 0];
                MissingSigma2j = (int)Math.Round(M22 * (M22 * (1d / M12 + 1d / M12prime + 1d / M11prime))) + dpCorMultis[j, j, 0];
            }
            MissingPairsi = rangeNames[j];
            MissingPairsj = rangeNames[i];
        }
        public double getMassToCharge(string rangeName1)
        {
            string[] name1 = rangeName1.Split('-');
            double dpos1 = 0d;
            Double.TryParse(name1[0], out dpos1);
            return dpos1;
        }
        public bool sameChargeState(string rangeName1, string rangeName2)
        {
            // Same charge state -- >5.5/1.33 and <5.5*1.33
            double dpos1 = getMassToCharge(rangeName1);
            double dpos2 = getMassToCharge(rangeName2);

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
        public void returnCSRPair(int input, ref bool found, ref int numerator, ref int denominator)
        {
            found = false;
            string inputName = rangeNames[input];
            for (int i=0; i<N; i++)
            {
                if (inputName.Equals(rangeNames[i])) continue;
                if (sameElement(inputName, rangeNames[i]))
                {
                    if (!sameChargeState(inputName, rangeNames[i]))
                    {
                        double dpos1 = getMassToCharge(inputName);
                        double dpos2 = getMassToCharge(rangeNames[i]);
                        if (dpos1 > dpos2)
                        {
                            if (dpos1/dpos2 > 1.94 && dpos1/dpos2 < 2.06)
                            {
                                found = true;
                                numerator = i;
                                denominator = input;
                                return;
                            }
                        }
                        else
                        {
                            if (dpos2 / dpos1 > 1.94 && dpos2 / dpos1 < 2.06)
                            {
                                found = true;
                                numerator = input;
                                denominator = i;
                                return;
                            }
                        }
                    }
                }
            }
        }
        public int getMissing3s(int i, int j, int k, ref int sigma2)
        {
            double M12 = (double)dpCorMultis[i, j, 0] + (double)dpCorMultis[j, i, 0];
            double M13 = (double)dpCorMultis[i, k, 0] + (double)dpCorMultis[k, i, 0];
            double M23 = (double)dpCorMultis[j, k, 0] + (double)dpCorMultis[k, j, 0];
            double M11 = 0.5d * M12 * M13 / M23;
            sigma2 = (int)Math.Round(M11 * (M11 * (1d / M12 + 1d / M13 + 1d / M23))) + dpCorMultis[i, i, 0];
            return (int)Math.Round(M11) - dpCorMultis[i, i, 0];
        }
        public double getIonFraction(string ion)
        {
            foreach (var isotope in isotopeList)
            {
                if (isotope.Name.Equals(ion))
                {
                    return isotope.Fraction;
                }
            }
            return 1d;
        }
    }
}