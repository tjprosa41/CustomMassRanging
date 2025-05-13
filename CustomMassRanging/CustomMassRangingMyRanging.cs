using System;
using System.Collections.Generic;
using System.Numerics;

namespace CustomMassRanging
{
    public class MyRanging
    {
        public Vector2[] Values;
        public int Length;
        public float StartPos;
        public float BinWidth;
        public float MaxPos;

        public MyRanging(int length, float startPos, float binWidth, float maxPos)
        {
            Values = new Vector2[length];
            Length = length;
            StartPos = startPos;
            BinWidth = binWidth;
            MaxPos = maxPos;
        }

        public float GetPos(int index)
        {
            return (StartPos + ((float)index) * BinWidth);
        }

        public int GetIndex(float pos)
        {
            return (int)Math.Round((pos - StartPos) / BinWidth);
        }

        public float FindLocalMax(double min, double max)
        {
            int start = GetIndex((float)min);
            int stop = GetIndex((float)max);
            float maxValue = 0.0f;
            float maxPos = 0.0f;
            for (int i = start; i <= stop; i++)
            {
                if (Values[i].Y > maxValue)
                {
                    maxValue = Values[i].Y;
                    maxPos = Values[i].X;
                }
            }
            return maxPos;
        }

        public double Integrate(int first, int last, int delta = 0)
        {
            double total = 0.0d;
            for (int i = first + delta; i <= last + delta; i++) total += (double)Values[i].Y;
            return total;
        }

        public double HalfBackground(int first, int last, int delta = 0)
        {
            int width = last - first + 1;
            double total = 0.0d;
            for (int i = first + delta - width / 2; i < first + delta; i++) total += (double)Values[i].Y;
            for (int i = last + delta + 1; i <= last + delta + width / 2; i++) total += (double)Values[i].Y;
            return total;
        }
        public double HalfBackgroundLeft(int first, int last, int delta = 0)
        {
            int width = last - first + 1;
            double total = 0.0d;
            for (int i = first + delta - width / 2; i < first + delta; i++) total += (double)Values[i].Y;
            return total;
        }
        public double HalfBackgroundRight(int first, int last, int delta = 0)
        {
            int width = last - first + 1;
            double total = 0.0d;
            for (int i = last + delta + 1; i <= last + delta + width / 2; i++) total += (double)Values[i].Y;
            return total;
        }

        public double QuarterBackground(int first, int last, int delta = 0)
        {
            int width = last - first + 1;
            double total = 0.0d;
            for (int i = first + delta - width / 4; i < first + delta; i++) total += (double)Values[i].Y;
            for (int i = last + delta + 1; i <= last + delta + width / 4; i++) total += (double)Values[i].Y;
            total *= 2.0d;
            return total;
        }
        public double QuarterBackgroundLeft(int first, int last, int delta = 0)
        {
            int width = last - first + 1;
            double total = 0.0d;
            for (int i = first + delta - width / 4; i < first + delta; i++) total += (double)Values[i].Y;
            total *= 2.0d;
            return total;
        }
        public double QuarterBackgroundRight(int first, int last, int delta = 0)
        {
            int width = last - first + 1;
            double total = 0.0d;
            for (int i = last + delta + 1; i <= last + delta + width / 4; i++) total += (double)Values[i].Y;
            total *= 2.0d;
            return total;
        }

