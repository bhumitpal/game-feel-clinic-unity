using System;
using System.Numerics;

namespace GameFeelClinic.ResponsiveSmoke
{

    public static class SmokeFieldMath
    {
        public static float DistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
        {
            Vector3 axis = end - start;
            float along = Clamp01(Vector3.Dot(point - start, axis) / Math.Max(Vector3.Dot(axis, axis), 1e-8f));
            return Vector3.Distance(point, start + axis * along);
        }

        public static float DensityMultiplier(
            Vector3 point, Vector3 start, Vector3 end,
            float radius, float softness, float cutStrength, float wholeOpacity = 1f)
        {
            radius = Math.Max(radius, 0f);
            softness = Math.Max(softness, 1e-4f);
            float distance = DistanceToSegment(point, start, end);
            float keep = SmoothStep(Math.Max(0f, radius - softness), radius + softness, distance);
            return Clamp01(wholeOpacity) * (1f - Clamp01(cutStrength) * (1f - keep));
        }

        public static float RecoveryStrength(float elapsedSeconds, float holdSeconds, float recoverySeconds)
        {
            if (elapsedSeconds < 0f)
                return 0f;

            holdSeconds = Math.Max(holdSeconds, 0f);
            if (elapsedSeconds <= holdSeconds)
                return 1f;

            if (recoverySeconds <= 0f)
                return 0f;

            return 1f - SmoothStep(holdSeconds, holdSeconds + recoverySeconds, elapsedSeconds);
        }

        public static float Opacity(float density, float worldDistance)
        {
            return 1f - (float)Math.Exp(-Math.Max(density, 0f) * Math.Max(worldDistance, 0f));
        }

        private static float SmoothStep(float start, float end, float value)
        {
            float t = Clamp01((value - start) / (end - start));
            return t * t * (3f - 2f * t);
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }
    }
}
