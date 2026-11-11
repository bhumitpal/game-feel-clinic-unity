using System;
using UnityEngine;

namespace GameFeelClinic.LockOn
{

    public sealed class StableLockSelector : MonoBehaviour
    {
        public Transform[] Registry = Array.Empty<Transform>();
        public Transform AimOrigin;
        [Min(0f)] public float Range = 12f;
        [Range(0f, 180f)] public float HalfAngle = 35f;
        [Min(0f)] public float SwitchMargin = .025f;
        public LayerMask ObstacleMask = 1;
        public int CurrentIndex = -1;
        public Transform Current => Registry != null && CurrentIndex >= 0 && CurrentIndex < Registry.Length ? Registry[CurrentIndex] : null;

        public Transform Tick(bool useHysteresis = true)
        {
            Transform aim = AimOrigin != null ? AimOrigin : transform;

            CurrentIndex = Select(Registry, aim.position, aim.forward,
                CurrentIndex, Range, HalfAngle, SwitchMargin,
                ObstacleMask, null, useHysteresis);

            return Current;
        }

        public static int Select(Transform[] registry, Vector3 origin, Vector3 forward,
            int currentIndex, float range = 12f, float halfAngle = 35f,
            float switchMargin = .025f, int obstacleMask = 1,
            Vector3[] positions = null, bool useHysteresis = true)
        {
            if (registry == null || registry.Length == 0 || range <= 0f || forward.sqrMagnitude < 1e-8f)
                return -1;
            if (positions != null && positions.Length != registry.Length)
                throw new ArgumentException("Supplied positions must match the registry length.");
            Vector3 aim = forward.normalized;
            float minDot = Mathf.Cos(Mathf.Clamp(halfAngle, 0f, 180f) * Mathf.Deg2Rad);
            float rangeSquared = range * range;
            float bestScore = float.NegativeInfinity;
            float currentScore = float.NegativeInfinity;
            int bestIndex = -1;

            for (int i = 0; i < registry.Length; i++)
            {
                Transform target = registry[i];
                if (target == null || !target.gameObject.activeInHierarchy) continue;
                Vector3 point = positions == null ? target.position : positions[i];
                if (!TryScore(point, origin, aim, rangeSquared, minDot, obstacleMask, out float score)) continue;

                if (i == currentIndex)
                    currentScore = score;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            bool currentValid = !float.IsNegativeInfinity(currentScore);
            if (!useHysteresis || !currentValid)
                return bestIndex;
            if (bestScore > currentScore + Mathf.Max(0f, switchMargin))
                return bestIndex;
            return currentIndex;

        }

        public static bool TryScore(Vector3 point, Vector3 origin, Vector3 normalizedForward,
            float rangeSquared, float minDot, int obstacleMask, out float score)
        {
            score = float.NegativeInfinity;

            Vector3 offset = point - origin;
            float distanceSquared = offset.sqrMagnitude;
            if (distanceSquared < 1e-8f || distanceSquared > rangeSquared)
                return false;

            float alignment = Vector3.Dot(normalizedForward, offset.normalized);
            if (alignment + 1e-6f < minDot)
                return false;

            if (Physics.Linecast(origin, point, obstacleMask,
                QueryTriggerInteraction.Ignore))
                return false;

            score = alignment;
            return true;
        }
    }
}