        public double NetMax(RangeScheme? scheme, int left, int right, ref int shift, ref double raw)
        {
            shift = 0;
            double netMax = 0.0d;

            //Left will have a constant background removed, so just maximize position, ignore background
            if (scheme == RangeScheme.Left || scheme == RangeScheme.LeftTail)
            {
                raw = Integrate(left, right);
                netMax = raw;
                double rawLeft = Integrate(left, right, -1);
                double netLeft = rawLeft;
                double rawRight = Integrate(left, right, +1);
                double netRight = rawRight;
                if (netLeft > netMax)
                {
                    shift = -1;
                    while (netLeft > netMax)
                    {
                        shift--;
                        raw = rawLeft;
                        netMax = netLeft;
                        rawLeft = Integrate(left, right, shift);
                        netLeft = rawLeft;
                    }
                    shift++;
                }
                else if (netRight > netMax)
                {
                    shift = +1;
                    while (netRight > netMax)
                    {
                        shift++;
                        raw = rawRight;
                        netMax = netRight;
                        rawRight = Integrate(left, right, shift);
                        netRight = rawRight;
                    }
                    shift--;
                }
            }
            else if (scheme == RangeScheme.Half)
            {
                raw = Integrate(left, right);
                netMax = raw - HalfBackground(left, right);
                double rawLeft = Integrate(left, right, -1);
                double netLeft = rawLeft - HalfBackground(left, right, -1);
                double rawRight = Integrate(left, right, +1);
                double netRight = rawRight - HalfBackground(left, right, +1);
                if (netLeft > netMax)
                {
                    shift = -1;
                    while (netLeft > netMax)
                    {
                        shift--;
                        raw = rawLeft;
                        netMax = netLeft;
                        rawLeft = Integrate(left, right, shift);
                        netLeft = rawLeft - HalfBackground(left, right, shift);
                    }
                    shift++;
                }
                else if (netRight > netMax)
                {
                    shift = +1;
                    while (netRight > netMax)
                    {
                        shift++;
                        raw = rawRight;
                        netMax = netRight;
                        rawRight = Integrate(left, right, shift);
                        netRight = rawRight - HalfBackground(left, right, shift);
                    }
                    shift--;
                }
            }
            else if (scheme == RangeScheme.Quarter)
            {
                raw = Integrate(left, right);
                netMax = raw - QuarterBackground(left, right);
                double rawLeft = Integrate(left, right, -1);
                double netLeft = rawLeft - QuarterBackground(left, right, -1);
                double rawRight = Integrate(left, right, +1);
                double netRight = rawRight - QuarterBackground(left, right, +1);
                if (netLeft > netMax)
                {
                    shift = -1;
                    while (netLeft > netMax)
                    {
                        shift--;
                        raw = rawLeft;
                        netMax = netLeft;
                        rawLeft = Integrate(left, right, shift);
                        netLeft = rawLeft - QuarterBackground(left, right, shift);
                    }
                    shift++;
                }
                else if (netRight > netMax)
                {
                    shift = +1;
                    while (netRight > netMax)
                    {
                        shift++;
                        raw = rawRight;
                        netMax = netRight;
                        rawRight = Integrate(left, right, shift);
                        netRight = rawRight - QuarterBackground(left, right, shift);
                    }
                    shift--;
                }
            }
            return netMax;
        }

        public void DetermineRange(float dPos, RangeScheme? scheme, Parameters parameters, ref float newLeft, ref float newRight,
            ref double netMax, ref double raw, ref double leftBgd, ref double rightBgd)
        {
            //nBinsMin needs to be divisible by 4 to support half AND quarter ranging -- added factor that scales minimum width
            int nBinsMin = 0;

            //Left is the same regardless of bUseFixedRangingWidth
            if (scheme == RangeScheme.Left || scheme == RangeScheme.LeftTail)
                nBinsMin = (int)((float)(parameters.DMaxPeakFWHunM * parameters.DRangingWidthFactor) / BinWidth);
            else
            {
                if (parameters.bUseFixedRangingWidth)
                {
                    nBinsMin = (int)((float)(parameters.DMaxPeakFWHunM * parameters.DRangingWidthFactor) / BinWidth / 4.0f * Math.Sqrt(dPos / MaxPos));
                    nBinsMin *= 4;
                }
                else
                {
                    nBinsMin = (int)((float)(parameters.DMinWidthFactor * parameters.DMaxPeakFWHunM) / BinWidth / 4.0f * Math.Sqrt(dPos / MaxPos));
                    nBinsMin *= 4;
                }
            }
            int width = nBinsMin;
            int startIndex = GetIndex(dPos);
            int left = startIndex - width / 2 + 1;
            int right = startIndex + width / 2;
            int shift = 0;

            //Left is the same regardless of bUseFixedRangingWidth
            if (scheme == RangeScheme.Left || scheme == RangeScheme.LeftTail)
            {
                raw = 0.0d;
                netMax = NetMax(scheme, left, right, ref shift, ref raw); //shift is a return value...set to zero inside to start
                left = left + shift; //expand and shift based on result, then repeat
                right = right + shift;

                //netMax is the netMax and raw-netMax is the background, depending on the method
                newLeft = GetPos(left - 1); //Need plot and range to both go up to next bin edge, so add -1 bigger
                newRight = GetPos(right);
                int nBinsDelta = (int)((float)(parameters.DLeftRangeDelta / BinWidth));
                if (left - nBinsDelta < 0) nBinsDelta = left;
                leftBgd = Integrate(left, right, -nBinsDelta);
                rightBgd = 0;
                netMax -= leftBgd;
                return;
            }

            int expand = 0;
            if (scheme == RangeScheme.Half || scheme == RangeScheme.Left || scheme == RangeScheme.LeftTail) expand = 1;
            else if (scheme == RangeScheme.Quarter) expand = 2;

            raw = 0.0d;
            netMax = NetMax(scheme, left, right, ref shift, ref raw); //shift is a return value...set to zero inside to start
            left = left + shift - expand; //expand and shift based on result, then repeat
            right = right + shift + expand;
            int oldshift = shift;
            shift = 0;
            double newRaw = 0.0d;
            double newMax = NetMax(scheme, left, right, ref shift, ref newRaw);

            //Skip below when using fixed ranging width
            while (newMax > netMax && !parameters.bUseFixedRangingWidth)
            {
                raw = newRaw;
                netMax = newMax;
                left = left + shift - expand;
                right = right + shift + expand;
                oldshift = shift;
                shift = 0;
                newMax = NetMax(scheme, left, right, ref shift, ref newRaw);
            }
            left = left - oldshift + expand;
            right = right - oldshift - expand;

            //netMax is the netMax and raw-netMax is the background, depending on the method
            newLeft = GetPos(left - 1); //Need plot and range to both go up to next bin edge, so add -1 bigger
            newRight = GetPos(right);

            if (scheme == RangeScheme.Half)
            {
                leftBgd = HalfBackgroundLeft(left, right);
                rightBgd = HalfBackgroundRight(left, right);
            }
            else if (scheme == RangeScheme.Quarter)
            {
                leftBgd = QuarterBackgroundLeft(left, right);
                rightBgd = QuarterBackgroundRight(left, right);
            }
        }

