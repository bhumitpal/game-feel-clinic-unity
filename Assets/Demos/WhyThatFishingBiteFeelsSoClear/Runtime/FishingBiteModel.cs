using UnityEngine;

namespace GameFeelClinic.Fishing
{
    public enum FishingPass { Baseline, Fix, Proof }

    public static class FishingBiteModel
    {
        public const float CommitAt = 2.70f;
        public const float ReelAt = 4.15f;
        public const float WindowLength = 1.85f;
        public const float FixedWaterline = 0.02922f;
        public const float PreviewLength = 7.8f;
        public static readonly Vector2 FloatPosition = new Vector2(0.65f, 1.0f);
        public static readonly float FalseCueAt = FindFixedLineCrossing();

        public static float WaterHeight(Vector2 position, float time)
        {
            float first = position.x * 1.6f + position.y * 1.25f + time * 2.3f;
            float second = position.x * 3.1f - position.y * 2.8f + time * 4.3f;
            return 0.045f * Mathf.Sin(first) + 0.022f * Mathf.Sin(second);
        }

        public static float Pull(float time)
        {
            float rise = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(CommitAt, CommitAt + 0.36f, time));
            float relax = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(CommitAt + 0.48f, CommitAt + 1.16f, time));
            return rise * (1f - 0.78f * relax);
        }

        public static float RelativeHeight(float time, bool committed)
        {
            float floatMotion = 0.096f + 0.026f * Mathf.Sin(time * 3.1f) + 0.013f * Mathf.Sin(time * 6.2f);
            return floatMotion - (committed ? 0.30f * Pull(time) : 0f);
        }

        public static float WorldFloatHeight(float time, bool committed) =>
            WaterHeight(FloatPosition, time) + RelativeHeight(time, committed);

        static float FindFixedLineCrossing()
        {
            const int rate = 120;
            for (int frame = 1; frame < CommitAt * rate; frame++)
            {
                float start = (frame - 1f) / rate;
                float end = frame / (float)rate;
                float startHeight = WorldFloatHeight(start, false);
                float endHeight = WorldFloatHeight(end, false);
                if (startHeight > FixedWaterline && endHeight <= FixedWaterline)
                    return Mathf.Lerp(start, end, Mathf.InverseLerp(startHeight, endHeight, FixedWaterline));
            }
            throw new System.InvalidOperationException("The idle wave did not cross the fixed baseline height.");
        }

        public static bool CanReel(float time, bool committed)
        {
            return committed && time >= CommitAt && time < CommitAt + WindowLength;
        }

        public static bool TryCrossing(float previous, float current, bool committed, out float contact)
        {
            contact = 0;
            if (!committed || current < CommitAt || previous >= current) return false;
            float low = Mathf.Max(CommitAt, previous);
            float high = current;
            if (RelativeHeight(low, true) <= 0f || RelativeHeight(high, true) > 0f) return false;

            for (int i = 0; i < 16; i++)
            {
                float middle = (low + high) * 0.5f;
                if (RelativeHeight(middle, true) > 0f) low = middle;
                else high = middle;
            }

            contact = high;
            return true;
        }

        public static float FirstCrossing(int sampleRate)
        {
            for (int frame = 1; frame <= sampleRate * 5; frame++)
            {
                if (TryCrossing((frame - 1f) / sampleRate, frame / (float)sampleRate, true, out float contact))
                    return contact;
            }
            throw new System.InvalidOperationException("The committed bite did not cross the water surface.");
        }
    }
}
