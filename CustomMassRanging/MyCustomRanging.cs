using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace CustomMassRanging
{
    public class MyCustomRanging
    {
        public Vector2[] Values;
        public int Length;
        public float StartPos;
        public float BinWidth;

        public MyCustomRanging(int length, float startPos, float binWidth)
        {
            Values = new Vector2[length];
            Length = length;
            StartPos = startPos;
            BinWidth = binWidth;
        }

        public float GetPos(int index)
        {
            return (StartPos + (float)index * BinWidth);
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

        public double QuarterBackground(int first, int last, int delta = 0)
        {
            int width = last - first + 1;
            double total = 0.0d;
            for (int i = first + delta - width / 4; i < first + delta; i++) total += (double)Values[i].Y;
            for (int i = last + delta + 1; i <= last + delta + width / 4; i++) total += (double)Values[i].Y;
            total *= 2.0d;
            return total;
        }

        public double NetMax(Scheme.RangeScheme? scheme, int left, int right, ref int shift, ref double raw)
        {
            shift = 0;
            double netMax = 0.0d;
            if (scheme == Scheme.RangeScheme.Half || scheme == Scheme.RangeScheme.Left)
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
            else if (scheme == Scheme.RangeScheme.Quarter)
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

        public void DetermineRange(float dPos, Scheme.RangeScheme? scheme, Parameters parameters, ref float newLeft, ref float newRight, ref double netMax, ref double raw)
        {
            //nBinsMin needs to be divisible by 4 to support half AND quarter ranging
            int nBinsMin = (int)((float)(parameters.DMinWidthFactor * parameters.DMaxPeakFWHunM) / BinWidth / 4.0f);
            nBinsMin *= 4;

            int width = nBinsMin;
            int startIndex = GetIndex(dPos);
            int left = startIndex - width / 2 + 1;
            int right = startIndex + width / 2;
            int shift = 0;

            //Need to incorporate Left later...!!!
            int expand = 0;
            if (scheme == Scheme.RangeScheme.Half || scheme == Scheme.RangeScheme.Left) expand = 1;
            else if (scheme == Scheme.RangeScheme.Quarter) expand = 2;

            raw = 0.0d;
            netMax = NetMax(scheme, left, right, ref shift, ref raw); //shift is a return value...set to zero inside to start
            left = left + shift - expand; //expand and shift based on result, then repeat
            right = right + shift + expand;
            double newRaw = 0.0d;
            double newMax = NetMax(scheme, left, right, ref shift, ref newRaw);
            while (newMax > netMax)
            {
                raw = newRaw;
                netMax = newMax;
                left = left + shift - expand;
                right = right + shift + expand;
                newMax = NetMax(scheme, left, right, ref shift, ref newRaw);
            }
            left = left - shift + expand;
            right = right - shift - expand;
            //netMax is the netMax and raw-netMax is the background, depending on the method
            newLeft = GetPos(left);
            newRight = GetPos(right);
        }
    }
}
