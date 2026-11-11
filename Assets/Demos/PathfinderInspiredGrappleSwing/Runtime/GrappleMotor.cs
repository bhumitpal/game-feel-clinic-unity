using System;
using UnityEngine;

namespace GameFeelClinic.Grapple
{
    [Serializable]
    public struct GrappleState
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public bool Taut;
    }

    public static class GrappleMotor
    {

        public static bool Constrain(ref Vector3 position, ref Vector3 velocity,
            Vector3 anchor, float maxLength)
        {
            RequireFinite(position, nameof(position));
            RequireFinite(velocity, nameof(velocity));
            RequireFinite(anchor, nameof(anchor));
            RequireLength(maxLength);

            if (maxLength == 0f)
            {
                position = anchor;
                velocity = Vector3.zero;
                return true;
            }

            double dx = (double)position.x - anchor.x;
            double dy = (double)position.y - anchor.y;
            double dz = (double)position.z - anchor.z;
            double distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (distance == 0d)
                return false;

            double tolerance = Math.Max(maxLength * 1e-6d,
                Math.Min(1e-6d, maxLength * 1e-4d));
            bool taut = distance >= (double)maxLength - tolerance;
            if (!taut)
                return false;

            Vector3 n = new Vector3((float)(dx / distance),
                (float)(dy / distance), (float)(dz / distance));
            Vector3 constrainedPosition = position;
            Vector3 constrainedVelocity = velocity;
            if (distance > maxLength)
            {
                double scale = maxLength / distance;
                constrainedPosition = new Vector3(
                    (float)(anchor.x + dx * scale),
                    (float)(anchor.y + dy * scale),
                    (float)(anchor.z + dz * scale));
            }

            RemoveOutward(ref constrainedVelocity, n);
            RequireFinite(constrainedPosition, "projected position");
            RequireFinite(constrainedVelocity, "projected velocity");
            position = constrainedPosition;
            velocity = constrainedVelocity;
            return true;
        }

        private static void RemoveOutward(ref Vector3 velocity, Vector3 n)
        {
            float outward = Vector3.Dot(velocity, n);
            if (outward > 0f)
                velocity -= n * outward;
        }

        public static void Step(ref GrappleState state, float dt, Vector3 gravity,
            Vector3 anchor, float maxLength, bool attached)
        {
            if (!Finite(dt) || dt < 0f)
                throw new ArgumentOutOfRangeException(nameof(dt),
                    "dt must be finite and nonnegative.");
            RequireFinite(state.Position, "state.Position");
            RequireFinite(state.Velocity, "state.Velocity");
            RequireFinite(gravity, nameof(gravity));
            RequireFinite(anchor, nameof(anchor));
            RequireLength(maxLength);

            Vector3 velocity = state.Velocity + gravity * dt;
            Vector3 position = state.Position + velocity * dt;
            RequireFinite(velocity, "integrated velocity");
            RequireFinite(position, "predicted position");
            bool taut = attached && Constrain(ref position, ref velocity,
                anchor, maxLength);

            state.Position = position;
            state.Velocity = velocity;
            state.Taut = taut;
        }

        private static bool Finite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void RequireFinite(Vector3 value, string parameter)
        {
            if (!Finite(value.x) || !Finite(value.y) || !Finite(value.z))
                throw new ArgumentException("Vector must contain finite values.", parameter);
        }

        private static void RequireLength(float value)
        {
            if (!Finite(value) || value < 0f)
                throw new ArgumentOutOfRangeException(nameof(value),
                    "maxLength must be finite and nonnegative.");
        }
    }
}
