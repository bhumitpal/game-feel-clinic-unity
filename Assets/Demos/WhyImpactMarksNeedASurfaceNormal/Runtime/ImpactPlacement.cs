using System;
using UnityEngine;

namespace GameFeelClinic.SurfaceImpact
{

    public static class ImpactPlacement
    {
        private const float MinimumDirectionLength = 0.000001f;
        private const float ParallelUpDot = 0.999f;

        public static Pose BuildPose(RaycastHit hit, float offset, Vector3 up)
        {
            if (!IsFinite(hit.point) || !IsFinite(hit.normal))
                throw new ArgumentException("Hit point and normal must be finite.", nameof(hit));
            if (!IsFinite(offset) || offset < 0f)
                throw new ArgumentOutOfRangeException(nameof(offset), "Use a finite, nonnegative surface offset.");
            if (!IsFinite(up))
                throw new ArgumentException("Up direction must be finite.", nameof(up));

            float normalLength = hit.normal.magnitude;
            if (!IsFinite(normalLength) || normalLength < MinimumDirectionLength)
                throw new ArgumentException("Use a nonzero normal from a successful raycast.", nameof(hit));

            Vector3 normal = hit.normal / normalLength;
            Vector3 referenceUp = up.normalized;
            if (referenceUp.sqrMagnitude < 0.5f ||
                Mathf.Abs(Vector3.Dot(referenceUp, normal)) > ParallelUpDot)
                referenceUp = LeastAlignedAxis(normal);

            Vector3 tangentUp = Vector3.ProjectOnPlane(referenceUp, normal).normalized;
            Quaternion rotation = Quaternion.LookRotation(normal, tangentUp);
            Vector3 position = hit.point + normal * offset;
            return new Pose(position, rotation);
        }

        private static Vector3 LeastAlignedAxis(Vector3 normal)
        {
            Vector3 absolute = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            if (absolute.x <= absolute.y && absolute.x <= absolute.z)
                return Vector3.right;
            return absolute.y <= absolute.z ? Vector3.up : Vector3.forward;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
