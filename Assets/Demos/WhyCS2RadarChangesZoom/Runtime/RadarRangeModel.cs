using System;
using System.Collections.Generic;

namespace GameFeelClinic.DynamicRadar
{

    public struct RadarMarker
    {
        public readonly string Id;
        public readonly double X;
        public readonly double Y;
        public readonly bool Known;

        public RadarMarker(string id, double x, double y, bool known)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentException("An actor ID is required.", "id");
            Id = id;
            X = x;
            Y = y;
            Known = known;
        }
    }

    public sealed class RadarRangeModel
    {
        private const double SnapEpsilon = 0.000001;
        private bool waitingForDrop = true;

        public double Minimum { get; private set; }
        public double Maximum { get; private set; }
        public double Padding { get; private set; }
        public double HoldSeconds { get; private set; }
        public double ShrinkTimeConstant { get; private set; }
        public double Current { get; private set; }
        public double Required { get; private set; }
        public double HoldRemaining { get; private set; }
        public int KnownCount { get; private set; }
        public bool MaximumReached { get; private set; }

        public RadarRangeModel(double minimum = 12, double maximum = 60,
            double padding = 4, double holdSeconds = 0.8, double shrinkTimeConstant = 0.7)
        {
            if (!Finite(minimum) || minimum <= 0) throw new ArgumentOutOfRangeException("minimum");
            if (!Finite(maximum) || maximum < minimum) throw new ArgumentOutOfRangeException("maximum");
            if (!Finite(padding) || padding < 0) throw new ArgumentOutOfRangeException("padding");
            if (!Finite(holdSeconds) || holdSeconds < 0) throw new ArgumentOutOfRangeException("holdSeconds");
            if (!Finite(shrinkTimeConstant) || shrinkTimeConstant <= 0) throw new ArgumentOutOfRangeException("shrinkTimeConstant");
            Minimum = minimum;
            Maximum = maximum;
            Padding = padding;
            HoldSeconds = holdSeconds;
            ShrinkTimeConstant = shrinkTimeConstant;
            Reset();
        }

        public void Reset()
        {
            Current = Required = Minimum;
            HoldRemaining = HoldSeconds;
            KnownCount = 0;
            MaximumReached = false;
            waitingForDrop = true;
        }

        public double Update(IReadOnlyList<RadarMarker> markers, double deltaTimeSeconds)
        {
            if (markers == null) throw new ArgumentNullException("markers");
            if (!Finite(deltaTimeSeconds) || deltaTimeSeconds < 0)
                throw new ArgumentOutOfRangeException("deltaTimeSeconds");

            double farthest = 0;
            int known = 0;
            for (int i = 0; i < markers.Count; i++)
            {
                RadarMarker marker = markers[i];
                if (!marker.Known) continue;
                if (!Finite(marker.X) || !Finite(marker.Y))
                    throw new ArgumentException("Known marker offsets must be finite.", "markers");
                double distance = Math.Sqrt(marker.X * marker.X + marker.Y * marker.Y);
                if (!Finite(distance)) throw new ArgumentException("Marker distance overflow.", "markers");
                farthest = Math.Max(farthest, distance);
                known++;
            }
            KnownCount = known;
            double padded = known == 0 ? Minimum : farthest + Padding;
            MaximumReached = padded >= Maximum;
            Required = Math.Max(Minimum, Math.Min(Maximum, padded));

            if (Required >= Current - SnapEpsilon)
            {

                Current = Required;
                HoldRemaining = HoldSeconds;
                waitingForDrop = true;
                return Current;
            }

            if (waitingForDrop)
            {
                HoldRemaining = HoldSeconds;
                waitingForDrop = false;
            }
            double waiting = Math.Min(HoldRemaining, deltaTimeSeconds);
            HoldRemaining = Math.Max(0, HoldRemaining - waiting);
            double shrinkTime = deltaTimeSeconds - waiting;
            if (shrinkTime > 0)
            {

                Current = Required + (Current - Required) * Math.Exp(-shrinkTime / ShrinkTimeConstant);
                if (Current - Required < SnapEpsilon) Current = Required;
            }
            return Current;
        }

        private static bool Finite(double value)
        {
            return !Double.IsNaN(value) && !Double.IsInfinity(value);
        }
    }
}