        public List<Vector3> FindAllPeaks(Parameters parameters)
        {
            double Sensitivity = parameters.dSensitivity; //Needs to be >=1
            int MinBinPairs = parameters.iMinBinPairs; //Use 2X these number of bins
            int MinPeakMaxCounts = parameters.iMinPeakMaxCounts;

            //Go through entire mass spectrum and use minimum range width to see if there are statistically significant counts
            List<Vector3> peaks = new();
            float factor = (float)(parameters.dMinWidthFactor * parameters.dMaxPeakFWHunM) / BinWidth;
            float currentPos = GetPos(Length - 1);
            //Need to accomodate for width at end of histogram and 1/2-range width
            //Also want to be even
            int stopBinWidth = 2 * (int)(factor * Math.Sqrt(currentPos / MaxPos) * 1.5d / 2d) + 2;
            for (int left = GetIndex(0.8f); left < Length - stopBinWidth; left++)
            {
                currentPos = GetPos(left);
                int nBins = 2 * (int)(factor * Math.Sqrt(currentPos / MaxPos) / 2d) + 2;
                if (nBins < 2 * MinBinPairs) nBins = 2 * MinBinPairs;
                int right = left + nBins - 1;
                double raw = Integrate(left, right);
                double bgd = HalfBackground(left, right);
                double net = raw - bgd;
                //99% CL Detected
                double max = 0d;
                int maxPoint = 0;
                double criteria = 3.289d * Math.Sqrt(bgd) / Sensitivity;
                if (net > criteria)
                {
                    //Find max position
                    for (int j = left; j <= right; j++)
                    {
                        if (Values[j].Y > max)
                        {
                            max = Values[j].Y;
                            maxPoint = j;
                        }
                    }
                    if (maxPoint == right)
                    {
                        int j = right + 1;
                        while (true)
                        {
                            if (Values[j].Y > max)
                            {
                                max = Values[j].Y;
                                maxPoint = j++;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    //Now allow shift and find max integrated
                    int delta = nBins / 2;
                    int shift = 0;
                    double newRaw = 0d;
                    double netMax = NetMax(RangeScheme.Half, maxPoint - delta, maxPoint + delta - 1, ref shift, ref newRaw);
                    double maxPos = GetPos(maxPoint);
                    double rightEdge = GetPos(left + nBins + shift);
                    if (max > (double)MinPeakMaxCounts)
                    {
                        peaks.Add(new Vector3(GetPos(maxPoint), -2.0f, (float)max));
                        left = maxPoint + delta + shift - 1;
                    }
                }
            }
            return peaks;
        }
    }
}
