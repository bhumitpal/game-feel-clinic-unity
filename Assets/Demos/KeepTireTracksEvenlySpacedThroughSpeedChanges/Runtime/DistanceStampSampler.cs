using System;
using UnityEngine;

namespace GameFeelClinic.DistanceTracks
{
    [DisallowMultipleComponent]
    public sealed class DistanceStampSampler : MonoBehaviour
    {
        [SerializeField] private bool sampleByDistance = true;
        [SerializeField, Min(0.05f)] private float spacing = 0.6f;
        [SerializeField, Min(0.05f)] private float resetDistance = 6f;

        private Vector3 previous;
        private double distanceToNext;
        private bool initialized;

        public bool SampleByDistance => sampleByDistance;
        public float Spacing => spacing;
        public float ResetDistance => resetDistance;

        public void Configure(float spacing, float resetDistance, bool sampleByDistance)
        {
            this.spacing = Mathf.Max(0.05f, spacing);
            this.resetDistance = Mathf.Max(this.spacing, resetDistance);
            this.sampleByDistance = sampleByDistance;
            initialized = false;
        }

        public void Reset(Vector3 position)
        {
            previous = position;
            distanceToNext = spacing;
            initialized = true;
        }

        public void Observe(Vector3 position, Action<Vector3> stamp)
        {
            if (!initialized)
            {
                Reset(position);
                return;
            }

            Vector3 segment = position - previous;
            float length = segment.magnitude;

            if (length > resetDistance)
            {
                Reset(position);
                return;
            }

            if (length <= 0f)
                return;

            if (sampleByDistance)
            {
                double next = distanceToNext;
                while (next <= length)
                {
                    float t = (float)(next / length);
                    stamp(Vector3.Lerp(previous, position, t));
                    next += spacing;
                }
                distanceToNext = next - length;
            }
            else
            {
                stamp(position);
            }

            previous = position;
        }
    }
}
